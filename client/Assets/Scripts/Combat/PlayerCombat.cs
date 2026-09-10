using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Espectro.Prototype
{
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int attackDamage = 25;
        [SerializeField] private float attackRange = 2.25f;
        [SerializeField] private float attackCooldown = 0.55f;
        [SerializeField] private float dodgeDistance = 3.4f;
        [SerializeField] private float dodgeDuration = 0.28f;
        [SerializeField] private float dodgeCooldown = 0.9f;

        private int health;
        private float nextAttackAt;
        private Slider healthBar;
        private Text healthText;
        private Vector3 spawnPosition;
        private float nextDodgeAt;
        private bool isDodging;

        public int Health => health;
        public bool IsAlive => health > 0;
        public bool IsInvulnerable => isDodging;

        public void ConfigureHud(Slider bar, Text label)
        {
            healthBar = bar;
            healthText = label;
            RefreshHud();
        }

        private void Awake()
        {
            health = maxHealth;
            spawnPosition = transform.position;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                Attack();
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.Q))
                Dodge();
        }

        public void Attack()
        {
            if (!IsAlive || isDodging || Time.time < nextAttackAt) return;
            nextAttackAt = Time.time + attackCooldown;
            StartCoroutine(AttackPulse());

            EnemyCombatTarget best = null;
            var bestDistance = attackRange * attackRange;
            foreach (var enemy in EnemyCombatTarget.Active)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                var offset = enemy.transform.position - transform.position;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance > bestDistance || Vector3.Dot(transform.forward, offset.normalized) < 0.15f) continue;
                best = enemy;
                bestDistance = distance;
            }
            if (best != null)
            {
                best.TakeDamage(attackDamage, transform.forward);
                Camera.main?.GetComponent<ThirdPersonCamera>()?.AddTrauma(0.2f);
            }
        }

        public void Dodge()
        {
            if (!IsAlive || isDodging || Time.time < nextDodgeAt) return;
            nextDodgeAt = Time.time + dodgeCooldown;
            StartCoroutine(DodgeMovement());
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive || isDodging) return;
            health = Mathf.Max(0, health - amount);
            Camera.main?.GetComponent<ThirdPersonCamera>()?.AddTrauma(0.38f);
            RefreshHud();
            if (health == 0) StartCoroutine(Respawn());
        }

        private IEnumerator AttackPulse()
        {
            var slash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slash.name = "Golpe";
            Object.Destroy(slash.GetComponent<Collider>());
            slash.transform.SetParent(transform);
            slash.transform.localPosition = new Vector3(0f, 1.15f, 1.05f);
            slash.transform.localScale = new Vector3(1.5f, 0.12f, 0.55f);
            slash.transform.localRotation = Quaternion.Euler(0f, -30f, -12f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            slash.GetComponent<Renderer>().material = new Material(shader) { color = new Color(1f, 0.8f, 0.18f) };
            var elapsed = 0f;
            while (elapsed < 0.16f)
            {
                elapsed += Time.deltaTime;
                slash.transform.localRotation *= Quaternion.Euler(0f, 500f * Time.deltaTime, 0f);
                yield return null;
            }
            Object.Destroy(slash);
        }

        private IEnumerator DodgeMovement()
        {
            isDodging = true;
            var movement = GetComponent<PrototypePlayerController>();
            movement.enabled = false;
            var controller = GetComponent<CharacterController>();
            var direction = transform.forward;
            var elapsed = 0f;
            while (elapsed < dodgeDuration)
            {
                elapsed += Time.deltaTime;
                var normalizedTime = Mathf.Clamp01(elapsed / dodgeDuration);
                var speedCurve = 1f - normalizedTime * normalizedTime;
                controller.Move(direction * (dodgeDistance / dodgeDuration) * speedCurve * 1.55f * Time.deltaTime);
                transform.localScale = new Vector3(1f, Mathf.Lerp(0.72f, 1f, normalizedTime), 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            movement.enabled = true;
            isDodging = false;
        }

        private IEnumerator Respawn()
        {
            GetComponent<PrototypePlayerController>().enabled = false;
            yield return new WaitForSeconds(2f);
            var controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            health = maxHealth;
            GetComponent<PrototypePlayerController>().enabled = true;
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (healthBar != null) healthBar.value = (float)health / maxHealth;
            if (healthText != null) healthText.text = $"VIDA  {health}/{maxHealth}";
        }
    }
}
