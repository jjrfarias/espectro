using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    public static class CombatRuntimeBootstrap
    {
        // Combate congelado como prototipo. Reative esta inicializacao quando o design estiver definido.
        private static void InstallCombatPrototype()
        {
            var playerController = Object.FindAnyObjectByType<PrototypePlayerController>();
            if (playerController == null || playerController.GetComponent<PlayerCombat>() != null) return;
            var combat = playerController.gameObject.AddComponent<PlayerCombat>();
            CreateEnemy();
            CreateHud(combat);
        }

        private static void CreateEnemy()
        {
            var enemy = new GameObject("Rastejante da Mina");
            enemy.transform.position = new Vector3(5.5f, 0.65f, 1.5f);
            var body = CreatePart(enemy.transform, "Corpo", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.35f, 0.75f, 1.55f), new Color(0.48f, 0.12f, 0.12f));
            CreatePart(enemy.transform, "Olho Esquerdo", PrimitiveType.Sphere, new Vector3(-0.35f, 0.25f, 0.68f), Vector3.one * 0.22f, new Color(1f, 0.72f, 0.08f));
            CreatePart(enemy.transform, "Olho Direito", PrimitiveType.Sphere, new Vector3(0.35f, 0.25f, 0.68f), Vector3.one * 0.22f, new Color(1f, 0.72f, 0.08f));
            var hornLeft = CreatePart(enemy.transform, "Chifre Esquerdo", PrimitiveType.Capsule, new Vector3(-0.52f, 0.55f, 0.05f), new Vector3(0.14f, 0.42f, 0.14f), new Color(0.18f, 0.12f, 0.08f));
            hornLeft.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
            var hornRight = CreatePart(enemy.transform, "Chifre Direito", PrimitiveType.Capsule, new Vector3(0.52f, 0.55f, 0.05f), new Vector3(0.14f, 0.42f, 0.14f), new Color(0.18f, 0.12f, 0.08f));
            hornRight.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
            CreatePart(enemy.transform, "Pata Esquerda", PrimitiveType.Capsule, new Vector3(-0.48f, -0.42f, 0.05f), new Vector3(0.2f, 0.38f, 0.2f), new Color(0.3f, 0.07f, 0.06f));
            CreatePart(enemy.transform, "Pata Direita", PrimitiveType.Capsule, new Vector3(0.48f, -0.42f, 0.05f), new Vector3(0.2f, 0.38f, 0.2f), new Color(0.3f, 0.07f, 0.06f));
            Object.Destroy(body.GetComponent<SphereCollider>());
            var collider = enemy.AddComponent<CapsuleCollider>();
            collider.radius = 0.65f;
            collider.height = 1.2f;
            collider.center = Vector3.zero;
            enemy.AddComponent<EnemyCombatTarget>();
        }

        private static void CreateHud(PlayerCombat combat)
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem Combate");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            var attackObject = new GameObject("Botao Ataque", typeof(RectTransform), typeof(Image), typeof(Button));
            attackObject.transform.SetParent(canvas.transform, false);
            var attackRect = (RectTransform)attackObject.transform;
            attackRect.anchorMin = new Vector2(1f, 0f);
            attackRect.anchorMax = new Vector2(1f, 0f);
            attackRect.pivot = new Vector2(0.5f, 0.5f);
            attackRect.anchoredPosition = new Vector2(-175f, 175f);
            attackRect.sizeDelta = new Vector2(190f, 190f);
            attackObject.GetComponent<Image>().color = new Color(0.68f, 0.16f, 0.08f, 0.92f);
            attackObject.GetComponent<Button>().onClick.AddListener(combat.Attack);
            CreateText(attackRect, "ATACAR", 31, TextAnchor.MiddleCenter);

            var dodgeObject = new GameObject("Botao Esquiva", typeof(RectTransform), typeof(Image), typeof(Button));
            dodgeObject.transform.SetParent(canvas.transform, false);
            var dodgeRect = (RectTransform)dodgeObject.transform;
            dodgeRect.anchorMin = new Vector2(1f, 0f);
            dodgeRect.anchorMax = new Vector2(1f, 0f);
            dodgeRect.pivot = new Vector2(0.5f, 0.5f);
            dodgeRect.anchoredPosition = new Vector2(-390f, 125f);
            dodgeRect.sizeDelta = new Vector2(155f, 155f);
            dodgeObject.GetComponent<Image>().color = new Color(0.08f, 0.36f, 0.56f, 0.9f);
            dodgeObject.GetComponent<Button>().onClick.AddListener(combat.Dodge);
            CreateText(dodgeRect, "ESQUIVA", 24, TextAnchor.MiddleCenter);

            var healthObject = new GameObject("Barra de Vida", typeof(RectTransform), typeof(Slider));
            healthObject.transform.SetParent(canvas.transform, false);
            var healthRect = (RectTransform)healthObject.transform;
            healthRect.anchorMin = new Vector2(0f, 1f);
            healthRect.anchorMax = new Vector2(0f, 1f);
            healthRect.pivot = new Vector2(0f, 1f);
            healthRect.anchoredPosition = new Vector2(30f, -105f);
            healthRect.sizeDelta = new Vector2(430f, 48f);
            var background = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(healthRect, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.05f, 0.9f);
            var fill = new GameObject("Preenchimento", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(healthRect, false);
            Stretch((RectTransform)fill.transform, 6f);
            fill.GetComponent<Image>().color = new Color(0.8f, 0.12f, 0.1f, 1f);
            var slider = healthObject.GetComponent<Slider>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var label = CreateText(healthRect, "VIDA  100/100", 24, TextAnchor.MiddleCenter);
            combat.ConfigureHud(slider, label);
        }

        private static GameObject CreatePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            part.GetComponent<Renderer>().material = new Material(shader) { color = color };
            return part;
        }

        private static Text CreateText(Transform parent, string value, int size, TextAnchor alignment)
        {
            var textObject = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Stretch((RectTransform)textObject.transform);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
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
