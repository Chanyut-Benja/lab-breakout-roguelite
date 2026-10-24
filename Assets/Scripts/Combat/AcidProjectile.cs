using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Acid projectile fired by Spitter enemies (Balance Sheet Section 8).
    /// Deals direct impact damage to the player and spawns an acid pool on impact or expiration.
    /// </summary>
    public class AcidProjectile : EnemyProjectile
    {
        [Header("Acid Projectile Settings")]
        [SerializeField] private float acidDamage = 8f;
        [SerializeField] private float acidSpeed = 6f;
        [SerializeField] private bool spawnsAcidPoolOnImpact = true;
        [SerializeField] private GameObject acidPoolPrefab;

        private void Awake()
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.2f, 1f, 0.25f, 1f); // Neon Acid Green
            }
        }

        private void Start()
        {
            Setup(transform.right, acidDamage, acidSpeed);
        }

        public void ConfigureAcid(Vector2 direction, float damage, float speed, GameObject poolPrefab = null)
        {
            this.acidDamage = damage;
            this.acidSpeed = speed;
            if (poolPrefab != null) this.acidPoolPrefab = poolPrefab;

            Setup(direction, damage, speed);

            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.2f, 1f, 0.25f, 1f);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision == null) return;

            if (IsReflected)
            {
                // Reflected behavior handled in base EnemyProjectile
                return;
            }

            if (collision.CompareTag("Player"))
            {
                IDamageable playerDamageable = collision.GetComponent<IDamageable>();
                if (playerDamageable != null)
                {
                    playerDamageable.TakeDamage(acidDamage);
                }

                if (spawnsAcidPoolOnImpact)
                {
                    SpawnAcidPool(transform.position);
                }

                Destroy(gameObject);
            }
        }

        private void SpawnAcidPool(Vector3 position)
        {
            if (acidPoolPrefab != null)
            {
                Instantiate(acidPoolPrefab, position, Quaternion.identity);
                return;
            }

            // Fallback: Check if AcidPool prefab exists in Resources
            GameObject pool = Resources.Load<GameObject>("Prefabs/AcidPool");
            if (pool != null)
            {
                Instantiate(pool, position, Quaternion.identity);
            }
        }
    }
}

public class AcidProjectile : Combat.AcidProjectile { }
