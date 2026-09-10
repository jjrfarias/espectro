using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Espectro.Network
{
    // Tela mínima de login/criação de personagem: funcional, não definitiva. Pensada para ser
    // restilizada pela interface principal do jogo sem precisar tocar em NetworkSession.cs, que
    // concentra toda a lógica de rede.
    public sealed class NetworkUI : MonoBehaviour
    {
        public event Action<string, string> LoginSubmitted;
        public event Action<string, string> RegisterSubmitted;
        public event Action<string> CharacterNameSubmitted;

        private GameObject authPanel;
        private InputField emailField;
        private InputField passwordField;
        private Text authStatus;

        private GameObject characterPanel;
        private InputField characterNameField;
        private Text characterStatus;

        public static NetworkUI Create()
        {
            var root = new GameObject("Rede - Interface", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem Rede");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            var ui = root.AddComponent<NetworkUI>();
            ui.BuildAuthPanel(root.transform);
            ui.BuildCharacterPanel(root.transform);
            ui.ShowAuth();
            return ui;
        }

        public void ShowAuth()
        {
            authPanel.SetActive(true);
            characterPanel.SetActive(false);
        }

        public void ShowCharacterCreation()
        {
            authPanel.SetActive(false);
            characterPanel.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetAuthStatus(string message) => authStatus.text = message;
        public void SetCharacterStatus(string message) => characterStatus.text = message;

        private void BuildAuthPanel(Transform parent)
        {
            authPanel = CreateBackground(parent, "Painel de Login");
            var panelTransform = authPanel.transform;

            var call = CreateLabel(panelTransform, "Chamada", "O VÉU ESTÁ SE ABRINDO", 18, new Vector2(0f, 385f), new Vector2(760f, 40f));
            call.color = new Color(0.55f, 0.86f, 0.82f);
            var story = CreateLabel(panelTransform, "Historia", "Em O Berço, cada trilha guarda uma memória.\nAtravesse a Mata dos Sussurros, descubra as ruínas\ne escolha o que o seu personagem vai proteger.", 22, new Vector2(-470f, 55f), new Vector2(560f, 210f));
            story.alignment = TextAnchor.UpperLeft;
            story.color = new Color(0.78f, 0.86f, 0.84f);
            var title = CreateLabel(panelTransform, "Titulo", "ESPECTRO", 56, new Vector2(270f, 285f), new Vector2(620f, 80f));
            title.color = new Color(0.96f, 0.72f, 0.34f);
            var subtitle = CreateLabel(panelTransform, "Subtitulo", "O BERÇO", 22, new Vector2(270f, 238f), new Vector2(620f, 40f));
            subtitle.color = new Color(0.72f, 0.82f, 0.76f);
            CreateLabel(panelTransform, "Prompt", "Entre para continuar sua jornada", 24, new Vector2(270f, 190f), new Vector2(620f, 45f));
            emailField = CreateInputField(panelTransform, "Campo Email", "e-mail", false, new Vector2(0f, 140f));
            passwordField = CreateInputField(panelTransform, "Campo Senha", "senha", true, new Vector2(0f, 60f));
            authStatus = CreateLabel(panelTransform, "Status", "", 20, new Vector2(270f, -38f), new Vector2(620f, 48f));
            authStatus.color = new Color(1f, 0.55f, 0.5f);

            CreateButton(panelTransform, "Botao Entrar", "ENTRAR NO BERÇO", new Vector2(110f, -115f), new Vector2(300f, 68f),
                () => LoginSubmitted?.Invoke(emailField.text, passwordField.text));
            CreateButton(panelTransform, "Botao Criar Conta", "CRIAR CONTA", new Vector2(430f, -115f), new Vector2(300f, 68f),
                () => RegisterSubmitted?.Invoke(emailField.text, passwordField.text));
        }

        private void BuildCharacterPanel(Transform parent)
        {
            characterPanel = CreateBackground(parent, "Painel de Personagem");
            var panelTransform = characterPanel.transform;

            var title = CreateLabel(panelTransform, "Titulo", "SUA HISTÓRIA COMEÇA AQUI", 38, new Vector2(0f, 180f), new Vector2(900f, 70f));
            title.color = new Color(0.96f, 0.72f, 0.34f);
            var hint = CreateLabel(panelTransform, "Hint", "Dê um nome à pessoa que atravessará o Véu.", 22, new Vector2(0f, 120f), new Vector2(800f, 40f));
            hint.color = new Color(0.75f, 0.84f, 0.8f);
            characterNameField = CreateInputField(panelTransform, "Campo Nome", "nome do personagem", false, new Vector2(0f, 40f));
            characterStatus = CreateLabel(panelTransform, "Status", "", 24, new Vector2(0f, -30f), new Vector2(560f, 60f));
            characterStatus.color = new Color(1f, 0.55f, 0.5f);

            CreateButton(panelTransform, "Botao Criar Personagem", "ENTRAR NO MUNDO", new Vector2(0f, -110f), new Vector2(360f, 70f),
                () => CharacterNameSubmitted?.Invoke(characterNameField.text));

            characterPanel.SetActive(false);
        }

        private static GameObject CreateBackground(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.075f, 0.98f);
            return panel;
        }

        private static Text CreateLabel(Transform parent, string name, string value, int fontSize, Vector2 anchoredPosition, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholder, bool isPassword, Vector2 anchoredPosition)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(560f, 68f);
            item.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var textArea = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            textArea.transform.SetParent(item.transform, false);
            Stretch((RectTransform)textArea.transform, 16f);
            var text = textArea.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObject.transform.SetParent(item.transform, false);
            Stretch((RectTransform)placeholderObject.transform, 16f);
            var placeholderText = placeholderObject.GetComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 28;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = placeholder;

            var field = item.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;
            return field;
        }

        private static void CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = new Color(0.14f, 0.42f, 0.62f, 0.95f);
            item.GetComponent<Button>().onClick.AddListener(onClick);

            var textObject = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(item.transform, false);
            Stretch((RectTransform)textObject.transform);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }
    }
}
