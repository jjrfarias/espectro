using System;
using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Network
{
    // GDD §6: "cada nível concede um ponto de atributo" + §6/§13 "permite redistribuição
    // gratuita durante o teste, falando com a instrutora". Presentation-only, mesmo padrão de
    // NetworkEconomyController: nenhum valor de atributo é calculado aqui — tudo vem confirmado
    // do servidor via attributes.snapshot.
    public sealed class NetworkAttributesController : MonoBehaviour
    {
        public event Action<string> AllocateRequested;
        public event Action RespecRequested;

        private static readonly (string Code, string Label)[] Attributes =
        {
            ("strength", "Força"), ("agility", "Agilidade"), ("vitality", "Vitalidade"), ("resistance", "Resistência"),
        };

        private GameObject panel;
        private GameObject content;
        private Text unspentText;
        private Text maxHpText;
        private Button respecButton;
        private readonly System.Collections.Generic.Dictionary<string, Text> valueLabels = new();
        private readonly System.Collections.Generic.Dictionary<string, Button> allocateButtons = new();
        private int unspentPoints;
        private bool expanded; // Recolhido por padrão — abre ao clicar na barra de vida (NetworkCombatController).

        public static NetworkAttributesController Create()
        {
            var root = new GameObject("Atributos Online - Interface", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 506;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var controller = root.AddComponent<NetworkAttributesController>();
            controller.BuildHud();
            controller.SetActive(false);
            return controller;
        }

        public void SetActive(bool active)
        {
            panel.SetActive(active);
            content.SetActive(active && expanded);
        }

        public void ToggleExpanded()
        {
            expanded = !expanded;
            content.SetActive(panel.activeSelf && expanded);
        }

        public void ApplyAttributes(AttributesSnapshotPayload payload)
        {
            if (payload == null || payload.attributes == null) return;
            unspentPoints = payload.unspentPoints;
            unspentText.text = $"PONTOS LIVRES: {unspentPoints}";
            maxHpText.text = $"HP MÁXIMO: {payload.maxHp}";

            SetValue("strength", payload.attributes.strength);
            SetValue("agility", payload.attributes.agility);
            SetValue("vitality", payload.attributes.vitality);
            SetValue("resistance", payload.attributes.resistance);

            foreach (var button in allocateButtons.Values) button.interactable = unspentPoints > 0;
            respecButton.interactable = payload.attributes.strength > 5 || payload.attributes.agility > 5
                || payload.attributes.vitality > 5 || payload.attributes.resistance > 5;
        }

        private void SetValue(string code, int value) => valueLabels[code].text = value.ToString();

        private void BuildHud()
        {
            panel = new GameObject("Painel de Atributos", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var full = (RectTransform)panel.transform;
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.offsetMin = full.offsetMax = Vector2.zero;

            var topLeft = new Vector2(0f, 1f);
            // Abaixo da barra de vida (y ~ -125..-225) e do texto de nível/XP (y ~ -260..-295) do
            // NetworkCombatController — evita sobrepor os dois. Recolhido por padrão.
            content = Panel(panel.transform, "Fundo Atributos", topLeft, new Vector2(28f, -310f), new Vector2(340f, 358f), new Color(0.05f, 0.055f, 0.06f, 0.93f));
            var titleText = Label(content.transform, "Titulo Atributos", "ATRIBUTOS", 18, topLeft, new Vector2(16f, -12f), new Vector2(200f, 26f), TextAnchor.MiddleLeft);
            titleText.color = new Color(0.85f, 0.7f, 0.4f);
            titleText.fontStyle = FontStyle.Bold;
            unspentText = Label(content.transform, "Pontos Livres", "PONTOS LIVRES: 0", 20, topLeft, new Vector2(16f, -40f), new Vector2(310f, 30f), TextAnchor.MiddleLeft);
            maxHpText = Label(content.transform, "HP Maximo", "HP MÁXIMO: 150", 15, topLeft, new Vector2(16f, -68f), new Vector2(310f, 24f), TextAnchor.MiddleLeft);
            maxHpText.color = new Color(0.75f, 0.8f, 0.73f);
            var background = content;

            var rowY = -104f;
            foreach (var (code, label) in Attributes)
            {
                var row = new GameObject($"Linha {code}", typeof(RectTransform));
                row.transform.SetParent(background.transform, false);
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = rowRect.anchorMax = topLeft;
                rowRect.pivot = topLeft;
                rowRect.anchoredPosition = new Vector2(16f, rowY);
                rowRect.sizeDelta = new Vector2(310f, 42f);

                Label(row.transform, "Nome", label, 18, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(140f, 38f), TextAnchor.MiddleLeft);
                valueLabels[code] = Label(row.transform, "Valor", "5", 20, new Vector2(0.62f, 0.5f), Vector2.zero, new Vector2(50f, 38f), TextAnchor.MiddleCenter);
                var attributeCode = code;
                allocateButtons[code] = MakeButton(row.transform, "Mais", "+", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(44f, 38f),
                    () => AllocateRequested?.Invoke(attributeCode));
                allocateButtons[code].interactable = false;
                rowY -= 46f;
            }

            respecButton = MakeButton(background.transform, "Redistribuir", "REDISTRIBUIR GRÁTIS", topLeft, new Vector2(16f, rowY - 10f), new Vector2(310f, 44f),
                () => RespecRequested?.Invoke());
            respecButton.interactable = false;
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
            Label(item.transform, "Texto", value, 16, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
