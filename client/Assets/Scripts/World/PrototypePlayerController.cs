using UnityEngine;

namespace Espectro.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float sprintMultiplier = 1.4f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float deceleration = 24f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private Transform cameraTransform;
        private float verticalVelocity;
        private Vector3 planarVelocity;
        private Vector3 safePosition;

        public Vector2 MobileInput { get; set; }
        public float SpeedLimit { get; set; } = float.PositiveInfinity;
        public Vector3 LastWorldVelocity { get; private set; }
        public float CurrentSpeed => planarVelocity.magnitude;
        public bool IsMoving => CurrentSpeed > 0.1f;

        // Espelha a intenção de movimento do quadro atual (câmera-relativa), para o cliente de
        // rede (Espectro.Network) enviar como movement.input sem duplicar a leitura de input.
        public Vector2 LastMoveInput { get; private set; }
        public float FacingYDegrees => transform.eulerAngles.y;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
            safePosition = transform.position;

            // As formas do personagem sao apenas visuais. Colliders filhos entram em conflito
            // com o CharacterController e podem faze-lo atravessar o piso.
            foreach (var childCollider in GetComponentsInChildren<Collider>(true))
            {
                if (childCollider != controller)
                    Destroy(childCollider);
            }
        }

        private void OnEnable()
        {
            LastWorldVelocity = Vector3.zero;
            planarVelocity = Vector3.zero;
            verticalVelocity = -2f;
            safePosition = transform.position;
        }

        private void Update()
        {
            if (transform.position.y < -5f)
            {
                controller.enabled = false;
                transform.position = safePosition;
                controller.enabled = true;
                planarVelocity = Vector3.zero;
                verticalVelocity = -2f;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            var keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            var input = MobileInput.sqrMagnitude > keyboard.sqrMagnitude ? MobileInput : keyboard;
            input = Vector2.ClampMagnitude(input, 1f);
            LastMoveInput = input;

            var forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            var right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var movement = forward * input.y + right * input.x;
            if (movement.sqrMagnitude > 0.001f)
            {
                var desiredRotation = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    rotationSpeed * Time.deltaTime);
            }

            var wantsSprint = Input.GetKey(KeyCode.LeftShift) || MobileInput.magnitude > 0.92f;
            var targetSpeed = moveSpeed * (wantsSprint ? sprintMultiplier : 1f) * input.magnitude;
            targetSpeed = Mathf.Min(targetSpeed, SpeedLimit);
            var targetVelocity = movement.normalized * targetSpeed;
            var changeRate = targetVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, changeRate * Time.deltaTime);

            verticalVelocity = controller.isGrounded && verticalVelocity < 0f
                ? -2f
                : verticalVelocity + gravity * Time.deltaTime;

            var velocity = planarVelocity;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
            var appliedVelocity = controller.velocity;
            LastWorldVelocity = new Vector3(appliedVelocity.x, 0f, appliedVelocity.z);

            if (controller.isGrounded && transform.position.y > -0.5f)
                safePosition = transform.position;
        }
    }
}
