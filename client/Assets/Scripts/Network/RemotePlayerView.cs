using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Network
{
    // Representação visual simples de outro jogador, criada em runtime a partir de um
    // world.snapshot. Não reaproveita o rig do jogador local (Corte0ProjectSetup.CreatePlayer)
    // para não depender de conteúdo gerado só no Editor; suaviza posição/rotação entre snapshots
    // que chegam a ~10 Hz.
    public sealed class RemotePlayerView : MonoBehaviour
    {
        private const float Smoothing = 10f;

        private Vector3 targetPosition;
        private float targetFacingY;
        private Transform nameBillboard;

        public string CharacterId { get; private set; }

        public static RemotePlayerView Create(SnapshotCharacterDto data)
        {
            var root = new GameObject($"Jogador Remoto - {data.name}");
            var view = root.AddComponent<RemotePlayerView>();
            view.CharacterId = data.characterId;

            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Corpo";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.78f, 0.42f, 0.14f) };
            Object.Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Cabeca";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            head.transform.localScale = Vector3.one * 0.56f;
            head.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.82f, 0.63f, 0.45f) };
            Object.Destroy(head.GetComponent<Collider>());

            view.CreateNameLabel(data.name);
            view.SnapTo(data);
            return view;
        }

        public void ApplySnapshot(SnapshotCharacterDto data)
        {
            targetPosition = new Vector3(data.position.x, data.position.y, data.position.z);
            targetFacingY = data.facingY;
        }

        private void SnapTo(SnapshotCharacterDto data)
        {
            ApplySnapshot(data);
            transform.position = targetPosition;
            transform.rotation = Quaternion.Euler(0f, targetFacingY, 0f);
        }

        private void Update()
        {
            var t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, targetFacingY, 0f), t);

            if (nameBillboard != null && Camera.main != null)
            {
                nameBillboard.rotation = Camera.main.transform.rotation;
            }
        }

        private void CreateNameLabel(string characterName)
        {
            var canvasObject = new GameObject("Nome", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.012f;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(320f, 60f);
            nameBillboard = canvasObject.transform;

            var textObject = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 42;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = characterName;
        }
    }
}
