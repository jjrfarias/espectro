using UnityEngine;

namespace Espectro.Prototype
{
    public sealed class PlayerAttackFeedback : MonoBehaviour
    {
        private float remaining;
        private LineRenderer arc;

        public static PlayerAttackFeedback Attach(Transform player)
        {
            var feedback = player.GetComponent<PlayerAttackFeedback>();
            return feedback ?? player.gameObject.AddComponent<PlayerAttackFeedback>();
        }

        public void Play()
        {
            remaining = 0.18f;
            arc.enabled = true;
        }

        private void Awake()
        {
            var effect = new GameObject("Arco do Golpe");
            effect.transform.SetParent(transform, false);
            arc = effect.AddComponent<LineRenderer>();
            arc.useWorldSpace = false;
            arc.positionCount = 9;
            arc.widthMultiplier = 0.07f;
            arc.material = new Material(Shader.Find("Sprites/Default"));
            for (var i = 0; i < 9; i++)
            {
                var angle = Mathf.Lerp(-65f, 65f, i / 8f) * Mathf.Deg2Rad;
                arc.SetPosition(i, new Vector3(Mathf.Sin(angle), 0.85f, Mathf.Cos(angle)));
            }
            arc.enabled = false;
        }

        private void Update()
        {
            if (remaining <= 0f) return;
            remaining -= Time.deltaTime;
            var color = new Color(1f, 0.78f, 0.25f, Mathf.Clamp01(remaining / 0.18f));
            arc.startColor = color;
            arc.endColor = color;
            if (remaining <= 0f) arc.enabled = false;
        }
    }
}
