using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    // GDD-MVP.md §4/§13: os cinco NPCs do tutorial produtivo (Instrutora, Minerador, Ferreiro,
    // Comerciante, Cronista) não existiam na cena — o que travava de verdade a fundição
    // (economy.service.ts exige hasTalkedTo(character, "ferreiro") antes de fundir) e deixava o
    // painel de Comerciante/Forja de NetworkEconomyController.cs sem gatilho real. Corpos e
    // posições aqui são deliberadamente simples (cápsula + esfera, sem rig) — a aparência de
    // verdade continua sendo trabalho de arte; isto só torna os cinco papéis do GDD alcançáveis.
    public static class GddNpcBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (GameObject.Find("NPC Instrutora") != null) return; // já instalado (recarga de cena em Play Mode).

            // GDD §4 passo 3-5: "surgir na praça de O Berço... falar com a instrutora" — o marco
            // "Mil Caminhos" (BercoWorldArt.BuildRoutes, center) é a própria praça/spawn.
            CreateNpc("Instrutora", new Vector3(-3.8f, 0f, 6.6f), 180f,
                new Color(0.32f, 0.42f, 0.62f),
                "A instrutora observa o caminho, pronta para orientar quem chega.");

            var forja = FrontOfBuilding("Forja", 1.2f);
            CreateNpc("Ferreiro", forja ?? new Vector3(6f, 0f, -1.5f), 0f,
                new Color(0.42f, 0.22f, 0.18f),
                "O ferreiro trabalha a bigorna, pronto para liberar a forja a quem perguntar.");

            var estalagem = FrontOfBuilding("Estalagem", 1.4f);
            CreateNpc("Comerciante", estalagem ?? new Vector3(-6f, 0f, -1.5f), 0f,
                new Color(0.55f, 0.42f, 0.15f),
                "O comerciante organiza suas mercadorias, pronto para comprar e vender.");

            var conselho = FrontOfBuilding("Casa do Conselho", 1.6f);
            CreateNpc("Cronista", conselho ?? new Vector3(0f, 0f, -4f), 0f,
                new Color(0.4f, 0.28f, 0.5f),
                "O cronista registra os feitos de O Berço junto ao mural.");

            var entradaMina = GameObject.Find("Interacao Entrada Mina");
            var posicaoMina = entradaMina != null
                ? entradaMina.transform.position + entradaMina.transform.forward * -2.2f
                : new Vector3(21f, 0f, -9.5f);
            CreateNpc("Minerador", posicaoMina, 0f,
                new Color(0.35f, 0.35f, 0.38f),
                "O minerador aguarda perto da mina, pronto para ensinar a extração.");
        }

        // Reaproveita o mesmo cálculo de fachada de BercoWorldArt.BuildVillage: soma os bounds de
        // todos os renderers do prédio nomeado e devolve um ponto à frente dele. Retorna null se o
        // prédio não existir na cena (fallback tratado pelo chamador).
        private static Vector3? FrontOfBuilding(string buildingName, float distanceInFront)
        {
            var building = GameObject.Find(buildingName);
            if (building == null) return null;
            var renderers = building.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return new Vector3(bounds.center.x, 0f, bounds.min.z - distanceInFront);
        }

        private static void CreateNpc(string displayName, Vector3 position, float yaw, Color robeColor, string line)
        {
            var root = new GameObject($"NPC {displayName}");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Corpo";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            body.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = robeColor };
            Object.Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Cabeca";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);
            head.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(0.86f, 0.68f, 0.54f) };
            Object.Destroy(head.GetComponent<Collider>());

            // Colisor único pro conjunto — sólido o bastante pra não atravessar, sem precisar de
            // um por parte.
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.7f;
            collider.radius = 0.35f;

            CreateNameLabel(root.transform, displayName);

            root.AddComponent<WorldInteractable>().Configure(displayName, line);
        }

        private static void CreateNameLabel(Transform parent, string displayName)
        {
            var canvasObject = new GameObject("Nome", typeof(RectTransform), typeof(Canvas));
            var billboard = canvasObject.transform;
            billboard.SetParent(parent, false);
            billboard.localPosition = new Vector3(0f, 2.15f, 0f);
            billboard.localScale = Vector3.one * 0.012f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)billboard).sizeDelta = new Vector2(220f, 60f);

            var textObject = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(billboard, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = displayName;
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            parent.gameObject.AddComponent<NpcNameBillboard>().target = billboard;
        }
    }

    // Mesma ideia do billboard de vida em NetworkEnemyView.cs: o texto sempre encara a câmera,
    // sem depender de qual direção o NPC está virado.
    public sealed class NpcNameBillboard : MonoBehaviour
    {
        public Transform target;

        private void LateUpdate()
        {
            if (target == null || Camera.main == null) return;
            target.rotation = Camera.main.transform.rotation;
        }
    }
}
