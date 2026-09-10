using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    // O joystick virtual em "Interface/Joystick" foi pensado pra touch (Android). Com o foco do
    // projeto migrado pra web/desktop (mouse+teclado), ele só ocupa espaço na tela sem servir pra
    // nada — WASD/setas já move o personagem direto (PrototypePlayerController.MobileInput só
    // ganha prioridade quando é maior que o input de teclado). Escondido quando não há touch, e
    // substituído por uma dica curta dos comandos de teclado.
    public static class DesktopControlsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Input.touchSupported) return;

            var joystick = GameObject.Find("Joystick");
            if (joystick != null) joystick.SetActive(false);

            var canvas = GameObject.Find("Interface");
            if (canvas == null || canvas.transform.Find("Dica de Controles") != null) return;

            var hintObject = new GameObject("Dica de Controles", typeof(RectTransform), typeof(Text));
            hintObject.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)hintObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = new Vector2(420f, 40f);

            var text = hintObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.LowerLeft;
            text.color = new Color(1f, 1f, 1f, 0.75f);
            text.text = "WASD mover · SHIFT correr · E interagir · botão direito + mouse orbita câmera";
        }
    }
}
