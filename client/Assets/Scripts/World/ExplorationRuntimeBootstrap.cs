using UnityEngine;

namespace Espectro.Prototype
{
    public static class ExplorationRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var elder = GameObject.Find("Ancia Mira")?.GetComponent<WorldInteractable>();
            elder?.ConfigureStory(
                "Ancia Mira",
                new[]
                {
                    "O Berco esta inquieto. As pedras cantaram durante toda a noite.",
                    "Procure uma Lumina perto da forja. Sua luz pode revelar o caminho da mina.",
                    "Aproxime-se dos objetos marcados e use Interagir. Volte quando descobrir o que existe la."
                },
                0,
                1);
            if (elder != null)
            {
                var conclusion = new GameObject("Conclusao com Ancia");
                conclusion.transform.SetParent(elder.transform, false);
                conclusion.AddComponent<WorldInteractable>().ConfigureStory(
                    "Ancia Mira",
                    new[]
                    {
                        "Entao era verdade. A Lumina despertou a passagem.",
                        "Voce respondeu ao primeiro chamado do Berco. O que vive abaixo agora sabe seu nome."
                    },
                    3,
                    4);
            }

            var mine = GameObject.Find("Interacao Entrada Mina")?.GetComponent<WorldInteractable>();
            if (mine != null) mine.ConfigureStory(
                "Entrada da Mina",
                new[] { "A Lumina reage a escuridao.", "A passagem se abre e um ar frio sobe das profundezas." },
                2,
                3);

            CreateLumina();
        }

        private static void CreateLumina()
        {
            if (GameObject.Find("Lumina Coletavel") != null) return;
            var root = new GameObject("Lumina Coletavel");
            root.transform.position = new Vector3(-5.5f, 1.1f, -2.2f);
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Cristal Lumina";
            crystal.transform.SetParent(root.transform, false);
            crystal.transform.localScale = new Vector3(0.48f, 0.85f, 0.48f);
            crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = new Color(0.15f, 0.85f, 1f) };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.08f, 0.65f, 1f) * 2.2f);
            crystal.GetComponent<Renderer>().material = material;
            Object.Destroy(crystal.GetComponent<Collider>());
            root.AddComponent<LuminaFloat>();
            root.AddComponent<WorldInteractable>().ConfigureStory(
                "Lumina",
                new[] { "Uma pequena luz pulsa dentro do cristal.", "Voce recolheu a Lumina. Ela aponta para o leste." },
                1,
                2,
                true);
        }
    }

    public sealed class LuminaFloat : MonoBehaviour
    {
        private Vector3 origin;
        private void Start() => origin = transform.position;
        private void Update()
        {
            transform.position = origin + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * 0.18f);
            transform.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);
        }
    }
}
