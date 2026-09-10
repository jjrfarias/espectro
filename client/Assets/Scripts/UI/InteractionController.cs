using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    public sealed class InteractionController : MonoBehaviour
    {
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
        }

        private void Update()
        {
            current = dialoguePanel.activeSelf ? dialogueTarget : FindNearest();
            actionButton.gameObject.SetActive(current != null);
            if (current != null) actionLabel.text = dialoguePanel.activeSelf ? "CONTINUAR" : "INTERAGIR\n" + current.DisplayName;

            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (dialoguePanel.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)))
                dialoguePanel.SetActive(false);
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
                return;
            }

            dialogueLine++;
            if (dialogueLine < dialogueTarget.LineCount)
            {
                dialogueText.text = dialogueTarget.GetLine(dialogueLine);
                return;
            }

            dialogueTarget.Interact(player);
            if (dialogueTarget.AdvanceToStage >= 0) questStage = dialogueTarget.AdvanceToStage;
            dialogueTarget.Complete();
            dialoguePanel.SetActive(false);
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
                0 => "OBJETIVO: Fale com a Ancia Mira na praca",
                1 => "OBJETIVO: Encontre a Lumina perto da forja",
                2 => "OBJETIVO: Leve a Lumina ate a entrada da mina",
                3 => "OBJETIVO: Retorne e conte a Ancia o que encontrou",
                _ => "OBJETIVO CONCLUIDO: O chamado sob as pedras"
            };
        }
    }
}
