using System;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Automated Gun Turret deployed by The Architect boss.
    /// Attacks player autonomously with rapid projectiles.
    /// Can be destroyed by player attacks (implements IDamageable).
    /// </summary>
    public class BossTurret : MonoBehaviour, IDamageable
    {
        [Header("Turret Stats")]
        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private float fireRate = 1.0f; // Seconds per shot
        [SerializeField] private float projectileSpeed = 6f;
        [SerializeField] private float projectileDamage = 10f;
        [SerializeField] private GameObject projectilePrefab;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.6f, 0.9f, 1f);
        [SerializeField] private Color overclockColor = new Color(1f, 0.4f, 0.1f, 1f);
        [SerializeField] private Color hurtColor = Color.white;

        private float currentHealth;
        private Transform target;
        private float nextFireTime;
        private bool isOverclocked;
        private float flashResetTime;
        private SkinAnimator skin;

        public event Action<BossTurret> OnTurretDestroyed;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.Minion);
            maxHealth = Mathf.Max(maxHealth, 150f);
            currentHealth = maxHealth;

            // the turret is a small copy of the Architect itself
            normalColor = Color.white;
            overclockColor = new Color(1f, 0.55f, 0.45f, 1f);
            hurtColor = new Color(1f, 0.35f, 0.35f, 1f);
            if (spriteRenderer != null) spriteRenderer.color = normalColor;
            skin = SkinLibrary.Apply(gameObject, "turret", 1f);
            if (skin != null && skin.baseWorldSize > 0.01f)
                SkinLibrary.Apply(gameObject, "turret", 1.1f / skin.baseWorldSize);   // small: about 1.1 world units tall, far below the boss
        }

        private void Start()
        {
            FindTarget();
            nextFireTime = Time.time + 0.5f; // Initial charge delay
            UpdateColor();
        }

        private void FindTarget()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        public void Setup(GameObject projPrefab, float damage = 10f, float speed = 6f)
        {
            if (projPrefab != null) projectilePrefab = projPrefab;
            projectileDamage = damage;
            projectileSpeed = speed;
        }

        public void SetOverclock(bool active)
        {
            isOverclocked = active;
            fireRate = active ? 0.5f : 1.0f; // 2x fire rate when overclocked!
            UpdateColor();
        }

        private void UpdateColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = isOverclocked ? overclockColor : normalColor;
            }
        }

        private void Update()
        {
            if (target == null) FindTarget();

            // Aim at target
            if (target != null)
            {
                Vector2 dir = (Vector2)(target.position - transform.position);
                if (dir.sqrMagnitude > 0.001f)
                {
                    if (skin == null)   // the Architect art stays upright; only plain sprites turn
                    {
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        transform.rotation = Quaternion.Euler(0, 0, angle);
                    }
                }

                // Fire projectiles
                if (Time.time >= nextFireTime)
                {
                    FireProjectile(dir.normalized);
                    nextFireTime = Time.time + fireRate;
                }
            }

            // Reset flash color
            if (Time.time >= flashResetTime && spriteRenderer != null)
            {
                UpdateColor();
            }
        }

        private void FireProjectile(Vector2 direction)
        {
            if (projectilePrefab == null) return;

            Vector3 spawnPos = transform.position + (Vector3)(direction * 0.5f);
            GameObject projObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

            EnemyProjectile proj = projObj.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                proj.Setup(direction, projectileDamage, projectileSpeed);
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Vector2.zero);
        }

        public void TakeDamage(float amount, Vector2 knockback)
        {
            currentHealth -= amount;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = hurtColor;
                flashResetTime = Time.time + 0.08f;
            }

            if (currentHealth <= 0f)
            {
                DestroyTurret();
            }
        }

        public void DestroyTurret()
        {
            OnTurretDestroyed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
