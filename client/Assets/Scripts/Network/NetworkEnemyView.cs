using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Network
{
    // Presentation only: health and life state always come from the server snapshot.
    public sealed class NetworkEnemyView : MonoBehaviour
    {
        private const float Smoothing = 12f;
        private const float HitDuration = 0.7f;
        private const float DeathDuration = 0.6f;
        private static readonly Color SelectionColor = new Color(1f, 0.79f, 0.25f);
        private readonly List<Material> ownedMaterials = new();
        private readonly List<Mesh> ownedMeshes = new();

        private Vector3 targetPosition;
        private Quaternion targetRotation = Quaternion.identity;
        private Transform body;
        private Transform billboard;
        private Collider selectionCollider;
        private LineRenderer selectionRing;
        private Text nameLabel;
        private Text healthLabel;
        private Text hitLabel;
        private RectTransform healthFill;
        private Material bodyMaterial;
        private Color bodyColor;
        private float hitRemaining;
        private bool selected;
        private bool hasSnapshot;
        private bool dying;
        private float deathTimer;
        private float idlePhase;

        public string EnemyId { get; private set; }
        public bool IsAlive { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public string DisplayName { get; private set; }
        public Vector3 Position => transform.position;

        public static NetworkEnemyView Create(SnapshotEnemyDto snapshot)
        {
            if (snapshot == null || snapshot.position == null)
                throw new ArgumentException("An enemy requires a snapshot with a position.", nameof(snapshot));

            var root = new GameObject($"Inimigo Online - {snapshot.enemyId}");
            var view = root.AddComponent<NetworkEnemyView>();
            view.EnemyId = snapshot.enemyId;
            bool boar = snapshot.definitionCode == "javali";
            view.DisplayName = boar ? "Javali" : snapshot.definitionCode == "lobo" ? "Lobo" : "Inimigo";
            view.CreateBody(boar);
            view.CreateBillboard();
            view.CreateSelectionRing(boar ? 1f : 0.9f);
            view.idlePhase = UnityEngine.Random.value * 10f; // dessincroniza a respiração entre animais.

            // Explicit QueryTriggerInteraction.Collide allows mouse selection. This collider
            // never blocks the local CharacterController or alters authoritative movement.
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.8f, 0.05f);
            collider.size = new Vector3(boar ? 1.1f : 0.8f, 1.5f, 2.1f);
            collider.isTrigger = true;
            view.selectionCollider = collider;
            view.ApplySnapshot(snapshot);
            return view;
        }

        public void ApplySnapshot(SnapshotEnemyDto snapshot)
        {
            if (snapshot == null || snapshot.position == null || snapshot.enemyId != EnemyId)
                return;

            var nextPosition = new Vector3(snapshot.position.x, snapshot.position.y, snapshot.position.z);
            var movement = nextPosition - targetPosition;
            movement.y = 0f;
            if (hasSnapshot && movement.sqrMagnitude > 0.0025f)
                targetRotation = Quaternion.LookRotation(movement, Vector3.up);

            bool wasAliveBefore = hasSnapshot && IsAlive;
            bool respawned = hasSnapshot && !IsAlive && snapshot.alive;
            targetPosition = nextPosition;
            // Respawn and the first snapshot should not slide in from an unrelated position.
            if (!hasSnapshot || respawned || (transform.position - targetPosition).sqrMagnitude > 64f)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }

            Hp = snapshot.hp;
            MaxHp = snapshot.maxHp;
            IsAlive = snapshot.alive;
            hasSnapshot = true;
            billboard.gameObject.SetActive(IsAlive);
            selectionCollider.enabled = IsAlive;

            // Tomba antes de sumir, em vez de desaparecer na hora — dá peso ao golpe final.
            // Ver Update() pra animação; o corpo continua ativo durante ela.
            if (respawned)
            {
                dying = false;
                body.gameObject.SetActive(true);
                body.localRotation = Quaternion.identity;
                body.localPosition = Vector3.zero;
                body.localScale = Vector3.one;
            }
            else if (wasAliveBefore && !IsAlive)
            {
                dying = true;
                deathTimer = DeathDuration;
            }
            else if (!IsAlive && !dying)
            {
                body.gameObject.SetActive(false);
            }

            if (!IsAlive || respawned)
            {
                selected = false;
                hitRemaining = 0f;
                bodyMaterial.color = bodyColor;
                hitLabel.gameObject.SetActive(false);
            }

            float fraction = MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f;
            healthFill.anchorMax = new Vector2(fraction, 1f);
            healthLabel.text = $"{Mathf.CeilToInt(Mathf.Max(0f, Hp))} / {Mathf.CeilToInt(Mathf.Max(0f, MaxHp))}";
            RefreshSelection();
        }

        public void SetSelected(bool value)
        {
            selected = value && IsAlive;
            RefreshSelection();
        }

        public void ShowHit(float damage)
        {
            if (!IsAlive || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage))
                return;

            hitRemaining = HitDuration;
            hitLabel.text = $"−{Mathf.CeilToInt(damage)}";
            hitLabel.gameObject.SetActive(true);
            // Deliberately does not subtract health or predict a kill.
        }

        private void Update()
        {
            if (!IsAlive)
            {
                if (dying)
                {
                    // Encolhe e afunda, sem rotação: o corpo é montado de várias partes fixas
                    // (pernas, orelhas etc. em posições locais separadas, não um mesh único) —
                    // girar o conjunto inteiro faz as pernas atravessarem o torso e parece que o
                    // animal "se dobra"/gruda em vez de tombar. Encolher é seguro pra qualquer
                    // geometria composta.
                    deathTimer = Mathf.Max(0f, deathTimer - Time.deltaTime);
                    var t = 1f - deathTimer / DeathDuration;
                    var eased = t * t; // acelera pro fim.
                    body.localScale = Vector3.one * (1f - 0.92f * eased);
                    body.localPosition = new Vector3(0f, -0.3f * eased, 0f);
                    if (deathTimer <= 0f)
                    {
                        dying = false;
                        body.gameObject.SetActive(false);
                    }
                }
                return;
            }

            float blend = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);

            // Respiração sutil — cosmético, aplicado só no corpo (não no transform que carrega a
            // posição de rede), pra dar vida ao animal parado sem interferir na sincronização.
            var bob = Mathf.Sin((Time.time + idlePhase) * 1.6f) * 0.035f;
            body.localPosition = new Vector3(0f, bob, 0f);

            if (selectionRing.enabled)
            {
                var pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
                selectionRing.transform.localScale = Vector3.one * pulse;
            }

            if (hitRemaining > 0f)
            {
                hitRemaining = Mathf.Max(0f, hitRemaining - Time.deltaTime);
                float progress = 1f - hitRemaining / HitDuration;
                hitLabel.rectTransform.anchoredPosition = new Vector2(0f, 62f + 44f * progress);
                // Cresce rápido ao aparecer e volta ao tamanho normal — dá mais peso ao número.
                hitLabel.rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(progress * Mathf.PI));
                hitLabel.color = new Color(1f, 0.87f, 0.43f, 1f - progress);
                bodyMaterial.color = Color.Lerp(bodyColor, Color.white, Mathf.Clamp01((hitRemaining - 0.5f) * 4f));
                if (hitRemaining <= 0f)
                    hitLabel.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (IsAlive && Camera.main != null)
                billboard.rotation = Camera.main.transform.rotation;
        }

        private void RefreshSelection()
        {
            selectionRing.enabled = selected && IsAlive;
            nameLabel.color = selected ? SelectionColor : Color.white;
            nameLabel.text = selected ? $"> {DisplayName} <" : DisplayName;
        }

        private void CreateBody(bool boar)
        {
            body = new GameObject("Animal").transform;
            body.SetParent(transform, false);
            bodyColor = boar ? new Color(0.42f, 0.25f, 0.15f) : new Color(0.39f, 0.46f, 0.52f);
            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            bodyMaterial = CreateMaterial(shader, bodyColor);
            var darkMaterial = CreateMaterial(shader, new Color(0.12f, 0.13f, 0.15f));
            var paleMaterial = CreateMaterial(shader, new Color(0.88f, 0.83f, 0.68f));
            var fur = new List<CombineInstance>();
            var dark = new List<CombineInstance>();
            var pale = new List<CombineInstance>();

            // Borrow Unity's built-in meshes, then combine all pieces by material. Each
            // creature has three renderers rather than a renderer/collider for every limb.
            var sphereSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereSource.SetActive(false);
            var cubeSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeSource.SetActive(false);
            var sphere = sphereSource.GetComponent<MeshFilter>().sharedMesh;
            var cube = cubeSource.GetComponent<MeshFilter>().sharedMesh;

            AddPart(fur, sphere, new Vector3(0f, 0.85f, -0.1f),
                boar ? new Vector3(1.05f, 1.15f, 1.7f) : new Vector3(0.68f, 0.82f, 1.5f));
            AddPart(fur, sphere, new Vector3(0f, boar ? 0.85f : 1.18f, 0.72f),
                boar ? new Vector3(0.7f, 0.76f, 0.82f) : new Vector3(0.53f, 0.59f, 0.65f));
            AddPart(fur, sphere, new Vector3(0f, boar ? 0.7f : 1.03f, 1.02f),
                new Vector3(boar ? 0.45f : 0.3f, 0.32f, 0.58f));
            AddPart(dark, sphere, new Vector3(0f, boar ? 0.7f : 1.03f, 1.28f),
                new Vector3(boar ? 0.39f : 0.23f, 0.23f, 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (boar ? 0.34f : 0.23f);
                AddPart(fur, cube, new Vector3(x, 0.34f, 0.39f), new Vector3(0.2f, 0.64f, 0.24f));
                AddPart(fur, cube, new Vector3(x, 0.34f, -0.65f), new Vector3(0.2f, 0.64f, 0.24f));
                AddPart(dark, cube, new Vector3(x, 0.07f, 0.43f), new Vector3(0.23f, 0.14f, 0.33f));
                AddPart(dark, cube, new Vector3(x, 0.07f, -0.61f), new Vector3(0.23f, 0.14f, 0.33f));
                AddPart(dark, sphere, new Vector3(side * (boar ? 0.3f : 0.23f), boar ? 1.02f : 1.29f, 0.94f),
                    Vector3.one * 0.105f);
                // Tilted narrow ears keep the wolf silhouette distinct from the squat boar.
                AddPart(fur, cube, new Vector3(side * 0.21f, boar ? 1.2f : 1.53f, 0.64f),
                    new Vector3(0.17f, boar ? 0.24f : 0.4f, 0.18f), new Vector3(0f, 0f, side * -18f));
                if (boar)
                    AddPart(pale, cube, new Vector3(side * 0.3f, 0.81f, 1.12f),
                        new Vector3(0.1f, 0.38f, 0.11f), new Vector3(-20f, 0f, side * -20f));
            }

            if (boar)
            {
                AddPart(dark, cube, new Vector3(0f, 1.4f, -0.15f), new Vector3(0.16f, 0.16f, 1.05f));
                AddPart(fur, cube, new Vector3(0f, 0.92f, -1f), new Vector3(0.12f, 0.12f, 0.35f), new Vector3(-25f, 0f, 0f));
            }
            else
            {
                AddPart(pale, sphere, new Vector3(0f, 0.98f, 0.47f), new Vector3(0.49f, 0.56f, 0.4f));
                AddPart(fur, sphere, new Vector3(0f, 0.83f, -1.03f), new Vector3(0.27f, 0.3f, 0.85f), new Vector3(-28f, 0f, 0f));
            }

            CreateCombinedRenderer("Pelagem", fur, bodyMaterial);
            CreateCombinedRenderer("Detalhes", dark, darkMaterial);
            CreateCombinedRenderer("Presas e peito", pale, paleMaterial);
            Destroy(sphereSource);
            Destroy(cubeSource);
        }

        private static void AddPart(List<CombineInstance> parts, Mesh mesh, Vector3 position, Vector3 scale, Vector3 rotation = default)
        {
            parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(rotation), scale) });
        }

        private void CreateCombinedRenderer(string objectName, List<CombineInstance> parts, Material material)
        {
            var mesh = new Mesh { name = $"{DisplayName} - {objectName}" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            ownedMeshes.Add(mesh);
            var part = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(body, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { color = color };
            ownedMaterials.Add(material);
            return material;
        }

        private void CreateSelectionRing(float radius)
        {
            var ring = new GameObject("Alvo Selecionado");
            ring.transform.SetParent(transform, false);
            selectionRing = ring.AddComponent<LineRenderer>();
            selectionRing.useWorldSpace = false;
            selectionRing.loop = true;
            selectionRing.widthMultiplier = 0.055f;
            selectionRing.positionCount = 32;
            selectionRing.sharedMaterial = CreateMaterial(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"), Color.white);
            selectionRing.startColor = SelectionColor;
            selectionRing.endColor = SelectionColor;
            selectionRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            selectionRing.receiveShadows = false;
            for (int i = 0; i < selectionRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / selectionRing.positionCount;
                selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.035f, Mathf.Sin(angle) * radius));
            }
            selectionRing.enabled = false;
        }

        private void CreateBillboard()
        {
            var canvasObject = new GameObject("Vida do Inimigo", typeof(RectTransform), typeof(Canvas));
            billboard = canvasObject.transform;
            billboard.SetParent(transform, false);
            billboard.localPosition = new Vector3(0f, 2f, 0f);
            billboard.localScale = Vector3.one * 0.008f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)billboard).sizeDelta = new Vector2(200f, 100f);
            nameLabel = CreateText("Nome", new Vector2(0f, 22f), 30, Color.white);
            healthLabel = CreateText("PV", new Vector2(0f, -28f), 21, Color.white);
            hitLabel = CreateText("Dano Confirmado", new Vector2(0f, 62f), 40, SelectionColor);
            hitLabel.gameObject.SetActive(false);

            var background = new GameObject("Fundo da Vida", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)background.transform;
            rect.SetParent(billboard, false);
            rect.sizeDelta = new Vector2(136f, 10f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.07f, 0.07f, 0.9f);
            backgroundImage.raycastTarget = false;

            var fill = new GameObject("Vida", typeof(RectTransform), typeof(Image));
            healthFill = (RectTransform)fill.transform;
            healthFill.SetParent(rect, false);
            healthFill.anchorMin = Vector2.zero;
            healthFill.anchorMax = Vector2.one;
            healthFill.offsetMin = Vector2.zero;
            healthFill.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.88f, 0.25f, 0.18f);
            fillImage.raycastTarget = false;
        }

        private Text CreateText(string objectName, Vector2 position, int fontSize, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var text = textObject.GetComponent<Text>();
            text.rectTransform.SetParent(billboard, false);
            text.rectTransform.sizeDelta = new Vector2(230f, 54f);
            text.rectTransform.anchoredPosition = position;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private void OnDestroy()
        {
            foreach (var material in ownedMaterials)
                if (material != null) Destroy(material);
            foreach (var mesh in ownedMeshes)
                if (mesh != null) Destroy(mesh);
        }
    }
}
