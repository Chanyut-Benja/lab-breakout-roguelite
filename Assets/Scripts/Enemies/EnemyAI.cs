using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour, IDamageable
{
    public enum State { Patrol, Chase, Attack }

    [Header("Enemy Classification")]
    [SerializeField] private bool isBoss = false;
    [SerializeField] private bool isPulseBoss = false;
    [SerializeField] private float pulseWindup = 0.7f;

    [Header("FSM State Settings")]
    public State currentState = State.Patrol;
    [SerializeField] private float chaseRange = 10f;
    [SerializeField] private float attackRange = 1.5f;

    [Header("Stats")]
    [SerializeField] private float maxHealth = 20f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int expAmount = 5;

    [Header("VFX & SFX Prefabs")]
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private GameObject expPrefab;

    private float baseMaxHealth;
    private float baseMoveSpeed;
    private float baseDamage;
    private bool hasCachedBaseStats = false;
    private float currentHealth;
    private float lastAttackTime;
    private Transform player;
    private Vector2 patrolTarget;
    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;
    private static Sprite pulseRingSprite;

    // Status Effects: Stun (Mind Shock), Poison DoT (Bio-Needler), Slow & Armor Reduction
    private float stunRemainingTimer = 0f;
    private float poisonDps = 0f;
    private float poisonRemainingTime = 0f;
    private float poisonTickAccumulator = 0f;
    private float slowRemainingTimer = 0f;
    private float currentSlowPercent = 0f;
    private float armorReductionPercent = 0f;
    private float armorReductionRemainingTime = 0f;

    public bool IsStunned => stunRemainingTimer > 0f;
    public bool IsPoisoned => poisonRemainingTime > 0f;
    public float SlowMultiplier => slowRemainingTimer > 0f ? Mathf.Clamp01(1f - currentSlowPercent) : 1f;
    public float DamageTakenMultiplier => armorReductionRemainingTime > 0f ? (1f + armorReductionPercent) : 1f;

    public static event System.Action<GameObject, Vector2, bool> OnLegacyEnemyDiedWithPoisonState;

    private void CacheBaseStatsIfNeeded()
    {
        if (hasCachedBaseStats) return;
        baseMaxHealth = maxHealth;
        baseMoveSpeed = moveSpeed;
        baseDamage = damage;
        hasCachedBaseStats = true;
    }

    public void MarkAsBoss()
    {
        isBoss = true;
    }

    public void ConfigureBackupBoss(float health, float attackDamage, bool pulseAttack)
    {
        isBoss = true;
        isPulseBoss = pulseAttack;
        maxHealth = health;
        damage = attackDamage;
        chaseRange = 14f;
        attackRange = pulseAttack ? 2.5f : 1.5f;
        moveSpeed = 2.8f;
        hasCachedBaseStats = false;
        CacheBaseStatsIfNeeded();
    }

    private void Awake()
    {
        CacheBaseStatsIfNeeded();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = isBoss ? ProceduralSprites.Boss : ProceduralSprites.Minion;
            originalColor = spriteRenderer.color;
        }
        FindPlayer();
        GetNewPatrolPoint();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    private void Update()
    {
        if (slowRemainingTimer > 0f)
        {
            slowRemainingTimer -= Time.deltaTime;
            if (slowRemainingTimer <= 0f)
            {
                slowRemainingTimer = 0f;
                currentSlowPercent = 0f;
            }
        }

        if (armorReductionRemainingTime > 0f)
        {
            armorReductionRemainingTime -= Time.deltaTime;
            if (armorReductionRemainingTime <= 0f)
            {
                armorReductionRemainingTime = 0f;
                armorReductionPercent = 0f;
            }
        }

        UpdatePoisonStatus();

        if (currentHealth <= 0f) return;

        // หากติดสถานะ Stun (เช่นจาก Mind Shock ของ N-02 Psionic) ให้หยุดการเคลื่อนที่และการโจมตีชั่วคราว
        if (stunRemainingTimer > 0f)
        {
            stunRemainingTimer -= Time.deltaTime;
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        if (player == null)
        {
            FindPlayer();
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        switch (currentState)
        {
            case State.Patrol:
                PatrolBehavior(distanceToPlayer);
                break;
            case State.Chase:
                ChaseBehavior(distanceToPlayer);
                break;
            case State.Attack:
                AttackBehavior(distanceToPlayer);
                break;
        }
    }

    private void UpdatePoisonStatus()
    {
        if (currentHealth <= 0f || poisonRemainingTime <= 0f) return;

        poisonRemainingTime -= Time.deltaTime;
        poisonTickAccumulator += Time.deltaTime;

        if (poisonTickAccumulator >= 0.25f || poisonRemainingTime <= 0f)
        {
            float tickDmg = poisonDps * poisonTickAccumulator * DamageTakenMultiplier;
            poisonTickAccumulator = 0f;
            currentHealth -= tickDmg;
            if (currentHealth <= 0f)
            {
                Die();
                return;
            }
        }

        if (poisonRemainingTime <= 0f)
        {
            poisonDps = 0f;
            poisonRemainingTime = 0f;
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
        }
    }

    /// <summary>
    /// ทำให้ศัตรูติดสถานะ Stun หยุดการเคลื่อนที่และการโจมตีเป็นเวลา duration วินาที
    /// </summary>
    public void ApplyStun(float duration = 1.0f)
    {
        if (currentHealth <= 0f || duration <= 0f) return;
        stunRemainingTimer = Mathf.Max(stunRemainingTimer, duration);
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        Debug.Log($"[{gameObject.name}] Stunned for {duration:F1}s (Mind Shock)!");
    }

    /// <summary>
    /// ทำให้ศัตรูติดสถานะพิษ (Poison DoT) ลดเลือดต่อเนื่องตามค่า dps เป็นเวลา duration วินาที
    /// </summary>
    public void ApplyPoison(float dps, float duration = 3.0f)
    {
        if (currentHealth <= 0f || dps <= 0f || duration <= 0f) return;
        poisonDps = Mathf.Max(poisonDps, dps);
        poisonRemainingTime = Mathf.Max(poisonRemainingTime, duration);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(originalColor, new Color(0.35f, 1f, 0.45f, 1f), 0.55f);
        }
    }

    public void ApplySlow(float slowPercent, float duration)
    {
        if (currentHealth <= 0f || slowPercent <= 0f || duration <= 0f) return;
        currentSlowPercent = Mathf.Clamp(Mathf.Max(currentSlowPercent, slowPercent), 0f, 0.85f);
        slowRemainingTimer = Mathf.Max(slowRemainingTimer, duration);
    }

    public void ApplyArmorReduction(float reductionPercent, float duration)
    {
        if (currentHealth <= 0f || reductionPercent <= 0f || duration <= 0f) return;
        armorReductionPercent = Mathf.Max(armorReductionPercent, reductionPercent);
        armorReductionRemainingTime = Mathf.Max(armorReductionRemainingTime, duration);
    }

    private void PatrolBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer <= chaseRange)
        {
            currentState = State.Chase;
            return;
        }

        transform.position = Vector2.MoveTowards(transform.position, patrolTarget, moveSpeed * SlowMultiplier * 0.5f * Time.deltaTime);
        if (Vector2.Distance(transform.position, patrolTarget) < 0.2f)
        {
            GetNewPatrolPoint();
        }
    }

    private void ChaseBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > chaseRange * 1.2f)
        {
            currentState = State.Patrol;
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= attackRange)
        {
            currentState = State.Attack;
            return;
        }

        transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * SlowMultiplier * Time.deltaTime);

        // กลับด้าน Sprite โดยไม่แตะ scale (ป้องกัน Bug Physics/Distance)
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = player.position.x < transform.position.x;
        }
    }

    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > attackRange)
        {
            currentState = State.Chase;
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            if (isBoss && isPulseBoss)
                StartCoroutine(DoPulseAttack());
            else
                DealDamageToPlayer();
        }
    }

    private IEnumerator DoPulseAttack()
    {
        GameObject warning = new GameObject("PulseWarning");
        warning.transform.SetParent(transform, false);
        warning.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        SpriteRenderer ring = warning.AddComponent<SpriteRenderer>();
        ring.sprite = CreatePulseRing();
        ring.color = new Color(0.95f, 0.25f, 0.3f, 0.85f);
        ring.sortingOrder = 5;
        warning.transform.localScale = Vector3.one * (attackRange * 2f / transform.localScale.x);

        yield return new WaitForSeconds(pulseWindup);
        if (player != null && Vector2.Distance(transform.position, player.position) <= attackRange)
            DealDamageToPlayer();
        Destroy(warning);
    }

    private static Sprite CreatePulseRing()
    {
        if (pulseRingSprite != null) return pulseRingSprite;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
            pixels[y * size + x] = radius >= 29f && radius <= 31.5f ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        pulseRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return pulseRingSprite;
    }

    private void DealDamageToPlayer()
    {
        if (player == null || IsStunned) return;

        // ดึง interface IDamageable ทั้งบน Root และ Child
        IDamageable playerDamageable = player.GetComponentInChildren<IDamageable>();
        if (playerDamageable != null)
        {
            playerDamageable.TakeDamage(damage);
            Debug.Log($"[EnemyAI] Dealt {damage} damage to Player via IDamageable.");
            return;
        }

        // สำรอง: หากหา IDamageable ไม่เจอ ให้เรียก PlayerHealth โดยตรง
        PlayerHealth playerHealth = player.GetComponentInChildren<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Debug.Log($"[EnemyAI] Dealt {damage} damage to Player via PlayerHealth.");
        }
    }

    // ทำดาเมจเพิ่มเมื่อเดินชนผู้เล่นโดยตรง (Melee Contact Damage)
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsStunned) return;
        if (!isPulseBoss && collision.gameObject.CompareTag("Player") && Time.time >= lastAttackTime + attackCooldown)
        {
            DealDamageToPlayer();
            lastAttackTime = Time.time;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsStunned) return;
        if (!isPulseBoss && other.CompareTag("Player") && Time.time >= lastAttackTime + attackCooldown)
        {
            DealDamageToPlayer();
            lastAttackTime = Time.time;
        }
    }

    private void GetNewPatrolPoint()
    {
        patrolTarget = (Vector2)transform.position + Random.insideUnitCircle * 4f;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f || amount <= 0f) return;
        float finalAmount = amount * DamageTakenMultiplier;
        currentHealth -= finalAmount;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.hitSFX);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount, Vector2 knockback)
    {
        TakeDamage(amount);
    }

    private void Die()
    {
        bool wasPoisonedBeforeDeath = poisonRemainingTime > 0f || poisonDps > 0f;
        poisonRemainingTime = 0f;
        poisonDps = 0f;

        OnLegacyEnemyDiedWithPoisonState?.Invoke(gameObject, transform.position, wasPoisonedBeforeDeath);
        if (deathVFX != null)
        {
            Instantiate(deathVFX, transform.position, Quaternion.identity);
        }

        if (expPrefab != null)
        {
            GameObject expObj = Instantiate(expPrefab, transform.position, Quaternion.identity);
            EXPCrystal crystal = expObj.GetComponent<EXPCrystal>();
            if (crystal != null)
            {
                crystal.SetExpValue(expAmount);
            }
        }

        if (isBoss && RoomManager.Instance != null)
        {
            RoomManager.Instance.OnBossDefeated();
        }

        GameOverManager.Instance?.RecordEnemyKilled();

        Destroy(gameObject);
    }

    public void SetScaledStats(float hpMultiplier, float damageMultiplier, float speedMultiplier = 1f)
    {
        CacheBaseStatsIfNeeded();
        maxHealth = baseMaxHealth * hpMultiplier;
        currentHealth = maxHealth; // รีเซ็ตเลือดปัจจุบันให้เท่ากับ Max HP ใหม่
        damage = baseDamage * damageMultiplier;
        moveSpeed = baseMoveSpeed * speedMultiplier;

        Debug.Log($"[{gameObject.name}] Scaled Stats -> MaxHP: {maxHealth:F1}, Damage: {damage:F1}, Speed: {moveSpeed:F2}");
    }
}
