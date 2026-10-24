using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Lingering toxic acid puddle deployed by The Doctor boss.
    /// Deals periodic damage to the player when standing inside.
    /// Fades out and destroys itself after duration expires.
    /// </summary>
    public class AcidPool : MonoBehaviour
    {
        [Header("Hazard Settings")]
        [SerializeField] private float lifetime = 6.0f;
        [SerializeField] private float damagePerTick = 6.0f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private float radius = 1.8f;

        private float spawnTime;
        private float nextTickTime;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.Minion);
        }

        private void Start()
        {
            spawnTime = Time.time;
            nextTickTime = Time.time;
        }

        public void Setup(float customDamage = 6f, float customLifetime = 6f, float customRadius = 1.8f)
        {
            damagePerTick = customDamage;
            lifetime = customLifetime;
            radius = customRadius;

            transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        }

        private void Update()
        {
            float elapsed = Time.time - spawnTime;

            // Fade out during last 1.5 seconds
            if (elapsed >= (lifetime - 1.5f) && spriteRenderer != null)
            {
                float remainingRatio = (lifetime - elapsed) / 1.5f;
                Color col = spriteRenderer.color;
                col.a = Mathf.Clamp01(remainingRatio * 0.7f);
                spriteRenderer.color = col;
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (collision.CompareTag("Player") && Time.time >= nextTickTime)
            {
                IDamageable damageable = collision.GetComponent<IDamageable>() ?? collision.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damagePerTick);
                    nextTickTime = Time.time + tickInterval;
                }
            }
        }
    }
}
