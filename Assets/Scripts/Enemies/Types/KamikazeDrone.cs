using Enemies.Core;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Kamikaze Drone (Floor 1 Room 5+):
    /// Low HP suicide drone that rushes the player and detonates on proximity or contact.
    /// Base HP 10, Weight 10.
    /// </summary>
    public class KamikazeDrone : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float explosionRadius = 1.8f;
        [SerializeField] private float baseExplosionDamage = 15f;
        [SerializeField] private float detonateDistance = 1.2f;

        private EnemyBase enemyBase;
        private EnemyHealth health;
        private bool hasDetonated = false;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            health = GetComponent<EnemyHealth>();
        }

        private void Update()
        {
            if (hasDetonated || (health != null && health.IsDead)) return;

            if (enemyBase != null && enemyBase.Target != null)
            {
                float dist = Vector2.Distance(transform.position, enemyBase.Target.position);
                if (dist <= detonateDistance)
                {
                    Detonate();
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (hasDetonated) return;
            if (collision.gameObject.CompareTag("Player"))
            {
                Detonate();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (hasDetonated) return;
            if (collision.CompareTag("Player"))
            {
                Detonate();
            }
        }

        public void Detonate()
        {
            if (hasDetonated) return;
            hasDetonated = true;

            if (health == null) health = GetComponent<EnemyHealth>();
            if (enemyBase == null) enemyBase = GetComponent<EnemyBase>();

            // บาลานซ์ 8: ดรอป 1 EXP เฉพาะเมื่อถูกผู้เล่นฆ่า ไม่ดรอปถ้าวิ่งชนระเบิดตัวเอง
            if (health != null)
            {
                health.SetExpDrop(0);
            }

            float damageMult = enemyBase != null ? enemyBase.DifficultyMultiplier : 1f;
            float totalDamage = baseExplosionDamage * damageMult;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].CompareTag("Player"))
                {
                    PlayerHealth ph = hits[i].GetComponent<PlayerHealth>();
                    if (ph != null)
                    {
                        ph.TakeDamage(totalDamage);
                    }
                }
            }

            Debug.Log($"[{name}] Kamikaze Drone detonated! Dealing {totalDamage} damage in {explosionRadius}m radius.");

            if (health != null && !health.IsDead)
            {
                health.TakeDamage(99999f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
