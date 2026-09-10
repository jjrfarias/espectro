using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    // GDD-MVP.md §4/§13: os cinco NPCs do tutorial produtivo (Instrutora, Minerador, Ferreiro,
    // Comerciante, Cronista) não existiam na cena — o que travava de verdade a fundição
    // (economy.service.ts exige hasTalkedTo(character, "ferreiro") antes de fundir) e deixava o
    // painel de Comerciante/Forja de NetworkEconomyController.cs sem gatilho real. Usa o mesmo
    // prefab de personagem dos jogadores (EspectroModels/Adventurers/Aventureiro, ver
    // RemotePlayerView.cs) em vez de formas primitivas, com fallback procedural só se o prefab
    // não existir. As falas são roteirizadas com o texto real de cada papel (GDD §4 passo a passo
    // e §13); antes usavam uma frase ambiente única, sem instrução nenhuma.
    public static class GddNpcBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (GameObject.Find("NPC Instrutora") != null) return; // já instalado (recarga de cena em Play Mode).

            // GDD §4 passo 3-5: "surgir na praça de O Berço... falar com a instrutora" — o marco
            // "Mil Caminhos" (BercoWorldArt.BuildRoutes, center) é a própria praça/spawn.
            CreateNpc("Instrutora", new Vector3(-3.8f, 0f, 6.6f), 180f, new Color(0.32f, 0.42f, 0.62f), new[]
            {
                "Seja bem-vinda(o) a O Berço, viajante. Eu sou a instrutora — aqui você aprende o essencial antes de seguir para a floresta e a montanha.",
                "Use WASD para andar e a tecla E para interagir com pessoas e objetos ao seu redor.",
                "Seus primeiros passos: enfrente uma criatura na floresta, extraia minério na mina, funda um lingote com o ferreiro e venda ao comerciante.",
                "Se quiser redistribuir seus pontos de atributo durante o teste, volte aqui e fale comigo de novo.",
            });

            var forja = FrontOfBuilding("Forja", 1.2f);
            CreateNpc("Ferreiro", forja ?? new Vector3(6f, 0f, -1.5f), 0f, new Color(0.42f, 0.22f, 0.18f), new[]
            {
                "Bem-vindo à forja de O Berço. Aqui fundimos minério em lingotes prontos pro comércio.",
                "Traga minério de ferro e eu libero a forja pra você fundir seus próprios lingotes.",
                "Um lingote vale bem mais que o minério cru — compensa o tempo que você passou na mina.",
            });

            var estalagem = FrontOfBuilding("Estalagem", 1.4f);
            CreateNpc("Comerciante", estalagem ?? new Vector3(-6f, 0f, -1.5f), 0f, new Color(0.55f, 0.42f, 0.15f), new[]
            {
                "Chegou na hora certa! Compro lingotes, couro e outros materiais, e vendo poções pra quem precisa.",
                "Um lingote de ferro rende boas moedas — é o jeito mais rápido de lucrar com o que você extrai.",
                "Se precisar se curar em campo, sempre tenho poções à venda.",
            });

            var conselho = FrontOfBuilding("Casa do Conselho", 1.6f);
            CreateNpc("Cronista", conselho ?? new Vector3(0f, 0f, -4f), 0f, new Color(0.4f, 0.28f, 0.5f), new[]
            {
                "Eu registro os feitos de todos que passam por O Berço, no mural desta casa do conselho.",
                "Toda conquista importante — sua primeira criatura derrotada, seu primeiro lingote — fica marcada aqui pra sempre.",
                "Volte sempre que quiser conferir a crônica da sua jornada.",
            });

            var entradaMina = GameObject.Find("Interacao Entrada Mina");
            var posicaoMina = entradaMina != null
                ? entradaMina.transform.position + entradaMina.transform.forward * -2.2f
                : new Vector3(21f, 0f, -9.5f);
            CreateNpc("Minerador", posicaoMina, 0f, new Color(0.35f, 0.35f, 0.38f), new[]
            {
                "Ah, um novo rosto! Eu cuido da entrada da mina e ensino quem chega a extrair minério.",
                "Leve esta picareta. Com ela você consegue minerar ferro na mina — três minérios já bastam pra começar.",
                "Depois de minerar, volte à cidade e procure o ferreiro pra fundir um lingote.",
            });
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

        private static void CreateNpc(string displayName, Vector3 position, float yaw, Color fallbackColor, string[] lines)
        {
            var root = new GameObject($"NPC {displayName}");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Mesmo modelo usado pelo jogador local/remoto (RemotePlayerView.cs) — evita um NPC
            // com corpo genérico ao lado de aventureiros com visual de verdade. Fallback só entra
            // se o prefab não existir nesta build.
            var prefab = Resources.Load<GameObject>("EspectroModels/Adventurers/Aventureiro");
            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, root.transform, false);
                model.name = "Aventureiro";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var modelAnimator = model.GetComponentInChildren<Animator>();
                if (modelAnimator != null) modelAnimator.applyRootMotion = false;
                foreach (var modelCollider in model.GetComponentsInChildren<Collider>()) Object.Destroy(modelCollider);
            }
            else
            {
                var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Corpo";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                body.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
                body.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = fallbackColor };
                Object.Destroy(body.GetComponent<Collider>());

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Cabeca";
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
                head.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);
                head.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(0.86f, 0.68f, 0.54f) };
                Object.Destroy(head.GetComponent<Collider>());
            }

            // Colisor único pro conjunto — sólido o bastante pra não atravessar, sem precisar de
            // um por parte do modelo/fallback.
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.7f;
            collider.radius = 0.35f;

            CreateNameLabel(root.transform, displayName);

            // requiredQuestStage/nextQuestStage = -1: sempre interagível e nunca mexe no estágio
            // local de quest da Lumina (mesmo efeito de Configure(), só que com várias falas em
            // vez de uma só — a progressão de verdade desses NPCs é server-tracked via
            // tutorial.snapshot, não essa flag local).
            root.AddComponent<WorldInteractable>().ConfigureStory(displayName, lines, -1, -1, false);
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
