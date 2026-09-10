using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Espectro.Prototype
{
    public sealed class EnemyCombatTarget : MonoBehaviour
    {
        public static readonly List<EnemyCombatTarget> Active = new();

        [SerializeField] private int maxHealth = 75;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private int contactDamage = 12;

        private int health;
        private PlayerCombat player;
        private Renderer[] renderers;
        private Vector3 spawnPosition;
        private float nextDamageAt;
        private bool isAttacking;
        private Transform healthBar;
        private Transform healthFill;
        private Vector3 healthFillScale;

        public bool IsAlive => health > 0;

        private void Awake()
        {
            health = maxHealth;
            spawnPosition = transform.position;
            renderers = GetComponentsInChildren<Renderer>();
            CreateHealthBar();
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (!IsAlive) return;
            if (healthBar != null && Camera.main != null) healthBar.rotation = Camera.main.transform.rotation;
            player ??= FindAnyObjectByType<PlayerCombat>();
            if (player == null || !player.IsAlive) return;
            var offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > detectionRange * detectionRange) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset), 8f * Time.deltaTime);
            if (isAttacking) return;
            if (offset.magnitude > 1.35f)
                transform.position += offset.normalized * moveSpeed * Time.deltaTime;
            else if (Time.time >= nextDamageAt)
            {
                nextDamageAt = Time.time + 1.1f;
                StartCoroutine(AttackWindup());
            }
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (!IsAlive) return;
            health = Mathf.Max(0, health - amount);
            transform.position += direction * 0.35f;
            RefreshHealthBar();
            CreateImpactParticles(new Color(1f, 0.28f, 0.08f), 10);
            StartCoroutine(Flash());
            if (health == 0) StartCoroutine(DieAndRespawn());
        }

        private IEnumerator Flash()
        {
            foreach (var item in renderers) item.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            foreach (var item in renderers) item.material.color = new Color(0.48f, 0.12f, 0.12f);
        }

        private IEnumerator DieAndRespawn()
        {
            CreateImpactParticles(new Color(0.55f, 0.08f, 0.04f), 24);
            if (healthBar != null) healthBar.gameObject.SetActive(false);
            transform.localScale = new Vector3(1.25f, 0.18f, 1.25f);
            yield return new WaitForSeconds(3f);
            transform.position = spawnPosition;
            transform.localScale = Vector3.one;
            health = maxHealth;
            if (healthBar != null) healthBar.gameObject.SetActive(true);
            RefreshHealthBar();
        }

        private IEnumerator AttackWindup()
        {
            isAttacking = true;
            var originalScale = transform.localScale;
            transform.localScale = new Vector3(1.15f, 0.82f, 1.15f);
            foreach (var item in renderers) item.material.color = new Color(1f, 0.42f, 0.08f);
            yield return new WaitForSeconds(0.32f);
            transform.localScale = originalScale;
            foreach (var item in renderers) item.material.color = new Color(0.48f, 0.12f, 0.12f);
            if (player != null && player.IsAlive)
            {
                var offset = player.transform.position - transform.position;
                offset.y = 0f;
                transform.position += offset.normalized * 0.55f;
                if (offset.sqrMagnitude < 2.9f) player.TakeDamage(contactDamage);
            }
            isAttacking = false;
        }

        private void CreateHealthBar()
        {
            healthBar = new GameObject("Vida do Rastejante").transform;
            healthBar.SetParent(transform, false);
            healthBar.localPosition = new Vector3(0f, 1.5f, 0f);
            var background = CreateBarPart("Fundo", healthBar, new Vector3(1.65f, 0.16f, 0.06f), new Color(0.08f, 0.03f, 0.03f));
            background.localPosition = Vector3.zero;
            healthFill = CreateBarPart("Vida", healthBar, new Vector3(1.55f, 0.1f, 0.07f), new Color(0.9f, 0.08f, 0.06f));
            healthFill.localPosition = new Vector3(0f, 0f, -0.04f);
            healthFillScale = healthFill.localScale;
        }

        private static Transform CreateBarPart(string name, Transform parent, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localScale = scale;
            Object.Destroy(item.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            item.GetComponent<Renderer>().material = new Material(shader) { color = color };
            return item.transform;
        }

        private void RefreshHealthBar()
        {
            if (healthFill == null) return;
            var ratio = (float)health / maxHealth;
            healthFill.localScale = new Vector3(healthFillScale.x * ratio, healthFillScale.y, healthFillScale.z);
            healthFill.localPosition = new Vector3(-(healthFillScale.x - healthFill.localScale.x) * 0.5f, 0f, -0.04f);
        }

        private void CreateImpactParticles(Color color, int count)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var origin = transform.position + Vector3.up * 0.65f;
            for (var i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                shard.name = "Fragmento de Impacto";
                shard.transform.position = origin + Random.insideUnitSphere * 0.35f;
                shard.transform.localScale = Vector3.one * Random.Range(0.08f, 0.19f);
                Object.Destroy(shard.GetComponent<Collider>());
                shard.GetComponent<Renderer>().material = new Material(shader) { color = color };
                var direction = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
                shard.AddComponent<CombatShard>().Launch(direction * Random.Range(2f, 4.5f), Random.Range(0.3f, 0.65f));
            }
        }
    }
}
