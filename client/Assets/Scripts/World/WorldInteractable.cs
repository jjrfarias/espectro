using System.Collections.Generic;
using UnityEngine;

namespace Espectro.Prototype
{
    public sealed class WorldInteractable : MonoBehaviour
    {
        public static readonly List<WorldInteractable> Active = new();

        [SerializeField] private string displayName;
        [TextArea, SerializeField] private string message;
        [SerializeField] private Transform destination;
        [SerializeField] private string[] dialogueLines;
        [SerializeField] private bool collectOnComplete;
        [SerializeField] private int requiredStage = -1;
        [SerializeField] private int advanceToStage = -1;

        public string DisplayName => displayName;
        public string Message => message;
        public int LineCount => dialogueLines != null && dialogueLines.Length > 0 ? dialogueLines.Length : 1;
        public bool CollectOnComplete => collectOnComplete;
        public int AdvanceToStage => advanceToStage;

        public void Configure(string title, string dialogue, Transform teleportDestination = null)
        {
            displayName = title;
            message = dialogue;
            destination = teleportDestination;
            dialogueLines = new[] { dialogue };
        }

        public void ConfigureStory(string title, string[] lines, int requiredQuestStage, int nextQuestStage, bool collect = false)
        {
            displayName = title;
            dialogueLines = lines;
            message = lines != null && lines.Length > 0 ? lines[0] : string.Empty;
            requiredStage = requiredQuestStage;
            advanceToStage = nextQuestStage;
            collectOnComplete = collect;
        }

        public bool CanInteract(int questStage) => requiredStage < 0 || requiredStage == questStage;

        public string GetLine(int index)
        {
            if (dialogueLines == null || dialogueLines.Length == 0) return message;
            return dialogueLines[Mathf.Clamp(index, 0, dialogueLines.Length - 1)];
        }

        public void Interact(PrototypePlayerController player)
        {
            if (destination == null || player == null) return;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(destination.position, destination.rotation);
            controller.enabled = true;
        }

        public void Complete()
        {
            if (collectOnComplete) gameObject.SetActive(false);
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
    }
}
