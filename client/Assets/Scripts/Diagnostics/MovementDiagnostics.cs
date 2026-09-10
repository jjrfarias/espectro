using Espectro.Prototype;
using UnityEngine;

namespace Espectro.Diagnostics
{
    // Ferramenta temporária de leitura: não altera input, movimento, Animator ou câmera.
    // Ative em uma build de desenvolvimento com ?diagnostics=1 na URL ou F8 no Editor.
    [DefaultExecutionOrder(10000)]
    public sealed class MovementDiagnostics : MonoBehaviour
    {
        private const string QueryParameter = "diagnostics=1";
        private PrototypePlayerController player;
        private CharacterController characterController;
        private Animator animator;
        private Camera gameplayCamera;
        private Vector3 updatePosition;
        private Vector3 previousCameraPosition;
        private Vector3 lateCorrection;
        private Vector3 visualOffset;
        private float frameRate;
        private bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FindAnyObjectByType<MovementDiagnostics>() != null) return;
            var diagnostics = new GameObject("Diagnóstico de Movimento");
            DontDestroyOnLoad(diagnostics);
            diagnostics.AddComponent<MovementDiagnostics>();
#endif
        }

        private void Start()
        {
            visible = Application.absoluteURL.IndexOf(QueryParameter, System.StringComparison.OrdinalIgnoreCase) >= 0;
            FindReferences();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F8)) visible = !visible;
            if (player == null || characterController == null) FindReferences();
            if (player != null) updatePosition = player.transform.position;
            frameRate = Mathf.Lerp(frameRate, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.12f);
#endif
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (player != null) lateCorrection = player.transform.position - updatePosition;
            if (gameplayCamera == null && Camera.main != null) gameplayCamera = Camera.main;
#endif
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!visible) return;
            if (player == null || characterController == null) FindReferences();

            var cameraDelta = gameplayCamera == null ? Vector3.zero : gameplayCamera.transform.position - previousCameraPosition;
            if (gameplayCamera != null) previousCameraPosition = gameplayCamera.transform.position;
            var intendedSpeed = player != null ? player.CurrentSpeed : 0f;
            var actualVelocity = characterController != null ? characterController.velocity : Vector3.zero;
            var sentVelocity = player != null ? player.LastWorldVelocity : Vector3.zero;
            var animationSpeed = animator != null ? animator.GetFloat("Speed") : 0f;
            var visual = player != null ? player.transform.Find("Aventureiro") : null;
            visualOffset = visual != null ? visual.localPosition : Vector3.zero;

            var panel = new Rect(18f, 18f, 500f, 224f);
            GUI.Box(panel, "Diagnóstico de fluidez — F8 fecha");
            var values =
                $"FPS: {frameRate:0}\n" +
                $"Movimento pretendido: {intendedSpeed:0.00} u/s | CharacterController: {actualVelocity.magnitude:0.00} u/s\n" +
                $"Velocidade enviada: {sentVelocity.magnitude:0.00} u/s | Animator Speed: {animationSpeed:0.00}\n" +
                $"Correção após Update: {lateCorrection.magnitude:0.000} u/frame ({lateCorrection.x:0.000}, {lateCorrection.z:0.000})\n" +
                $"Deslocamento da câmera: {cameraDelta.magnitude:0.000} u/frame\n" +
                $"Offset local do modelo: ({visualOffset.x:0.000}, {visualOffset.y:0.000}, {visualOffset.z:0.000})\n\n" +
                "Interpretação: correção tardia alta aponta à rede; câmera alta com correção baixa aponta à câmera;\n" +
                "pretendido alto e CharacterController baixo aponta colisão/animação.";
            GUI.Label(new Rect(34f, 50f, 470f, 176f), values);
#endif
        }

        private void FindReferences()
        {
            player = FindAnyObjectByType<PrototypePlayerController>();
            characterController = player != null ? player.GetComponent<CharacterController>() : null;
            animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            gameplayCamera = Camera.main;
            if (gameplayCamera != null) previousCameraPosition = gameplayCamera.transform.position;
        }
    }
}
