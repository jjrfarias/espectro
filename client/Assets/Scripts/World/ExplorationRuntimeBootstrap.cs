using UnityEngine;

namespace Espectro.Prototype
{
    // Havia aqui uma segunda narrativa de tutorial, inteiramente local e fictícia ("Ancia Mira"
    // procura uma "Lumina" perto da forja, que abre a mina) coexistindo na mesma praça com a
    // Instrutora real (GddNpcBootstrap.cs) e disputando o mesmo campo
    // InteractionController.questStage que também recebe o progresso real do tutorial vindo do
    // servidor (ApplyRemoteTutorialStage) — resultado: duas narrativas desencontradas ao mesmo
    // tempo, sem nenhuma ligação com o GDD real. Decisão do usuário: desativar essa narrativa
    // fictícia. "Interacao Entrada Mina" continua intacta (só perde o componente de diálogo da
    // Lumina) porque GddNpcBootstrap.cs a usa como referência de posição pro Minerador.
    public static class ExplorationRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var elder = GameObject.Find("Ancia Mira");
            if (elder != null) elder.SetActive(false);

            var mineInteraction = GameObject.Find("Interacao Entrada Mina")?.GetComponent<WorldInteractable>();
            if (mineInteraction != null) Object.Destroy(mineInteraction);
        }
    }
}
