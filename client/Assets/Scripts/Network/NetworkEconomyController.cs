using System;
using System.Collections.Generic;
using Espectro.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Network
{
    // Apresentação e intenções do Corte 3 (economia): nenhum item, moeda ou XP é calculado
    // aqui — tudo vem confirmado do servidor (ver server/README.md). Espelha o mesmo padrão de
    // NetworkCombatController: HUD construída em código, presentation-only.
    //
    // Layout: cabeçalho (moedas/equipamento/poção) sempre visível; a seção do Comerciante
    // (vender/comprar poção) e a da Forja (fundir) só aparecem perto do respectivo NPC — GDD §12
    // ("vender ao comerciante") e §11 ("o ferreiro libera a forja"). Como os NPCs "Ferreiro" e
    // "Comerciante" ainda não foram posicionados no mundo pelo Codex, a checagem de proximidade
    // tem um fallback seguro: se nenhum WorldInteractable com aquele nome existir na cena, a
    // seção fica sempre visível (evita esconder a função só porque o NPC não foi colocado ainda).
    public sealed class NetworkEconomyController : MonoBehaviour
    {
        public event Action<string> MineRequested;
        public event Action<string> CraftRequested;
        public event Action<string, int> SellRequested;
        public event Action<string, string> EquipRequested;
        public event Action<string, int> BuyRequested;
        public event Action<string> UseItemRequested;
        public event Action CraftCancelRequested;

        private const string PotionCode = "pocao";
        private const int PotionBuyPrice = 15; // Espelha buyPrices.pocao em economy.constants.ts (só exibição).
        private const string ForgeNpcName = "Ferreiro";
        private const string MerchantNpcName = "Comerciante";
        private const float NpcInteractionRange = 4.5f;

        private const float HeaderBottomY = -166f;
        private const float SectionGapY = 10f;
        private const float MerchantSectionHeight = 300f;
        private const float ForgeSectionHeight = 132f;
        private const float HintSectionHeight = 70f;
        private const float BackgroundWidth = 430f;
        private const float BottomPadding = 18f;

        private static readonly (string Code, string Label)[] SellableItems =
        {
            ("minerio_ferro", "Minério de Ferro"), ("minerio_cobre", "Minério de Cobre"),
            ("lingote_ferro", "Lingote de Ferro"), ("lingote_cobre", "Lingote de Cobre"),
            ("couro", "Couro"),
        };
        private static readonly Dictionary<string, string> ResourceLabels = new()
        {
            ["ferro"] = "Ferro", ["cobre"] = "Cobre",
        };
        private static readonly Dictionary<string, string> ItemDisplayNames = new()
        {
            ["minerio_ferro"] = "Minério de Ferro", ["minerio_cobre"] = "Minério de Cobre",
            ["lingote_ferro"] = "Lingote de Ferro", ["lingote_cobre"] = "Lingote de Cobre",
            ["couro"] = "Couro", ["espada_simples"] = "Espada Simples", ["picareta_simples"] = "Picareta Simples",
            ["pocao"] = "Poção",
        };

        private readonly Dictionary<string, GameObject> nodeMarkers = new();
        private readonly Dictionary<string, SnapshotResourceNodeDto> nodeState = new();
        private readonly List<string> nodeScratch = new();
        private readonly Dictionary<string, int> inventory = new();
        private readonly Dictionary<string, Text> sellRows = new();

        private PrototypePlayerController player;
        private GameObject panel;
        private GameObject background;
        private Text coinText;
        private Text equipmentText;
        private Button toolToggleButton;
        private Text toolToggleLabel;
        private Text minePromptText;
        private Text messageText;
        private string equippedTool; // null = nada equipado.
        private const float MiningRangeUnits = 2.75f; // Espelha MINING_RANGE_UNITS em economy.constants.ts.
        private string nearestAvailableNodeId;
        private float messageUntil;

        private Text potionText;
        private Button potionBuyButton;
        private Button potionUseButton;

        private GameObject merchantSection;
        private GameObject forgeSection;
        private Text contextHintText;
        private bool merchantVisible;
        private bool forgeVisible = true; // valor inicial diferente força o primeiro Relayout().
        private bool contextInitialized;

        private GameObject miningProgressRoot;
        private Image miningProgressFill;
        private float miningProgressStart;
        private float miningProgressDuration;

        private GameObject craftProgressRoot;
        private Image craftProgressFill;
        private Text craftProgressLabel;
        private float craftProgressStart;
        private float craftProgressDuration;
        private string craftInProgressRecipe;

        public static NetworkEconomyController Create(PrototypePlayerController player)
        {
            var root = new GameObject("Economia Online - Interface", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 505;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var controller = root.AddComponent<NetworkEconomyController>();
            controller.player = player;
            controller.BuildHud();
            controller.SetActive(false);
            return controller;
        }

        public void SetActive(bool active)
        {
            panel.SetActive(active);
            if (!active)
            {
                ClearNodes();
                miningProgressRoot.SetActive(false);
                craftProgressRoot.SetActive(false);
                craftInProgressRecipe = null;
            }
            else
            {
                contextInitialized = false; // Força reavaliar proximidade ao reentrar online.
            }
        }

        public void ApplyEconomy(EconomySnapshotPayload economy)
        {
            if (economy == null) return;
            coinText.text = $"MOEDAS: {economy.coinBalance}";
            inventory.Clear();
            foreach (var item in economy.inventory ?? Array.Empty<InventoryItemDto>())
                inventory[item.itemCode] = item.quantity;
            equippedTool = economy.equipment?.tool;
            RefreshInventoryRows();
            RefreshEquipmentDisplay(economy.equipment);
            RefreshPotionDisplay();
        }

        public void ApplyBuyResult(BuyResultPayload result)
        {
            if (result == null) return;
            ShowMessage($"Comprado: {result.quantityBought}x {DisplayName(result.itemCode)} por {result.coinsSpent} moedas");
            ApplyEconomy(result.economy);
        }

        public void ApplyUseItemResult(UseItemResultPayload result)
        {
            if (result == null) return;
            ShowMessage($"HP: {result.hp}/{result.maxHp}");
            ApplyEconomy(result.economy);
        }

        public void ApplyMineStarted(MineStartedPayload payload)
        {
            if (payload == null) return;
            miningProgressStart = Time.unscaledTime;
            miningProgressDuration = Mathf.Max(0.01f, payload.durationMs / 1000f);
            miningProgressRoot.SetActive(true);
            miningProgressFill.fillAmount = 0f;
        }

        public void ApplyMineCancelled(MineCancelledPayload result)
        {
            miningProgressRoot.SetActive(false);
            if (result != null) ShowMessage("Mineração cancelada");
        }

        public void ApplyCraftStarted(CraftStartedPayload payload)
        {
            if (payload == null) return;
            craftInProgressRecipe = payload.recipeCode;
            craftProgressStart = Time.unscaledTime;
            craftProgressDuration = Mathf.Max(0.01f, payload.durationMs / 1000f);
            craftProgressLabel.text = $"Fundindo {DisplayName(RecipeOutput(payload.recipeCode))}...";
            craftProgressRoot.SetActive(true);
            craftProgressFill.fillAmount = 0f;
        }

        public void ApplyCraftCancelled(CraftCancelledPayload result)
        {
            craftInProgressRecipe = null;
            craftProgressRoot.SetActive(false);
            if (result != null) ShowMessage($"Fundição cancelada: devolvido {result.refundedQuantity}x minério");
        }

        public void ApplyEquipResult(EquipResultPayload result)
        {
            if (result == null) return;
            ApplyEconomy(result.economy);
        }

        public void ApplyMineResult(MineResultPayload result)
        {
            if (result == null) return;
            miningProgressRoot.SetActive(false);
            var label = ResourceLabels.TryGetValue(result.resourceCode, out var name) ? name : result.resourceCode;
            ShowMessage($"+{result.quantityGained} minério de {label}");
            ApplyEconomy(result.economy);
        }

        public void ApplyCraftResult(CraftResultPayload result)
        {
            if (result == null) return;
            craftInProgressRecipe = null;
            craftProgressRoot.SetActive(false);
            ShowMessage($"+{result.producedQuantity} {DisplayName(result.producedItemCode)}");
            ApplyEconomy(result.economy);
        }

        public void ApplySellResult(SellResultPayload result)
        {
            if (result == null) return;
            ShowMessage($"Vendido: {result.quantitySold}x {DisplayName(result.itemCode)} por {result.coinsEarned} moedas");
            ApplyEconomy(result.economy);
        }

        public void ApplyResourceNodes(SnapshotResourceNodeDto[] nodes)
        {
            nodeScratch.Clear();
            foreach (var node in nodes ?? Array.Empty<SnapshotResourceNodeDto>())
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId) || node.position == null) continue;
                nodeScratch.Add(node.nodeId);
                nodeState[node.nodeId] = node;
                if (!nodeMarkers.TryGetValue(node.nodeId, out var marker) || marker == null)
                    nodeMarkers[node.nodeId] = CreateMarker(node);
                else
                    UpdateMarker(marker, node);
            }

            var toRemove = new List<string>();
            foreach (var id in nodeMarkers.Keys)
                if (!nodeScratch.Contains(id)) toRemove.Add(id);
            foreach (var id in toRemove)
            {
                if (nodeMarkers[id] != null) Destroy(nodeMarkers[id]);
                nodeMarkers.Remove(id);
                nodeState.Remove(id);
            }
        }

        public void ShowMessage(string message)
        {
            messageText.text = message;
            messageUntil = Time.unscaledTime + 4f;
        }

        private void Update()
        {
            if (!panel.activeSelf || player == null) return;

            nearestAvailableNodeId = null;
            var bestDistance = MiningRangeUnits;
            foreach (var pair in nodeState)
            {
                if (!pair.Value.available) continue;
                var nodePosition = new Vector3(pair.Value.position.x, pair.Value.position.y, pair.Value.position.z);
                var distance = Vector3.Distance(player.transform.position, nodePosition);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                nearestAvailableNodeId = pair.Key;
            }

            if (nearestAvailableNodeId != null)
            {
                var resourceCode = nodeState[nearestAvailableNodeId].resourceCode;
                var label = ResourceLabels.TryGetValue(resourceCode, out var name) ? name : resourceCode;
                minePromptText.gameObject.SetActive(true);
                minePromptText.text = $"Pressione M para minerar ({label})";
                if (Input.GetKeyDown(KeyCode.M)) MineRequested?.Invoke(nearestAvailableNodeId);
            }
            else
            {
                minePromptText.gameObject.SetActive(false);
            }

            if (Time.unscaledTime > messageUntil) messageText.text = "";

            if (miningProgressRoot.activeSelf)
                miningProgressFill.fillAmount = Mathf.Clamp01((Time.unscaledTime - miningProgressStart) / miningProgressDuration);
            if (craftProgressRoot.activeSelf)
                craftProgressFill.fillAmount = Mathf.Clamp01((Time.unscaledTime - craftProgressStart) / craftProgressDuration);

            var nearMerchant = IsNearOrAbsent(MerchantNpcName);
            var nearForge = IsNearOrAbsent(ForgeNpcName);
            if (!contextInitialized || nearMerchant != merchantVisible || nearForge != forgeVisible)
            {
                contextInitialized = true;
                RelayoutContext(nearMerchant, nearForge);
            }
        }

        // Perto do NPC (dentro do alcance) OU o NPC ainda não existe na cena — ver nota no topo
        // do arquivo sobre o fallback enquanto o mundo (Codex) não posiciona Ferreiro/Comerciante.
        private bool IsNearOrAbsent(string npcDisplayName)
        {
            var exists = false;
            var rangeSq = NpcInteractionRange * NpcInteractionRange;
            foreach (var interactable in WorldInteractable.Active)
            {
                if (interactable == null || interactable.DisplayName != npcDisplayName) continue;
                exists = true;
                if ((interactable.transform.position - player.transform.position).sqrMagnitude <= rangeSq) return true;
            }
            return !exists;
        }

        private void RelayoutContext(bool nearMerchant, bool nearForge)
        {
            merchantVisible = nearMerchant;
            forgeVisible = nearForge;
            merchantSection.SetActive(nearMerchant);
            forgeSection.SetActive(nearForge);
            var showHint = !nearMerchant && !nearForge;
            contextHintText.gameObject.SetActive(showHint);

            var forgeY = HeaderBottomY - (nearMerchant ? MerchantSectionHeight + SectionGapY : 0f);
            ((RectTransform)forgeSection.transform).anchoredPosition = new Vector2(0f, forgeY);

            var height = -HeaderBottomY
                + (nearMerchant ? MerchantSectionHeight + SectionGapY : 0f)
                + (nearForge ? ForgeSectionHeight + SectionGapY : 0f)
                + (showHint ? HintSectionHeight : 0f)
                + BottomPadding;
            ((RectTransform)background.transform).sizeDelta = new Vector2(BackgroundWidth, height);
        }

        private void RefreshInventoryRows()
        {
            foreach (var (code, label) in SellableItems)
            {
                var quantity = inventory.TryGetValue(code, out var value) ? value : 0;
                sellRows[code].text = $"{label}: {quantity}";
                var button = sellRows[code].transform.parent.Find("Vender")?.GetComponent<Button>();
                if (button != null) button.interactable = quantity > 0;
            }
        }

        private void RefreshEquipmentDisplay(EquipmentDto equipment)
        {
            var weapon = string.IsNullOrEmpty(equipment?.mainHand) ? "nenhuma" : DisplayName(equipment.mainHand);
            var tool = string.IsNullOrEmpty(equipment?.tool) ? "nenhuma" : DisplayName(equipment.tool);
            equipmentText.text = $"Arma: {weapon}  ·  Ferramenta: {tool}";

            var hasPickaxeEquipped = equipment != null && equipment.tool == "picareta_simples";
            toolToggleLabel.text = hasPickaxeEquipped ? "GUARDAR PICARETA" : "EQUIPAR PICARETA";
            toolToggleButton.interactable = hasPickaxeEquipped || (inventory.TryGetValue("picareta_simples", out var owned) && owned > 0);
        }

        private static string DisplayName(string itemCode) =>
            ItemDisplayNames.TryGetValue(itemCode, out var label) ? label : itemCode;

        private static string RecipeOutput(string recipeCode) => recipeCode; // recipeCode == itemCode produzido (lingote_ferro/lingote_cobre).

        private void RefreshPotionDisplay()
        {
            var owned = inventory.TryGetValue(PotionCode, out var quantity) ? quantity : 0;
            potionText.text = $"Poção: {owned}";
            potionUseButton.interactable = owned > 0;
        }

        private GameObject CreateMarker(SnapshotResourceNodeDto node)
        {
            var isIron = node.resourceCode == "ferro";
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = $"Veio Online - {node.nodeId}";
            marker.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            Destroy(marker.GetComponent<Collider>());
            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = isIron ? new Color(0.55f, 0.34f, 0.24f) : new Color(0.72f, 0.44f, 0.22f),
            };
            UpdateMarker(marker, node);
            return marker;
        }

        private static void UpdateMarker(GameObject marker, SnapshotResourceNodeDto node)
        {
            marker.transform.position = new Vector3(node.position.x, node.position.y + 0.4f, node.position.z);
            marker.SetActive(node.available);
        }

        private void ClearNodes()
        {
            foreach (var marker in nodeMarkers.Values)
                if (marker != null) Destroy(marker);
            nodeMarkers.Clear();
            nodeState.Clear();
        }

        private void OnDestroy() => ClearNodes();

        private void BuildHud()
        {
            panel = new GameObject("Painel de Economia", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var full = (RectTransform)panel.transform;
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.offsetMin = full.offsetMax = Vector2.zero;

            var topRight = new Vector2(1f, 1f);
            background = Panel(panel.transform, "Fundo Economia", topRight, new Vector2(-30f, -170f), new Vector2(BackgroundWidth, 300f), new Color(0.045f, 0.045f, 0.04f, 0.92f));

            // Cabeçalho (sempre visível): moedas, equipamento, ferramenta e poção rápida.
            coinText = Label(background.transform, "Moedas", "MOEDAS: 0", 26, topRight, new Vector2(-16f, -14f), new Vector2(400f, 40f), TextAnchor.MiddleRight);
            coinText.color = new Color(0.95f, 0.82f, 0.45f);
            coinText.fontStyle = FontStyle.Bold;
            equipmentText = Label(background.transform, "Equipamento", "Arma: nenhuma  ·  Ferramenta: nenhuma", 16, topRight, new Vector2(-16f, -46f), new Vector2(400f, 26f), TextAnchor.MiddleRight);
            equipmentText.color = new Color(0.78f, 0.83f, 0.76f);
            toolToggleButton = MakeButton(background.transform, "Alternar Picareta", "EQUIPAR PICARETA", topRight, new Vector2(-16f, -80f), new Vector2(400f, 40f),
                () => EquipRequested?.Invoke("tool", equippedTool == "picareta_simples" ? null : "picareta_simples"));
            toolToggleLabel = toolToggleButton.GetComponentInChildren<Text>();

            var potionRow = new GameObject("Linha Pocao Cabecalho", typeof(RectTransform));
            potionRow.transform.SetParent(background.transform, false);
            var potionRowRect = (RectTransform)potionRow.transform;
            potionRowRect.anchorMin = potionRowRect.anchorMax = topRight;
            potionRowRect.pivot = topRight;
            potionRowRect.anchoredPosition = new Vector2(-16f, -124f);
            potionRowRect.sizeDelta = new Vector2(400f, 36f);
            potionText = Label(potionRow.transform, "Texto Pocao", "Poção: 0", 18, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220f, 34f), TextAnchor.MiddleLeft);
            potionUseButton = MakeButton(potionRow.transform, "Usar Pocao", "USAR POÇÃO", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(150f, 34f),
                () => UseItemRequested?.Invoke(PotionCode));
            potionUseButton.interactable = false;

            Divider(background.transform, topRight, new Vector2(-16f, -156f), 400f);

            BuildMerchantSection(topRight);
            BuildForgeSection(topRight);

            contextHintText = Label(background.transform, "Dica de Contexto", "Aproxime-se do Comerciante para vender itens ou comprar poções,\nou do Ferreiro para fundir metais.", 15,
                topRight, new Vector2(-16f, HeaderBottomY), new Vector2(400f, 60f), TextAnchor.UpperRight);
            contextHintText.color = new Color(0.62f, 0.67f, 0.64f);

            miningProgressRoot = Panel(panel.transform, "Barra de Mineracao", new Vector2(0.5f, 0.4f), Vector2.zero, new Vector2(400f, 26f), new Color(0.08f, 0.08f, 0.07f, 0.85f));
            miningProgressFill = BuildFill(miningProgressRoot.transform, new Color(0.6f, 0.44f, 0.2f));
            Label(miningProgressRoot.transform, "Texto Mineracao", "MINERANDO...", 14, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 26f), TextAnchor.MiddleCenter);
            miningProgressRoot.SetActive(false);

            craftProgressRoot = Panel(panel.transform, "Barra de Fundicao", new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(400f, 26f), new Color(0.08f, 0.08f, 0.07f, 0.85f));
            craftProgressFill = BuildFill(craftProgressRoot.transform, new Color(0.55f, 0.3f, 0.15f));
            craftProgressLabel = Label(craftProgressRoot.transform, "Texto Fundicao", "FUNDINDO...", 14, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 26f), TextAnchor.MiddleCenter);
            MakeButton(craftProgressRoot.transform, "Cancelar Fundicao", "X", new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(30f, 22f),
                () => CraftCancelRequested?.Invoke());
            craftProgressRoot.SetActive(false);

            minePromptText = Label(panel.transform, "Dica de Mineracao", "", 26, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(700f, 50f), TextAnchor.MiddleCenter);
            minePromptText.gameObject.SetActive(false);
            messageText = Label(panel.transform, "Mensagem de Economia", "", 24, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(800f, 45f), TextAnchor.MiddleCenter);

            RelayoutContext(true, true);
        }

        private void BuildMerchantSection(Vector2 topRight)
        {
            merchantSection = new GameObject("Secao Comerciante", typeof(RectTransform));
            Rect(merchantSection, background.transform, topRight, new Vector2(0f, HeaderBottomY - SectionGapY), new Vector2(BackgroundWidth, MerchantSectionHeight));

            var title = Label(merchantSection.transform, "Titulo Comerciante", "COMERCIANTE", 17, topRight, new Vector2(-16f, -12f), new Vector2(300f, 24f), TextAnchor.MiddleLeft);
            title.color = new Color(0.85f, 0.7f, 0.4f);
            title.fontStyle = FontStyle.Bold;

            var rowY = -46f;
            foreach (var (code, label) in SellableItems)
            {
                var row = new GameObject($"Linha {code}", typeof(RectTransform));
                row.transform.SetParent(merchantSection.transform, false);
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = rowRect.anchorMax = topRight;
                rowRect.pivot = topRight;
                rowRect.anchoredPosition = new Vector2(-16f, rowY);
                rowRect.sizeDelta = new Vector2(400f, 40f);

                sellRows[code] = Label(row.transform, "Texto", $"{label}: 0", 18, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(260f, 38f), TextAnchor.MiddleLeft);
                var itemCode = code;
                MakeButton(row.transform, "Vender", "VENDER", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 36f),
                    () => { if (inventory.TryGetValue(itemCode, out var quantity) && quantity > 0) SellRequested?.Invoke(itemCode, quantity); });
                rowY -= 42f;
            }

            var buyRow = new GameObject("Linha Comprar Pocao", typeof(RectTransform));
            buyRow.transform.SetParent(merchantSection.transform, false);
            var buyRowRect = (RectTransform)buyRow.transform;
            buyRowRect.anchorMin = buyRowRect.anchorMax = topRight;
            buyRowRect.pivot = topRight;
            buyRowRect.anchoredPosition = new Vector2(-16f, rowY - 6f);
            buyRowRect.sizeDelta = new Vector2(400f, 40f);
            Label(buyRow.transform, "Texto Comprar", "Poção", 18, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(150f, 38f), TextAnchor.MiddleLeft);
            potionBuyButton = MakeButton(buyRow.transform, "Comprar Pocao", $"COMPRAR ({PotionBuyPrice})", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(180f, 36f),
                () => BuyRequested?.Invoke(PotionCode, 1));
        }

        private void BuildForgeSection(Vector2 topRight)
        {
            forgeSection = new GameObject("Secao Forja", typeof(RectTransform));
            Rect(forgeSection, background.transform, topRight, new Vector2(0f, HeaderBottomY - SectionGapY), new Vector2(BackgroundWidth, ForgeSectionHeight));

            var title = Label(forgeSection.transform, "Titulo Forja", "FORJA", 17, topRight, new Vector2(-16f, -12f), new Vector2(300f, 24f), TextAnchor.MiddleLeft);
            title.color = new Color(0.85f, 0.7f, 0.4f);
            title.fontStyle = FontStyle.Bold;

            MakeButton(forgeSection.transform, "Fundir Ferro", "FUNDIR FERRO", topRight, new Vector2(-16f, -46f), new Vector2(400f, 40f),
                () => CraftRequested?.Invoke("lingote_ferro"));
            MakeButton(forgeSection.transform, "Fundir Cobre", "FUNDIR COBRE", topRight, new Vector2(-16f, -90f), new Vector2(400f, 40f),
                () => CraftRequested?.Invoke("lingote_cobre"));
        }

        private static Image BuildFill(Transform parent, Color color)
        {
            var fillRoot = new GameObject("Preenchimento", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)fillRoot.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
            var image = fillRoot.GetComponent<Image>();
            image.color = color;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = 0f;
            image.raycastTarget = false;
            return image;
        }

        private static void Divider(Transform parent, Vector2 anchor, Vector2 position, float width)
        {
            var line = Panel(parent, "Divisor", anchor, position, new Vector2(width, 1f), new Color(1f, 1f, 1f, 0.08f));
            line.GetComponent<Image>().raycastTarget = false;
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
            var item = Panel(parent, name, anchor, position, size, new Color(0.27f, 0.2f, 0.07f, 0.96f));
            item.GetComponent<Image>().raycastTarget = true;
            var button = item.AddComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            button.onClick.AddListener(action);
            Label(item.transform, "Texto", value, 18, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
