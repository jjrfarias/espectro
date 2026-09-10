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
        private int tutorialStage;

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
            tutorialStage = 0;
            RefreshObjective();
        }

        // Chamado pela sessão online quando o servidor confirma o tutorial persistente
        // (tutorial.snapshot). tutorialStage é a contagem de completedSteps — 6 passos reais em
        // contracts/src/index.ts tutorialStepCodes, não 4 (ver NetworkSession.HandleTutorialSnapshot).
        public void ApplyRemoteTutorialStage(int stage)
        {
            tutorialStage = Mathf.Clamp(stage, 0, 6);
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
            if (dialogueTarget.AdvanceToStage >= 0) tutorialStage = dialogueTarget.AdvanceToStage;
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
                if (candidate == null || !candidate.CanInteract(tutorialStage)) continue;
                var distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        // GDD-MVP.md §4 "Jornada da primeira sessão" (passos 5-10) e §13 (tutorialStepCodes em
        // contracts/src/index.ts): falou_instrutora, derrotou_criatura, falou_minerador,
        // extraiu_minerio, fundiu_lingote, vendeu_lingote — nessa ordem.
        private void RefreshObjective()
        {
            objectiveText.text = tutorialStage switch
            {
                0 => "PRIMEIRA JORNADA\nFale com a Instrutora na praça",
                1 => "PRIMEIRA JORNADA\nEnfrente uma criatura na floresta",
                2 => "PRIMEIRA JORNADA\nFale com o Minerador perto da mina",
                3 => "PRIMEIRA JORNADA\nExtraia minério de ferro na mina",
                4 => "PRIMEIRA JORNADA\nFunda um lingote na forja com o Ferreiro",
                5 => "PRIMEIRA JORNADA\nVenda o lingote ao Comerciante",
                _ => "PRIMEIRA JORNADA  •  CONCLUÍDA\nConsulte o mural com o Cronista"
            };
        }
    }
}
