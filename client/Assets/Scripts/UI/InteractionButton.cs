using UnityEngine;
using UnityEngine.EventSystems;

namespace Espectro.Prototype
{
    public sealed class InteractionButton : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private InteractionController interaction;

        public void Configure(InteractionController controller) => interaction = controller;

        public void OnPointerDown(PointerEventData eventData)
        {
            interaction?.Interact();
        }
    }
}
