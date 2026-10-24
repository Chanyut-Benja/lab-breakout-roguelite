using System.Collections;
using Enemies.Core;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Kamikaze Drone (Balance Sheet Section 8):
    /// Rushes close to the player, waits for a 0.5s delay with visual windup warning,
    /// and then detonates in a deadly AoE explosion.
    /// If destroyed by player during the delay, drops EXP & Credit normally.
    /// </summary>
    public class KamikazeDrone : MonoBehaviour
    {
        [Header("Kamikaze Drone Base Stats (Balance Sheet)")]
        [SerializeField] private float baseDroneHP = 10f;
        [SerializeField] private int baseExpDrop = 1;

        [Header("Explosion Settings")]
        [SerializeField] private float explosionRadius = 2.2f;
        [SerializeField] private float baseExplosionDamage = 18f;
        [SerializeField] private float detonateDistance = 1.5f;
        [SerializeField] private float fuseDuration = 0.5f; // หน่วงเวลา 0.5s

        private EnemyBase enemyBase;
        private EnemyHealth health;
        private SpriteRenderer spriteRenderer;
        private Color originalColor = Color.white;
        private bool isPriming = false;
        private bool hasDetonated = false;
        private Coroutine fuseCoroutine;

        public bool IsPriming => isPriming;
        public bool HasDetonated => hasDetonated;
        public float FuseDuration => fuseDuration;
        public float ExplosionRadius => explosionRadius;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            health = GetComponent<EnemyHealth>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }

        private void Start()
        {
            if (health != null)
            {
                float mult = enemyBase != null ? enemyBase.HPMultiplier : 1f;
                health.SetExactMaxHealth(baseDroneHP * mult);
                health.SetExpDrop(baseExpDrop);
            }
        }

        private void OnEnable()
        {
            isPriming = false;
            hasDetonated = false;
            fuseCoroutine = null;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        private void Update()
        {
            if (hasDetonated || isPriming || (health != null && health.IsDead)) return;

            if (enemyBase != null && enemyBase.Target != null)
            {
                float dist = Vector2.Distance(transform.position, enemyBase.Target.position);
                if (dist <= detonateDistance)
                {
                    StartFuseCountdown();
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (hasDetonated || isPriming) return;
            if (collision.gameObject.CompareTag("Player"))
            {
                StartFuseCountdown();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (hasDetonated || isPriming) return;
            if (collision.CompareTag("Player"))
            {
                StartFuseCountdown();
            }
        }

        /// <summary>
        /// เริ่มต้นหน่วงเวลา 0.5 วินาที พร้อมแสดงสัญญาณเตือนก่อนระเบิด AoE
        /// </summary>
        public void StartFuseCountdown()
        {
            if (isPriming || hasDetonated) return;
            if (health != null && health.IsDead) return;

            isPriming = true;
            if (fuseCoroutine != null) StopCoroutine(fuseCoroutine);
            fuseCoroutine = StartCoroutine(FuseRoutine());
        }

        private IEnumerator FuseRoutine()
        {
            float elapsed = 0f;
            Color warningColor = new Color(1f, 0.15f, 0.15f, 1f);

            // หน่วงเวลา 0.5s พร้อมกระพริบสีเตือนความถี่สูง
            while (elapsed < fuseDuration)
            {
                if (health != null && health.IsDead)
                {
                    // ผู้เล่นกำจัดโดรนก่อนหน่วงเวลาเสร็จสิ้น -> ยกเลิกการระเบิดพลีชีพ
                    yield break;
                }

                elapsed += Time.deltaTime;
                if (spriteRenderer != null)
                {
                    float flashPhase = Mathf.PingPong(elapsed * 12f, 1f);
                    spriteRenderer.color = Color.Lerp(originalColor, warningColor, flashPhase);
                }
                yield return null;
            }

            if (health != null && health.IsDead)
            {
                yield break;
            }

            Detonate();
        }

        public void Detonate()
        {
            if (hasDetonated) return;
            hasDetonated = true;

            if (health == null) health = GetComponent<EnemyHealth>();
            if (enemyBase == null) enemyBase = GetComponent<EnemyBase>();

            // หากระเบิดตัวเอง ไม่ดรอป EXP/Credit (ดรอปเฉพาะเมื่อผู้เล่นสังหารก่อนระเบิด)
            if (health != null)
            {
                health.SetExpDrop(0);
            }

            float damageMult = enemyBase != null ? enemyBase.DifficultyMultiplier : 1f;
            float totalDamage = baseExplosionDamage * damageMult;

            // สร้างรัศมีความเสียหายแบบ AoE
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].CompareTag("Player"))
                {
                    PlayerHealth ph = hits[i].GetComponent<PlayerHealth>() ?? hits[i].GetComponentInChildren<PlayerHealth>();
                    if (ph != null)
                    {
                        ph.TakeDamage(totalDamage);
                    }
                    else
                    {
                        IDamageable dmg = hits[i].GetComponent<IDamageable>();
                        dmg?.TakeDamage(totalDamage);
                    }
                }
            }

            // Spawn explosion effect if available
            GameObject explosionVFX = Resources.Load<GameObject>("Prefabs/ExplosionVFX");
            if (explosionVFX != null)
            {
                Instantiate(explosionVFX, transform.position, Quaternion.identity);
            }

            Debug.Log($"[{name}] Kamikaze Drone detonated! Dealing {totalDamage} AoE damage in {explosionRadius}m radius.");

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
