using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Espectro.Network
{
    // GDD §2/§9: chat local — mensagens ficam retidas e alcançam só quem está na mesma instância
    // do remetente (o próprio servidor já faz essa filtragem; aqui só exibe o que chega).
    // Presentation-only, mesmo padrão dos outros controllers: nenhuma mensagem é moderada ou
    // filtrada no cliente — isso já aconteceu no servidor antes de "chat.message" ser enviado.
    public sealed class NetworkChatController : MonoBehaviour
    {
        private const int MaxVisibleMessages = 6;
        private const int MaxMessageLength = 240; // Espelha CHAT_MESSAGE_MAX_LENGTH em contracts/src/index.ts.

        // Outros controllers com atalhos de teclado (M minerar, F atacar, Tab alvo, N mapa, E
        // interagir) devem checar isto antes de consumir uma tecla — sem isso, digitar no chat
        // dispararia essas ações. Suprime só os atalhos dos meus arquivos; WASD (movimento, em
        // PrototypePlayerController.cs) continua fora do meu alcance — ver nota no AGENT-WORK-LOG.
        public static bool InputFocused { get; private set; }

        public event Action<string> MessageSubmitted;

        private GameObject panel;
        private Text[] messageLines;
        private InputField chatField;

        public static NetworkChatController Create()
        {
            var root = new GameObject("Chat Online - Interface", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 507;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem Chat Online", typeof(EventSystem), typeof(StandaloneInputModule));
            var controller = root.AddComponent<NetworkChatController>();
            controller.BuildHud();
            controller.SetActive(false);
            return controller;
        }

        public void SetActive(bool active)
        {
            panel.SetActive(active);
            if (!active) InputFocused = false;
        }

        public void ApplyChatMessage(ChatMessagePayload payload)
        {
            if (payload == null) return;
            PushMessage($"{payload.characterName}: {payload.content}");
        }

        private void Update()
        {
            InputFocused = panel.activeInHierarchy && chatField.isFocused;
            if (InputFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Submit();
        }

        private void Submit()
        {
            var text = chatField.text.Trim();
            chatField.text = "";
            if (string.IsNullOrEmpty(text)) return;
            MessageSubmitted?.Invoke(text);
        }

        private void PushMessage(string line)
        {
            for (var i = 0; i < messageLines.Length - 1; i++)
                messageLines[i].text = messageLines[i + 1].text;
            messageLines[^1].text = line;
        }

        private void BuildHud()
        {
            // Canto inferior esquerdo, acima da dica de controles fixa do DesktopControlsBootstrap
            // (y 24-64) — ver nota lá sobre o espaço reservado.
            var bottomLeft = new Vector2(0f, 0f);
            panel = Panel(transform, "Fundo Chat", bottomLeft, new Vector2(24f, 76f), new Vector2(460f, 210f), new Color(0.04f, 0.04f, 0.045f, 0.82f));

            messageLines = new Text[MaxVisibleMessages];
            for (var i = 0; i < MaxVisibleMessages; i++)
            {
                messageLines[i] = Label(panel.transform, $"Linha {i}", "", 17, new Vector2(0f, 1f), new Vector2(10f, -10f - i * 26f), new Vector2(440f, 24f), TextAnchor.MiddleLeft);
            }

            chatField = CreateInputField(panel.transform, "Campo Chat", "Digite uma mensagem...", new Vector2(10f, 10f), new Vector2(340f, 40f));
            chatField.characterLimit = MaxMessageLength;
            MakeButton(panel.transform, "Botao Enviar", "ENVIAR", new Vector2(0f, 0f), new Vector2(360f, 10f), new Vector2(90f, 40f), Submit);
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            Rect(item, parent, new Vector2(0f, 0f), position, size);
            item.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var textArea = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            textArea.transform.SetParent(item.transform, false);
            Stretch((RectTransform)textArea.transform, 10f);
            var text = textArea.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObject.transform.SetParent(item.transform, false);
            Stretch((RectTransform)placeholderObject.transform, 10f);
            var placeholderText = placeholderObject.GetComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 18;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = placeholder;

            var field = item.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholderText;
            return field;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
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
            Label(item.transform, "Texto", value, 16, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
