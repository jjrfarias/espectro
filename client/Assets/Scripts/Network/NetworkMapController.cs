using System;
using System.Collections.Generic;
using Espectro.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Network
{
    // Minimapa + mapa cheio (tecla M). Presentation only: só desenha o que já vem no
    // world.snapshot (posição do próprio personagem, outros jogadores, inimigos, veios de
    // recurso) — nenhuma posição é inventada aqui. Mesmo padrão de NetworkCombatController e
    // NetworkEconomyController.
    public sealed class NetworkMapController : MonoBehaviour
    {
        // "Terreno do Berco" (Corte0ProjectSetup.CreateEnvironment) é um Plane de escala 8 ->
        // vai de -40 a 40 em X/Z (ver nota em StylizedVisualBootstrap.cs sobre o mesmo limite).
        private const float WorldHalfExtent = 90f;
        private const float MinimapRadiusUnits = 26f; // área do mundo visível ao redor do jogador no minimapa.

        private PrototypePlayerController player;
        private GameObject root;
        private RectTransform minimapArea;
        private RectTransform fullMapPanel;
        private RectTransform fullMapArea;
        private RectTransform playerArrow;
        private RectTransform fullMapPlayerArrow;
        private bool fullMapOpen;

        private readonly List<RectTransform> pool = new();
        private int poolCursor;

        public static NetworkMapController Create(PrototypePlayerController player)
        {
            var go = new GameObject("Mapa Online - Interface", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var controller = go.AddComponent<NetworkMapController>();
            controller.player = player;
            controller.BuildUi();
            controller.SetActive(false);
            return controller;
        }

        public void SetActive(bool active)
        {
            root.SetActive(active);
            if (!active)
            {
                fullMapOpen = false;
                fullMapPanel.gameObject.SetActive(false);
            }
        }

        public void ApplySnapshot(WorldSnapshotPayload snapshot)
        {
            if (snapshot?.self?.position == null) return;
            poolCursor = 0;

            var selfPosition = new Vector2(snapshot.self.position.x, snapshot.self.position.z);
            PlaceArrow(playerArrow, minimapArea, Vector2.zero, snapshot.self.facingY);
            PlaceArrow(fullMapPlayerArrow, fullMapArea, WorldToFullMap(selfPosition), snapshot.self.facingY);

            foreach (var other in snapshot.others ?? Array.Empty<SnapshotCharacterDto>())
            {
                if (other?.position == null) continue;
                var worldPos = new Vector2(other.position.x, other.position.z);
                PlaceBlip(minimapArea, WorldToMinimap(worldPos, selfPosition), new Color(0.35f, 0.6f, 1f));
                PlaceBlip(fullMapArea, WorldToFullMap(worldPos), new Color(0.35f, 0.6f, 1f));
            }

            foreach (var enemy in snapshot.enemies ?? Array.Empty<SnapshotEnemyDto>())
            {
                if (enemy?.position == null || !enemy.alive) continue;
                var worldPos = new Vector2(enemy.position.x, enemy.position.z);
                PlaceBlip(minimapArea, WorldToMinimap(worldPos, selfPosition), new Color(0.85f, 0.2f, 0.18f));
                PlaceBlip(fullMapArea, WorldToFullMap(worldPos), new Color(0.85f, 0.2f, 0.18f));
            }

            foreach (var node in snapshot.resourceNodes ?? Array.Empty<SnapshotResourceNodeDto>())
            {
                if (node?.position == null || !node.available) continue;
                var worldPos = new Vector2(node.position.x, node.position.z);
                var color = node.resourceCode == "ferro" ? new Color(0.55f, 0.34f, 0.24f) : new Color(0.72f, 0.44f, 0.22f);
                PlaceBlip(minimapArea, WorldToMinimap(worldPos, selfPosition), color);
                PlaceBlip(fullMapArea, WorldToFullMap(worldPos), color);
            }

            for (var i = poolCursor; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        private void Update()
        {
            // N, não M: M já abre/mineira o veio de recurso em NetworkEconomyController — as duas
            // teclas dispararem juntas faria minerar e abrir/fechar o mapa ao mesmo tempo.
            if (Input.GetKeyDown(KeyCode.N) && root.activeSelf)
            {
                fullMapOpen = !fullMapOpen;
                fullMapPanel.gameObject.SetActive(fullMapOpen);
            }
        }

        private Vector2 WorldToMinimap(Vector2 worldPos, Vector2 selfWorldPos)
        {
            var relative = worldPos - selfWorldPos;
            var normalized = relative / MinimapRadiusUnits;
            return ClampToUnitCircle(normalized) * (minimapArea.rect.width * 0.5f);
        }

        private Vector2 WorldToFullMap(Vector2 worldPos)
        {
            var normalized = worldPos / WorldHalfExtent;
            normalized.x = Mathf.Clamp(normalized.x, -1f, 1f);
            normalized.y = Mathf.Clamp(normalized.y, -1f, 1f);
            return normalized * (fullMapArea.rect.width * 0.5f);
        }

        private static Vector2 ClampToUnitCircle(Vector2 value)
        {
            var magnitude = value.magnitude;
            return magnitude > 1f ? value / magnitude : value;
        }

        private void PlaceArrow(RectTransform arrow, RectTransform area, Vector2 localPos, float facingYDegrees)
        {
            arrow.SetParent(area, false);
            arrow.anchoredPosition = localPos;
            arrow.localRotation = Quaternion.Euler(0f, 0f, -facingYDegrees);
        }

        private void PlaceBlip(RectTransform area, Vector2 localPos, Color color)
        {
            RectTransform blip;
            if (poolCursor < pool.Count)
            {
                blip = pool[poolCursor];
            }
            else
            {
                blip = CreateBlip(transform);
                pool.Add(blip);
            }
            poolCursor++;
            blip.gameObject.SetActive(true);
            blip.SetParent(area, false);
            blip.anchoredPosition = localPos;
            blip.GetComponent<Image>().color = color;
        }

        private static RectTransform CreateBlip(Transform parent)
        {
            var go = new GameObject("Marca", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(10f, 10f);
            go.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private void BuildUi()
        {
            root = new GameObject("Raiz do Mapa", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            // Minimapa: canto inferior direito, livre (economia ocupa o superior direito, combate
            // o superior/central, dica de controles o inferior esquerdo). Moldura dupla (borda
            // dourada + fundo escuro) pra parecer um elemento de HUD de verdade, não um retângulo cru.
            var accent = new Color(0.72f, 0.56f, 0.28f, 0.9f);
            var minimapFrame = Panel(root.transform, "Moldura Minimapa", new Vector2(1f, 0f), new Vector2(-22f, 22f), new Vector2(228f, 228f), accent);
            var minimapBackground = Panel(minimapFrame.transform, "Fundo Minimapa", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f), new Color(0.05f, 0.075f, 0.06f, 0.95f));
            minimapArea = (RectTransform)minimapBackground.transform;
            Label(root.transform, "Dica do Mapa", "N — MAPA COMPLETO", 14, new Vector2(1f, 0f), new Vector2(-22f, 256f), new Vector2(228f, 22f), TextAnchor.MiddleCenter).color = new Color(0.85f, 0.88f, 0.83f, 0.75f);
            playerArrow = CreatePlayerArrow(minimapArea, 16f);

            fullMapPanel = (RectTransform)Panel(root.transform, "Painel Mapa Completo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 760f), new Color(0.02f, 0.02f, 0.02f, 0.95f)).transform;
            fullMapPanel.gameObject.SetActive(false);
            var mapTitle = Label(fullMapPanel, "Titulo Mapa", "O BERÇO", 30, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(600f, 40f), TextAnchor.MiddleCenter);
            mapTitle.color = new Color(0.85f, 0.7f, 0.4f);
            mapTitle.fontStyle = FontStyle.Bold;
            var fullMapFrame = Panel(fullMapPanel, "Moldura Mapa Completo", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(648f, 648f), accent);
            var fullMapBackground = Panel(fullMapFrame.transform, "Fundo Mapa Completo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 640f), new Color(0.09f, 0.13f, 0.09f, 1f));
            fullMapArea = (RectTransform)fullMapBackground.transform;
            fullMapPlayerArrow = CreatePlayerArrow(fullMapArea, 18f);
            BuildLegend(fullMapPanel);
        }

        private void BuildLegend(Transform parent)
        {
            var legendY = -680f;
            AddLegendRow(parent, new Vector2(0f, legendY), new Color(1f, 1f, 1f), "Você");
            AddLegendRow(parent, new Vector2(150f, legendY), new Color(0.35f, 0.6f, 1f), "Jogadores");
            AddLegendRow(parent, new Vector2(320f, legendY), new Color(0.85f, 0.2f, 0.18f), "Inimigos");
            AddLegendRow(parent, new Vector2(470f, legendY), new Color(0.55f, 0.34f, 0.24f), "Ferro");
            AddLegendRow(parent, new Vector2(600f, legendY), new Color(0.72f, 0.44f, 0.22f), "Cobre");
        }

        private static void AddLegendRow(Transform parent, Vector2 position, Color color, string label)
        {
            var swatch = Panel(parent, "Legenda", new Vector2(0.5f, 1f), position + new Vector2(-60f, 0f), new Vector2(14f, 14f), color);
            Label(parent, "Legenda Texto", label, 16, new Vector2(0.5f, 1f), position + new Vector2(-40f, 3f), new Vector2(120f, 24f), TextAnchor.MiddleLeft);
        }

        private static RectTransform CreatePlayerArrow(RectTransform area, float size)
        {
            // Anel dourado atrás + núcleo branco na frente — destaca o próprio jogador em meio
            // aos pontos de jogadores/inimigos/recursos, todos círculos menores e sem anel.
            var ring = new GameObject("Voce (Anel)", typeof(RectTransform), typeof(Image));
            ring.transform.SetParent(area, false);
            var ringRect = (RectTransform)ring.transform;
            ringRect.sizeDelta = new Vector2(size * 1.7f, size * 1.7f);
            ring.GetComponent<Image>().color = new Color(0.85f, 0.68f, 0.32f, 0.9f);
            ring.GetComponent<Image>().raycastTarget = false;

            var go = new GameObject("Voce", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(ring.transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);
            go.GetComponent<Image>().color = Color.white;
            go.GetComponent<Image>().raycastTarget = false;
            return ringRect;
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
    }
}
