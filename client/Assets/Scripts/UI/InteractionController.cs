using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    public sealed class InteractionController : MonoBehaviour
    {
        public static System.Action<string> OnlineNpcTalkRequested;
        // Mesmo padrão de NetworkChatController.InputFocused: NetworkEconomyController também lê a
        // tecla E (pra abrir Forja/Comerciante) e agora pode disputar com este diálogo, já que os
        // NPCs do GDD ficam dentro do alcance de ambos os controllers. Setado no exato momento em
        // que dialoguePanel muda de estado (não amostrado uma vez por Update), pra não depender da
        // ordem de execução entre os dois MonoBehaviours no mesmo frame.
        public static bool DialogueActive;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionLabel;
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text dialogueText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private float interactionRange = 4f;

        private WorldInteractable current;
        private WorldInteractable dialogueTarget;
        private int dialogueLine;
        private int questStage;

        private void Awake()
        {
            if (actionButton == null) return;
            actionButton.onClick.RemoveListener(Interact);
            actionButton.onClick.AddListener(Interact);
        }

        public void Configure(PrototypePlayerController target, Button button, Text label, GameObject panel, Text speaker, Text body, Text objective)
        {
            player = target;
            actionButton = button;
            actionLabel = label;
            dialoguePanel = panel;
            speakerText = speaker;
            dialogueText = body;
            objectiveText = objective;
            dialoguePanel.SetActive(false);
            questStage = 0;
            RefreshObjective();
        }

        // Chamado pela sessão online quando o servidor confirma o tutorial persistente.
        // Mantém o mesmo texto e a mesma leitura do modo local.
        public void ApplyRemoteTutorialStage(int stage)
        {
            questStage = Mathf.Clamp(stage, 0, 4);
            RefreshObjective();
        }

        private void Update()
        {
            current = dialoguePanel.activeSelf ? dialogueTarget : FindNearest();
            actionButton.gameObject.SetActive(current != null);
            if (current != null) actionLabel.text = dialoguePanel.activeSelf ? "CONTINUAR" : "INTERAGIR\n" + current.DisplayName;

            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (dialoguePanel.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)))
            {
                dialoguePanel.SetActive(false);
                DialogueActive = false;
            }
        }

        public void Interact()
        {
            if (current == null)
            {
                Debug.Log("Interacao ignorada: nenhum alvo proximo.");
                return;
            }
            if (!dialoguePanel.activeSelf)
            {
                dialogueTarget = current;
                dialogueLine = 0;
                speakerText.text = current.DisplayName;
                dialogueText.text = current.GetLine(0);
                dialoguePanel.SetActive(true);
                DialogueActive = true;
                return;
            }

            dialogueLine++;
            if (dialogueLine < dialogueTarget.LineCount)
            {
                dialogueText.text = dialogueTarget.GetLine(dialogueLine);
                return;
            }

            dialogueTarget.Interact(player);
            // GDD §13: os cinco NPCs do tutorial produtivo (contracts/src/index.ts npcCodes) —
            // identificados pelo nome de exibição, mesmo padrão já usado pra Instrutora/Minerador.
            var npcCode = dialogueTarget.DisplayName switch
            {
                "Instrutora" => "instrutora",
                "Minerador" => "minerador",
                "Ferreiro" => "ferreiro",
                "Comerciante" => "comerciante",
                "Cronista" => "cronista",
                _ => null,
            };
            if (!string.IsNullOrEmpty(npcCode)) OnlineNpcTalkRequested?.Invoke(npcCode);
            if (dialogueTarget.AdvanceToStage >= 0) questStage = dialogueTarget.AdvanceToStage;
            dialogueTarget.Complete();
            dialoguePanel.SetActive(false);
            DialogueActive = false;
            dialogueTarget = null;
            RefreshObjective();
        }

        private WorldInteractable FindNearest()
        {
            if (player == null) return null;
            WorldInteractable nearest = null;
            var bestDistance = interactionRange * interactionRange;
            foreach (var candidate in WorldInteractable.Active)
            {
                if (candidate == null || !candidate.CanInteract(questStage)) continue;
                var distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        private void RefreshObjective()
        {
            objectiveText.text = questStage switch
            {
                0 => "CAPÍTULO I  •  O SUSSURRO SOB A MATA\nFale com a Anciã Mira na praça",
                1 => "CAPÍTULO I  •  O SUSSURRO SOB A MATA\nEncontre a Lumina perto da forja",
                2 => "CAPÍTULO I  •  O SUSSURRO SOB A MATA\nLeve a Lumina até a entrada da mina",
                3 => "CAPÍTULO I  •  O SUSSURRO SOB A MATA\nRetorne e conte à Anciã o que encontrou",
                _ => "CAPÍTULO I  •  CONCLUÍDO\nO chamado sob as pedras foi respondido"
            };
        }
    }
}
