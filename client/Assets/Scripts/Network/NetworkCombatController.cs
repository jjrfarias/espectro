using System;
using System.Collections.Generic;
using Espectro.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Espectro.Network
{
    // Apresentação e intenções. Nenhum dano, XP ou renascimento é calculado aqui.
    public sealed class NetworkCombatController : MonoBehaviour
    {
        public event Action OnlineRequested;
        public event Action LocalRequested;
        public event Action<string> AttackRequested;
        public event Action AttributesToggleRequested;

        private readonly Dictionary<string, NetworkEnemyView> enemies = new();
        private readonly HashSet<string> present = new();
        private readonly List<string> removed = new();
        private readonly List<NetworkEnemyView> candidates = new();
        private PrototypePlayerController player;
        private GameObject battleHud;
        private Text modeText;
        private Text healthText;
        private Text progressText;
        private Text targetText;
        private Text messageText;
        private GameObject messageBackdrop;
        private Text deathText;
        private GameObject deathBackdrop;
        private Image healthFill;
        private Button modeButton;
        private Button attackButton;
        private Button targetButton;
        private Text modeButtonLabel;
        private NetworkEnemyView selected;
        private bool local = true;
        private bool online;
        private bool alive;
        private int level = 1;
        private int xp;
        private Vector3 confirmedPosition;
        private float nextAttackAt;
        private float messageUntil;
        private const float SelectionRange = 25f;
        // Espelha PLAYER_ATTACK_RANGE_UNITS em enemy-definitions.ts — sem isso o botão ATACAR
        // ficava clicável até 25m (raio de seleção de alvo), convidando cliques que o servidor
        // sempre recusava com OUT_OF_RANGE além de ~2,25m.
        private const float AttackRangeUnits = 2.25f;

        public static NetworkCombatController Create(PrototypePlayerController player)
        {
            var root = new GameObject("Combate Online - Interface", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem Combate Online", typeof(EventSystem), typeof(StandaloneInputModule));
            var result = root.AddComponent<NetworkCombatController>();
            result.player = player;
            result.BuildHud();
            return result;
        }

        public void SetLocalMode()
        {
            ClearEnemies();
            local = true;
            online = alive = false;
            battleHud.SetActive(false);
            modeText.text = "Exploração local";
            modeButtonLabel.text = "ENTRAR ONLINE";
        }

        public void SetConnectingMode(string message)
        {
            ClearEnemies();
            local = online = alive = false;
            battleHud.SetActive(false);
            modeText.text = message;
            modeButton.gameObject.SetActive(false);
        }

        public void PrepareCharacter(CharacterResponse character)
        {
            level = character.level;
            xp = character.xp;
            nextAttackAt = 0f;
            UpdateProgress();
        }

        public void ApplySnapshot(WorldSnapshotPayload snapshot)
        {
            if (snapshot?.self == null) return;
            if (!online)
            {
                online = true;
                local = false;
                battleHud.SetActive(true);
                modeText.text = "Mundo compartilhado";
                modeButton.gameObject.SetActive(true);
                modeButtonLabel.text = "SAIR DO ONLINE";
                ShowMessage("Explore a floresta. Selecione um inimigo e use F ou ATACAR.");
            }
            if (snapshot.self.position != null)
                confirmedPosition = new Vector3(snapshot.self.position.x, snapshot.self.position.y, snapshot.self.position.z);
            SetHealth(snapshot.self.hp, snapshot.self.maxHp);
            present.Clear();
            foreach (var state in snapshot.enemies ?? Array.Empty<SnapshotEnemyDto>())
            {
                if (state == null || string.IsNullOrEmpty(state.enemyId) || state.position == null) continue;
                present.Add(state.enemyId);
                if (!enemies.TryGetValue(state.enemyId, out var view) || view == null)
                    enemies[state.enemyId] = NetworkEnemyView.Create(state);
                else
                    view.ApplySnapshot(state);
            }
            removed.Clear();
            foreach (var pair in enemies)
                if (!present.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed)
            {
                if (enemies[id] != null) Destroy(enemies[id].gameObject);
                enemies.Remove(id);
            }
            if (selected == null || !selected.IsAlive || !present.Contains(selected.EnemyId))
                SelectNearest();
        }

        public void ApplyResolved(CombatResolvedPayload result)
        {
            if (!online || result == null) return;
            level = result.characterLevel;
            xp = result.characterXp;
            UpdateProgress();
            if (enemies.TryGetValue(result.targetId, out var view) && view != null)
                view.ShowHit(result.damage);
            var message = result.targetDied
                ? $"Inimigo derrotado · +{result.xpAwarded} XP"
                : $"{result.damage:0} de dano";
            if (result.leveledUp) message += $" · Nível {level}!";
            ShowMessage(message);
        }

        public void ApplyDamaged(CombatPlayerDamagedPayload result)
        {
            if (!online || result == null) return;
            SetHealth(result.hp, result.maxHp);
            ShowMessage(result.died ? "Você caiu. Aguarde o retorno a O Berço." : $"Você recebeu {result.damage:0} de dano.");
        }

        public void ShowMessage(string message)
        {
            messageText.text = message;
            messageBackdrop.SetActive(!string.IsNullOrEmpty(message));
            messageUntil = Time.unscaledTime + 4f;
        }

        private void SetHealth(float hp, float maxHp)
        {
            alive = hp > 0f;
            healthText.text = $"VIDA  {Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}";
            var fraction = maxHp > 0 ? Mathf.Clamp01(hp / maxHp) : 0f;
            healthFill.rectTransform.localScale = new Vector3(fraction, 1f, 1f);
            deathBackdrop.SetActive(!alive);
            deathText.text = "VOCÊ CAIU\nAguardando o retorno a O Berço...";
        }

        private void UpdateProgress()
        {
            if (progressText == null) return;
            progressText.text = level >= 10 ? $"Nível {level} · nível máximo" : $"Nível {level} · {xp} / {Mathf.CeilToInt(100f * Mathf.Pow(level, 1.5f))} XP";
        }

        private void Update()
        {
            if (!online) return;
            if (selected == null || !selected.IsAlive || HorizontalDistance(selected) > SelectionRange)
                SelectNearest();
            if (Input.GetKeyDown(KeyCode.Tab)) SelectNext();
            if (Input.GetMouseButtonDown(0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                var camera = Camera.main;
                if (camera != null && Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out var hit, 100f, ~0, QueryTriggerInteraction.Collide))
                {
                    var view = hit.collider.GetComponentInParent<NetworkEnemyView>();
                    if (view != null && view.IsAlive && HorizontalDistance(view) <= SelectionRange) Select(view);
                }
            }
            if (Input.GetKeyDown(KeyCode.F)) Attack();
            var hasTarget = selected != null && selected.IsAlive;
            var inAttackRange = hasTarget && HorizontalDistance(selected) <= AttackRangeUnits;
            attackButton.interactable = alive && inAttackRange && Time.unscaledTime >= nextAttackAt;
            targetButton.interactable = alive && enemies.Count > 0;
            targetText.text = hasTarget
                ? $"{selected.DisplayName} · {Mathf.CeilToInt(selected.Hp)} / {Mathf.CeilToInt(selected.MaxHp)} PV · {HorizontalDistance(selected):0.0} m"
                    + (inAttackRange ? "" : " · fora de alcance")
                : "Procure lobos e javalis na floresta";
            if (Time.unscaledTime > messageUntil && messageText.text != "")
            {
                messageText.text = "";
                messageBackdrop.SetActive(false);
            }
        }

        private void Attack()
        {
            if (!online || !alive || Time.unscaledTime < nextAttackAt) return;
            if (selected == null || !selected.IsAlive) SelectNearest();
            if (selected == null)
            {
                ShowMessage("Aproxime-se de um inimigo para selecionar um alvo.");
                return;
            }
            if (HorizontalDistance(selected) > AttackRangeUnits)
            {
                ShowMessage("Aproxime-se mais do alvo para atacar.");
                return;
            }
            // Debounce de entrada; o intervalo real de ataque (por agilidade) é validado pelo servidor.
            nextAttackAt = Time.unscaledTime + 0.2f;
            AttackRequested?.Invoke(selected.EnemyId);
        }

        private void SelectNearest()
        {
            NetworkEnemyView nearest = null;
            var best = SelectionRange;
            foreach (var view in enemies.Values)
            {
                if (view == null || !view.IsAlive) continue;
                var distance = HorizontalDistance(view);
                if (distance >= best) continue;
                nearest = view;
                best = distance;
            }
            Select(nearest);
        }

        private void SelectNext()
        {
            candidates.Clear();
            foreach (var view in enemies.Values)
                if (view != null && view.IsAlive && HorizontalDistance(view) <= SelectionRange) candidates.Add(view);
            candidates.Sort((a, b) => string.CompareOrdinal(a.EnemyId, b.EnemyId));
            if (candidates.Count == 0) { Select(null); return; }
            var index = candidates.IndexOf(selected);
            Select(candidates[(index + 1) % candidates.Count]);
        }

        private void Select(NetworkEnemyView view)
        {
            if (selected == view) return;
            if (selected != null) selected.SetSelected(false);
            selected = view;
            if (selected != null) selected.SetSelected(true);
        }

        private float HorizontalDistance(NetworkEnemyView view)
        {
            var offset = view.Position - confirmedPosition;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void ClearEnemies()
        {
            selected = null;
            foreach (var view in enemies.Values)
                if (view != null) Destroy(view.gameObject);
            enemies.Clear();
        }

        private void OnDestroy() => ClearEnemies();

        private void BuildHud()
        {
            var topRight = new Vector2(1f, 1f);
            var modeBackdrop = Backdrop(transform, topRight, new Vector2(-30f, -95f), new Vector2(580f, 35f));
            modeText = Label(modeBackdrop.transform, "Estado da Sessao", "", 20, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 35f), TextAnchor.MiddleRight);
            modeButton = MakeButton(transform, "Modo Online", "ENTRAR ONLINE", topRight, new Vector2(-30f, -28f), new Vector2(340f, 58f),
                () => { if (local) OnlineRequested?.Invoke(); else LocalRequested?.Invoke(); });
            modeButtonLabel = modeButton.GetComponentInChildren<Text>();

            battleHud = new GameObject("HUD de Combate", typeof(RectTransform));
            battleHud.transform.SetParent(transform, false);
            var full = (RectTransform)battleHud.transform;
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.offsetMin = full.offsetMax = Vector2.zero;
            var topLeft = new Vector2(0f, 1f);
            var healthBackground = Panel(battleHud.transform, "Fundo Vida", topLeft, new Vector2(28f, -125f), new Vector2(360f, 100f), new Color(0.035f, 0.06f, 0.075f, 0.93f));
            healthBackground.GetComponent<Image>().raycastTarget = true;
            var healthButton = healthBackground.AddComponent<Button>();
            healthButton.targetGraphic = healthBackground.GetComponent<Image>();
            healthButton.onClick.AddListener(() => AttributesToggleRequested?.Invoke());
            var bar = Panel(healthBackground.transform, "Vida", Vector2.zero, new Vector2(12f, 12f), new Vector2(336f, 10f), new Color(0.18f, 0.8f, 0.62f));
            healthFill = bar.GetComponent<Image>();
            healthText = Label(healthBackground.transform, "Valor Vida", "VIDA", 25, topLeft, new Vector2(12f, -5f), new Vector2(330f, 45f), TextAnchor.MiddleLeft);
            var attributesHint = Label(healthBackground.transform, "Dica Atributos", "▾ clique para ver atributos", 13, topLeft, new Vector2(12f, -62f), new Vector2(330f, 22f), TextAnchor.MiddleLeft);
            attributesHint.color = new Color(0.6f, 0.68f, 0.65f);
            var progressBackdrop = Backdrop(battleHud.transform, topLeft, new Vector2(28f, -235f), new Vector2(430f, 35f));
            progressText = Label(progressBackdrop.transform, "Progresso", "", 22, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 35f), TextAnchor.MiddleLeft);
            var targetBackdrop = Backdrop(battleHud.transform, new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(620f, 55f));
            targetText = Label(targetBackdrop.transform, "Alvo", "", 25, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 55f), TextAnchor.MiddleCenter);
            messageBackdrop = Backdrop(battleHud.transform, new Vector2(0.5f, 0f), new Vector2(0f, 155f), new Vector2(1000f, 70f));
            messageText = Label(messageBackdrop.transform, "Resultado", "", 25, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 70f), TextAnchor.MiddleCenter);
            messageBackdrop.SetActive(false);
            deathBackdrop = Panel(battleHud.transform, "Fundo Morte", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 160f), new Color(0.02f, 0.02f, 0.02f, 0.7f));
            deathText = Label(deathBackdrop.transform, "Morte", "", 34, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 130f), TextAnchor.MiddleCenter);
            deathText.color = new Color(1f, 0.72f, 0.55f);
            deathBackdrop.SetActive(false);
            attackButton = MakeButton(battleHud.transform, "Atacar", "ATACAR [F]", new Vector2(1f, 0f), new Vector2(-35f, 340f), new Vector2(235f, 85f), Attack);
            targetButton = MakeButton(battleHud.transform, "Trocar Alvo", "ALVO [TAB]", new Vector2(1f, 0f), new Vector2(-35f, 440f), new Vector2(235f, 58f), SelectNext);
            UpdateProgress();
        }

        // Fundo semi-transparente atrás de texto solto sobre a cena 3D (céu claro, terreno) —
        // sem isso o texto branco fica ilegível dependendo do fundo atrás dele.
        private static GameObject Backdrop(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var item = Panel(parent, "Fundo de Texto", anchor, position, size + new Vector2(24f, 10f), new Color(0.02f, 0.03f, 0.03f, 0.55f));
            return item;
        }

        private static RectTransform Rect(GameObject item, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static GameObject Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            Rect(item, parent, anchor, position, size);
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return item;
        }

        private static Text Label(Transform parent, string name, string value, int fontSize, Vector2 anchor, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            Rect(item, parent, anchor, position, size);
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.96f, 0.92f);
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string value, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var item = Panel(parent, name, anchor, position, size, new Color(0.09f, 0.27f, 0.28f, 0.96f));
            item.GetComponent<Image>().raycastTarget = true;
            var button = item.AddComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            button.onClick.AddListener(action);
            Label(item.transform, "Texto", value, 23, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
