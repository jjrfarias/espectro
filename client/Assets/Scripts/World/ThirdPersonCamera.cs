using UnityEngine;

namespace Espectro.Prototype
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 6.5f;
        [SerializeField] private float minDistance = 3.2f;
        [SerializeField] private float maxDistance = 9f;
        [SerializeField] private float yaw;
        [SerializeField] private float pitch = 22f;
        [SerializeField] private float orbitSensitivity = 0.16f;
        [SerializeField] private float zoomSensitivity = 0.015f;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private float lookHeight = 1.2f;
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private float baseFieldOfView = 60f;
        [SerializeField] private float sprintFieldOfView = 67f;

        private Vector3 velocity;
        private Camera cameraComponent;
        private PrototypePlayerController player;
        private Vector2 previousTouchPosition;
        private bool trackingTouch;
        private float trauma;

        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            RestoreSafeDefaults();

            if (target == null)
            {
                var localPlayer = FindAnyObjectByType<PrototypePlayerController>();
                target = localPlayer != null ? localPlayer.transform : null;
            }

            player = target != null ? target.GetComponent<PrototypePlayerController>() : null;
            SnapToTarget();
        }

        private void RestoreSafeDefaults()
        {
            // Cenas criadas antes da camera orbital nao possuem estes campos serializados.
            // Valores zerados deixam a camera parada ou colada ao personagem.
            if (distance < 2f) distance = 6.5f;
            if (minDistance < 1f) minDistance = 3.2f;
            if (maxDistance <= minDistance) maxDistance = 9f;
            if (pitch < 5f || pitch > 75f) pitch = 22f;
            if (lookHeight < 0.5f) lookHeight = 1.2f;
            if (collisionRadius < 0.05f) collisionRadius = 0.3f;
            if (baseFieldOfView < 30f) baseFieldOfView = 60f;
            if (sprintFieldOfView < baseFieldOfView) sprintFieldOfView = 67f;
        }

        public void SetTarget(Transform value)
        {
            target = value;
            player = target != null ? target.GetComponent<PrototypePlayerController>() : null;
            SnapToTarget();
        }

        private void Update()
        {
            ReadOrbitInput();
            if (cameraComponent != null)
            {
                var speedRatio = player != null ? Mathf.InverseLerp(4f, 6.5f, player.CurrentSpeed) : 0f;
                var desiredFov = Mathf.Lerp(baseFieldOfView, sprintFieldOfView, speedRatio);
                cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, desiredFov, 5f * Time.deltaTime);
            }
        }

        private void OnEnable()
        {
            velocity = Vector3.zero;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var lookPoint = target.position + Vector3.up * lookHeight;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var direction = rotation * Vector3.back;
            var desiredDistance = distance;
            // Ignora o CharacterController do próprio jogador; quando a esfera
            // começa dentro dele, a câmera recebia uma distância variável a cada
            // giro e produzia o tremor visível ao trocar de direção.
            var hits = Physics.SphereCastAll(lookPoint, collisionRadius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            foreach (var candidate in hits)
            {
                if (candidate.collider == null || (target != null && candidate.collider.transform.IsChildOf(target))) continue;
                if (candidate.distance < nearest) nearest = candidate.distance;
            }
            if (!float.IsPositiveInfinity(nearest))
                desiredDistance = Mathf.Max(minDistance * 0.45f, nearest - collisionRadius);
            var desired = lookPoint + direction * desiredDistance;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            if (trauma > 0f)
            {
                var strength = trauma * trauma * 0.22f;
                transform.position += Random.insideUnitSphere * strength;
                trauma = Mathf.MoveTowards(trauma, 0f, 2.8f * Time.deltaTime);
            }
            transform.LookAt(lookPoint);
        }

        private void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            var lookPoint = target.position + Vector3.up * lookHeight;
            transform.position = lookPoint + Quaternion.Euler(pitch, yaw, 0f) * Vector3.back * distance;
            transform.LookAt(lookPoint);
        }

        private void ReadOrbitInput()
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * orbitSensitivity * 55f;
                pitch -= Input.GetAxis("Mouse Y") * orbitSensitivity * 55f;
            }
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.7f, minDistance, maxDistance);

            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                if (touch.position.x < Screen.width * 0.45f)
                {
                    trackingTouch = false;
                    return;
                }
                if (touch.phase == TouchPhase.Began)
                {
                    previousTouchPosition = touch.position;
                    trackingTouch = true;
                }
                else if (trackingTouch && touch.phase == TouchPhase.Moved)
                {
                    var delta = touch.position - previousTouchPosition;
                    previousTouchPosition = touch.position;
                    yaw += delta.x * orbitSensitivity;
                    pitch -= delta.y * orbitSensitivity;
                }
            }
            else if (Input.touchCount == 2)
            {
                var first = Input.GetTouch(0);
                var second = Input.GetTouch(1);
                var previousDistance = ((first.position - first.deltaPosition) - (second.position - second.deltaPosition)).magnitude;
                var currentDistance = (first.position - second.position).magnitude;
                distance = Mathf.Clamp(distance - (currentDistance - previousDistance) * zoomSensitivity, minDistance, maxDistance);
                trackingTouch = false;
            }
            else
            {
                trackingTouch = false;
            }
            pitch = Mathf.Clamp(pitch, 8f, 58f);
        }
    }
}
