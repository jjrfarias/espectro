using UnityEngine;

namespace Espectro.Prototype
{
    public sealed class ProceduralCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        private Vector3 previousPosition;

        public void Configure(Transform root, Transform armLeft, Transform armRight, Transform legLeft, Transform legRight)
        {
            visualRoot = root;
            leftArm = armLeft;
            rightArm = armRight;
            leftLeg = legLeft;
            rightLeg = legRight;
        }

        private void Start() => previousPosition = transform.position;

        private void LateUpdate()
        {
            var planarDelta = transform.position - previousPosition;
            planarDelta.y = 0f;
            var speed = planarDelta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            previousPosition = transform.position;

            var moving = Mathf.Clamp01(speed / 2f);
            var swing = Mathf.Sin(Time.time * 10f) * 32f * moving;
            leftArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            leftLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            visualRoot.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.05f * moving, 0f);
        }
    }
}
