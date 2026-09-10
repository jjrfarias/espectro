using UnityEngine;
using UnityEngine.EventSystems;

namespace Espectro.Prototype
{
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform handle;
        [SerializeField] private PrototypePlayerController player;

        private RectTransform area;

        public void Configure(RectTransform joystickHandle, PrototypePlayerController target)
        {
            area = transform as RectTransform;
            handle = joystickHandle;
            player = target;
        }

        private void Awake()
        {
            area ??= transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateInput(eventData);

        public void OnDrag(PointerEventData eventData) => UpdateInput(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (player != null)
            {
                player.MobileInput = Vector2.zero;
            }
        }

        private void UpdateInput(PointerEventData eventData)
        {
            if (area == null || handle == null || player == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                return;
            }

            var radius = Mathf.Min(area.rect.width, area.rect.height) * 0.5f;
            var input = Vector2.ClampMagnitude(localPoint / radius, 1f);
            handle.anchoredPosition = input * radius * 0.55f;
            player.MobileInput = input;
        }
    }
}

