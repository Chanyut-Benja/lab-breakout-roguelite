using System;
using System.Collections;
using UnityEngine;

namespace Enemies.Core
{
    /// <summary>
    /// Handles enemy health, hurt flash, knockback, Poison DoT, Stun status forwarding, damage reception via IDamageable,
    /// and invokes OnEnemyDied event for external XP/resource systems.
    /// Fully pool-friendly: resets values in OnEnable and Initialize.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("References")]
        [SerializeField] private EnemyBase enemyBase;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D rb;

        [Header("Hurt Flash Settings")]
        [SerializeField] private Color hurtFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private float flashDuration = 0.08f;

        // Runtime Stats
        private EnemyData data;
        private float maxHealth = 10f;
        private float currentHealth = 10f;
        private float difficultyMultiplier = 1f;
        private bool isDead = false;
        private int customExpDrop = -1;

        // Status Effects: Poison DoT, Stun, Slow & Armor Reduction (Flame Ring)
        private float poisonDps = 0f;
        private float poisonRemainingTime = 0f;
        private float poisonTickAccumulator = 0f;
        private float armorReductionPercent = 0f;
        private float armorReductionRemainingTime = 0f;

        private Color originalSpriteColor = Color.white;
        private Coroutine flashCoroutine;

        // Events per spec contract
        public static event Action<EnemyData, Vector2> OnEnemyDied;
        public static event Action<EnemyData, float, Vector2> OnEnemyDamaged;
        public static event Action<GameObject, Vector2, bool> OnAnyEnemyDiedWithPoisonState; // (deadEnemy, position, wasPoisoned)

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<EnemyHealth> OnDied;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public bool IsPoisoned => poisonRemainingTime > 0f;
        public bool IsStunned => enemyBase != null && enemyBase.IsStunned;
        public float DamageTakenMultiplier => armorReductionRemainingTime > 0f ? (1f + armorReductionPercent) : 1f;
        public bool IsInvulnerable { get; set; } = false;

        private void Awake()
        {
            if (enemyBase == null) enemyBase = GetComponent<EnemyBase>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();

            if (spriteRenderer != null)
            {
                originalSpriteColor = spriteRenderer.color;
            }
        }

        private void OnEnable()
        {
            isDead = false;
            IsInvulnerable = false;
            customExpDrop = -1;
            ClearStatusEffects();

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalSpriteColor;
            }

            ResetHealth();
        }

        private void Update()
        {
            if (isDead) return;

            if (armorReductionRemainingTime > 0f)
            {
                armorReductionRemainingTime -= Time.deltaTime;
                if (armorReductionRemainingTime <= 0f)
                {
                    armorReductionRemainingTime = 0f;
                    armorReductionPercent = 0f;
                }
            }

            if (poisonRemainingTime <= 0f) return;

            poisonRemainingTime -= Time.deltaTime;
            poisonTickAccumulator += Time.deltaTime;

            // ลดเลือดจากสถานะพิษทุก 0.25 วินาที (หรือเมื่อจบสถานะ) ตามค่า DPS (5/s -> 12/s)
            if (poisonTickAccumulator >= 0.25f || poisonRemainingTime <= 0f)
            {
                float tickDamage = poisonDps * poisonTickAccumulator;
                poisonTickAccumulator = 0f;
                ApplyPoisonTickDamage(tickDamage);
            }

            if (poisonRemainingTime <= 0f)
            {
                poisonDps = 0f;
                poisonRemainingTime = 0f;
                if (spriteRenderer != null && flashCoroutine == null)
                {
                    spriteRenderer.color = originalSpriteColor;
                }
            }
        }

        public void Initialize(EnemyBase owner, EnemyData enemyData, float difficulty)
        {
            this.enemyBase = owner;
            this.data = enemyData;
            this.difficultyMultiplier = difficulty;
            ClearStatusEffects();
            ResetHealth();
        }

        public void ClearStatusEffects()
        {
            poisonDps = 0f;
            poisonRemainingTime = 0f;
            poisonTickAccumulator = 0f;
            armorReductionPercent = 0f;
            armorReductionRemainingTime = 0f;
        }

        private void ResetHealth()
        {
            if (data != null)
            {
                maxHealth = data.MaxHP * difficultyMultiplier;
            }
            else
            {
                maxHealth = 10f * difficultyMultiplier;
            }

            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// กำหนดค่า Max HP พื้นฐานที่แน่นอน (เช่นสำหรับ Boss ตามตารางบาลานซ์ 8.3)
        /// </summary>
        public void SetExactMaxHealth(float exactMaxHP)
        {
            maxHealth = Mathf.Max(1f, exactMaxHP);
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetExpDrop(int amount)
        {
            customExpDrop = Mathf.Max(0, amount);
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// ทำให้ศัตรูติดสถานะพิษ (Poison DoT) ลดเลือดต่อเนื่องเป็นเวลา duration วินาที
        /// </summary>
        public void ApplyPoison(float dps, float duration = 3.0f)
        {
            if (isDead || dps <= 0f || duration <= 0f) return;

            poisonDps = Mathf.Max(poisonDps, dps);
            poisonRemainingTime = Mathf.Max(poisonRemainingTime, duration);

            if (spriteRenderer != null && flashCoroutine == null)
            {
                spriteRenderer.color = Color.Lerp(originalSpriteColor, new Color(0.35f, 1f, 0.45f, 1f), 0.55f);
            }
        }

        /// <summary>
        /// ทำให้ศัตรูติดสถานะ Stun หยุดการเคลื่อนที่และการโจมตีชั่วคราว (เช่นจาก Mind Shock เมื่อติดคริติคอล)
        /// </summary>
        public void ApplyStun(float duration = 1.0f)
        {
            if (isDead || duration <= 0f) return;

            if (enemyBase != null)
            {
                enemyBase.ApplyStun(duration);
            }
        }

        public void ApplySlow(float slowPercent, float duration)
        {
            if (isDead || slowPercent <= 0f || duration <= 0f) return;
            if (enemyBase != null)
            {
                enemyBase.ApplySlow(slowPercent, duration);
            }
        }

        public void ApplyArmorReduction(float reductionPercent, float duration)
        {
            if (isDead || reductionPercent <= 0f || duration <= 0f) return;
            armorReductionPercent = Mathf.Max(armorReductionPercent, reductionPercent);
            armorReductionRemainingTime = Mathf.Max(armorReductionRemainingTime, duration);
        }

        private void ApplyPoisonTickDamage(float amount)
        {
            if (isDead || IsInvulnerable || amount <= 0f) return;

            float finalAmount = amount * DamageTakenMultiplier;
            currentHealth -= finalAmount;
            currentHealth = Mathf.Max(0f, currentHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnEnemyDamaged?.Invoke(data, finalAmount, transform.position);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Vector2.zero);
        }

        public void TakeDamage(float amount, Vector2 knockback)
        {
            if (isDead || IsInvulnerable || amount <= 0f) return;

            float finalAmount = amount * DamageTakenMultiplier;
            currentHealth -= finalAmount;
            currentHealth = Mathf.Max(0f, currentHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnEnemyDamaged?.Invoke(data, finalAmount, transform.position);

            // Apply Hurt Flash
            TriggerHurtFlash();

            // Apply Knockback (scaled by resistance)
            if (knockback != Vector2.zero && rb != null)
            {
                float resistance = data != null ? data.KnockbackResistance : 0f;
                Vector2 effectiveKnockback = knockback * Mathf.Clamp01(1f - resistance);
                rb.AddForce(effectiveKnockback, ForceMode2D.Impulse);
            }

            // Play Hit SFX
            if (data != null && data.HitSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.HitSFX);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void TriggerHurtFlash()
        {
            if (spriteRenderer == null || !gameObject.activeInHierarchy) return;

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(HurtFlashRoutine());
        }

        private IEnumerator HurtFlashRoutine()
        {
            spriteRenderer.color = hurtFlashColor;
            yield return new WaitForSeconds(flashDuration);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = IsPoisoned
                    ? Color.Lerp(originalSpriteColor, new Color(0.35f, 1f, 0.45f, 1f), 0.55f)
                    : originalSpriteColor;
            }
            flashCoroutine = null;
        }

        private void Die()
        {
            if (isDead) return;
            bool wasPoisonedBeforeDeath = poisonRemainingTime > 0f || poisonDps > 0f;
            isDead = true;
            ClearStatusEffects();

            Debug.Log($"[{gameObject.name}] Enemy died! (HP reached 0)");

            // แจ้งเตือนระบบ Epidemic Outbreak Resonance หากมอนสเตอร์ติดพิษตาย
            OnAnyEnemyDiedWithPoisonState?.Invoke(gameObject, transform.position, wasPoisonedBeforeDeath);

            // Spawn Death VFX
            if (data != null && data.DeathVFX != null)
            {
                Instantiate(data.DeathVFX, transform.position, Quaternion.identity);
            }

            // Play Die SFX
            if (data != null && data.DieSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.DieSFX);
            }

            // Spawn EXP drop if configured
            if (data != null && data.ExpPrefab != null)
            {
                int expValue = customExpDrop >= 0 ? customExpDrop : data.ExpDrop;
                if (expValue > 0)
                {
                    GameObject expObj = Instantiate(data.ExpPrefab, transform.position, Quaternion.identity);
                    EXPCrystal crystal = expObj.GetComponent<EXPCrystal>();
                    if (crystal != null)
                    {
                        crystal.SetExpValue(expValue);
                    }
                }
            }

            // Fire contract event for XP/resource listeners
            OnEnemyDied?.Invoke(data, transform.position);
            OnDied?.Invoke(this);

            // Inform EnemyBase FSM to enter Dead state
            if (enemyBase != null)
            {
                enemyBase.OnDeath();
            }
            else
            {
                gameObject.SetActive(false);
            }

            GameOverManager.Instance?.RecordEnemyKilled();
        }
    }
}
