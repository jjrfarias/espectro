using UnityEngine;

namespace Espectro.Prototype
{
    public sealed class CombatShard : MonoBehaviour
    {
        private Vector3 velocity;
        private float lifetime;
        private float age;

        public void Launch(Vector3 initialVelocity, float duration)
        {
            velocity = initialVelocity;
            lifetime = duration;
        }

        private void Update()
        {
            age += Time.deltaTime;
            velocity += Vector3.down * 8f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(280f * Time.deltaTime, 190f * Time.deltaTime, 110f * Time.deltaTime);
            transform.localScale *= Mathf.Pow(0.05f, Time.deltaTime / Mathf.Max(lifetime, 0.01f));
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
