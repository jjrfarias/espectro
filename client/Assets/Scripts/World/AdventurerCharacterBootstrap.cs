using UnityEngine;

namespace Espectro.Prototype
{
    // Substitui o corpo em primitivas do jogador (criado em Corte0ProjectSetup.CreatePlayer e
    // decorado com acessórios em StylizedVisualBootstrap.UpgradePlayer) pelo modelo real do
    // KayKit, preparado uma única vez em Editor por Assets/Editor/AdventurerCharacterSetup.cs e
    // salvo como prefab em Resources.
    //
    // Roda em MonoBehaviour.Start() em vez de [RuntimeInitializeOnLoadMethod] direto: a ordem
    // entre diferentes RuntimeInitializeOnLoadMethod(AfterSceneLoad) não é garantida, e
    // StylizedVisualBootstrap cria os acessórios em primitivas nesse mesmo evento. Start() só
    // roda depois que todo AfterSceneLoad já terminou, então "Acessorios Visuais" sempre existe
    // (ou não) antes da gente decidir escondê-lo.
    public static class AdventurerCharacterBootstrap
    {
        private const string PrefabPath = "EspectroModels/Adventurers/Aventureiro";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<AdventurerBootstrapRunner>() != null) return;
            new GameObject("Bootstrap do Aventureiro").AddComponent<AdventurerBootstrapRunner>();
        }

        private static void Run()
        {
            var player = GameObject.Find("Jogador");
            if (player == null || player.transform.Find("Aventureiro") != null) return;

            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                // Pacote KayKit não instalado ou prefab ainda não gerado pelo Editor — mantém o
                // corpo em primitivas como está, sem quebrar o protótipo.
                return;
            }

            var oldVisual = player.transform.Find("Visual");
            if (oldVisual != null) oldVisual.gameObject.SetActive(false);
            var oldAccessories = player.transform.Find("Acessorios Visuais");
            if (oldAccessories != null) oldAccessories.gameObject.SetActive(false);

            var instance = Object.Instantiate(prefab, player.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            var animator = instance.GetComponent<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            var controller = player.GetComponent<PrototypePlayerController>();
            if (animator != null && controller != null)
            {
                player.AddComponent<AdventurerLocomotionDriver>().Configure(animator, controller);
            }
        }

        private sealed class AdventurerBootstrapRunner : MonoBehaviour
        {
            private void Start()
            {
                Run();
                Destroy(gameObject);
            }
        }
    }

    public sealed class AdventurerLocomotionDriver : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private Animator animator;
        private PrototypePlayerController controller;

        public void Configure(Animator targetAnimator, PrototypePlayerController targetController)
        {
            animator = targetAnimator;
            controller = targetController;
        }

        private void Update()
        {
            if (animator == null || controller == null) return;
            animator.SetFloat(SpeedParam, controller.CurrentSpeed);
        }
    }
}
