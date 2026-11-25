using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float iFrameDuration = 0.5f;
    private float currentHealth;
    private float invulnerableUntil = 0f;
    private bool isDashInvulnerable = false;

    // Nano-Barrier Boon Shield (30 -> 60 HP, ฟื้นเมื่อไม่โดนตี 5s)
    private float maxBarrierShield = 0f;
    private float currentBarrierShield = 0f;
    private float lastDamageReceivedTime = -999f;

    [Header("UI Reference")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Text hpText;

    private PlayerStats playerStats;
    private RectTransform psionicBar;   // purple strip after the HP fill showing the Psionic Barrier shield

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float CurrentBarrierShield => currentBarrierShield;
    public float MaxBarrierShield => maxBarrierShield;
    public float LastDamageReceivedTime => lastDamageReceivedTime;
    public bool IsDashInvulnerable => isDashInvulnerable;
    public bool IsInvulnerable => isDashInvulnerable || Time.time < invulnerableUntil;

    public event System.Action OnBarrierBroken;
    public event System.Action<float> OnShieldDamaged;
    public event System.Action<float> OnPlayerDamaged;

    private float barrierGraceUntil;

    /// <summary>maxShield = number of hits the barrier can block (Lv.1 = 1, Lv.2 = 2).</summary>
    public void ConfigureNanoBarrier(float maxShield)
    {
        float clampedMax = Mathf.Max(0f, maxShield);
        if (maxBarrierShield <= 0f && clampedMax > 0f)
        {
            maxBarrierShield = clampedMax;
            currentBarrierShield = maxBarrierShield;
            UpdateHealthUI();
        }
        else if (clampedMax > maxBarrierShield)
        {
            float diff = clampedMax - maxBarrierShield;
            maxBarrierShield = clampedMax;
            currentBarrierShield = Mathf.Min(maxBarrierShield, currentBarrierShield + diff);
            UpdateHealthUI();
        }
        else if (clampedMax < maxBarrierShield)
        {
            maxBarrierShield = clampedMax;
            currentBarrierShield = Mathf.Min(currentBarrierShield, maxBarrierShield);
            UpdateHealthUI();
        }
    }

    public void RestoreNanoBarrierToFull()
    {
        if (maxBarrierShield <= 0f) return;
        currentBarrierShield = maxBarrierShield;
        UpdateHealthUI();
    }

    /// <summary>
    /// เปิด/ปิดสถานะอมตะชั่วคราว (I-Frame) ระหว่างที่กำลัง Dash (เช่น Overclock Dash ของ N-03 Cyber-Swift)
    /// </summary>
    public void SetDashInvulnerability(bool active)
    {
        isDashInvulnerable = active;
    }

    /// <summary>
    /// มอบสถานะอมตะชั่วคราว (I-Frame) ตามระยะเวลาที่กำหนด
    /// </summary>
    public void GrantInvulnerability(float duration)
    {
        if (duration <= 0f) return;
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    public void ResetInvulnerability()
    {
        invulnerableUntil = 0f;
    }

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        if (playerStats != null)
        {
            maxHealth = playerStats.MaxHealth;
        }

        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    private void Update()
    {
        // ฟื้นฟู HP ต่อวินาทีจากผัง Skill Tree ของ N-01 Vanguard (HP Regen)
        if (currentHealth > 0f && currentHealth < maxHealth && playerStats != null && playerStats.HPRegenPerSecond > 0f)
        {
            Heal(playerStats.HPRegenPerSecond * Time.deltaTime);
        }
    }

    public void IncreaseMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth > 0f && amount > 0f && isDashInvulnerable && SkillMajorEffects.Instance != null) SkillMajorEffects.Instance.OnDodged();   // Lightning Reflexes
        if (currentHealth <= 0f || amount <= 0f || IsInvulnerable) return;
        if (Time.time < barrierGraceUntil) return;   // grace window after a barrier block

        lastDamageReceivedTime = Time.time;
        if (SkillMajorEffects.Instance != null) SkillMajorEffects.Instance.TryPsionicEmergency();   // Psionic Barrier: heal + shield when hit under 30% HP
        float bonusIFrame = playerStats != null ? playerStats.BonusIFrameDuration : 0f;
        invulnerableUntil = Time.time + iFrameDuration + bonusIFrame;

        // คำนวณ Armor และ Reflect จาก Skill Tree ของ N-01 Vanguard
        float flatArmor = playerStats != null ? playerStats.Armor : 0f;
        float remainingDamage = flatArmor > 0f ? Mathf.Max(1f, amount - flatArmor) : amount;

        if (playerStats != null && flatArmor > 0f && playerStats.ReflectArmorRatio > 0f)
        {
            float reflectDmg = flatArmor * playerStats.ReflectArmorRatio;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2.5f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] != null && hits[i].gameObject != gameObject && !hits[i].CompareTag("Player"))
                {
                    IDamageable enemyDmg = hits[i].GetComponent<IDamageable>() ?? hits[i].GetComponentInParent<IDamageable>();
                    if (enemyDmg != null)
                    {
                        enemyDmg.TakeDamage(reflectDmg);
                    }
                }
            }
        }

        // Nano-Barrier: every charge blocks one whole hit. After a block there is a short grace window.
        if (currentBarrierShield > 0f)
        {
            currentBarrierShield = Mathf.Max(0f, currentBarrierShield - 1f);
            remainingDamage = 0f;
            barrierGraceUntil = Time.time + 0.4f;
            OnShieldDamaged?.Invoke(1f);

            if (currentBarrierShield <= 0f)
            {
                currentBarrierShield = 0f;
                OnBarrierBroken?.Invoke();   // Resonance: the blade ring fires here
            }
        }

        SkillMajorEffects fx = SkillMajorEffects.Instance;
        if (fx != null && remainingDamage > 0f) remainingDamage = fx.AbsorbShield(remainingDamage);   // Psionic Barrier

        if (remainingDamage > 0f)
        {
            if (fx != null && fx.TryAbsoluteArmor(currentHealth - remainingDamage, maxHealth))   // Absolute Armor
                remainingDamage = Mathf.Min(remainingDamage, Mathf.Max(0f, currentHealth - 1f));
            currentHealth -= remainingDamage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        UpdateHealthUI();

        OnPlayerDamaged?.Invoke(amount);

        if (currentHealth > 0 && AudioManager.Instance != null)   // ถ้าตายจะเล่นเสียงตายแทน
            AudioManager.Instance.PlayHurtSFX();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount, Vector2 knockback)
    {
        if (currentHealth <= 0f || amount <= 0f || IsInvulnerable) return;

        TakeDamage(amount);
        if (currentHealth <= 0f || knockback == Vector2.zero) return;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            body.AddForce(knockback, ForceMode2D.Impulse);
    }

    /// <summary>
    /// ความเสียหายต่อเนื่องจากสภาพแวดล้อม เช่น แก๊สพิษ Containment Purge (ไม่ติด I-Frame ของการถูกโจมตีปกติ)
    /// </summary>
    public void TakePurgeDamage(float amount)
    {
        if (currentHealth <= 0f || amount <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateHealthUI();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    public void SetHpText(Text text)
    {
        hpText = text;
        UpdateHealthUI();
    }

    private void LateUpdate()
    {
        // something else (e.g. the EXP bar) can write to the shared slider; always restore the real HP
        if (healthSlider != null && (!Mathf.Approximately(healthSlider.maxValue, maxHealth) || !Mathf.Approximately(healthSlider.value, currentHealth)))
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        UpdatePsionicBar();
    }

    private float PsionicShield => SkillMajorEffects.Instance != null ? SkillMajorEffects.Instance.CurrentShield : 0f;

    private void UpdatePsionicBar()
    {
        if (healthSlider == null) return;
        float sh = PsionicShield;
        if (psionicBar == null)
        {
            if (sh <= 0f) return;
            var go = new GameObject("PsionicShieldBar", typeof(RectTransform), typeof(Image));
            psionicBar = go.GetComponent<RectTransform>();
            psionicBar.SetParent(healthSlider.transform, false);
            psionicBar.offsetMin = Vector2.zero;
            psionicBar.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.7f, 0.4f, 1f, 0.9f);
            img.raycastTarget = false;
        }
        bool on = sh > 0f && maxHealth > 0f;
        if (psionicBar.gameObject.activeSelf != on) psionicBar.gameObject.SetActive(on);
        if (!on) return;
        psionicBar.SetAsLastSibling();
        float a = Mathf.Clamp01(currentHealth / maxHealth);
        float b = Mathf.Clamp01((currentHealth + sh) / maxHealth);
        psionicBar.anchorMin = new Vector2(a, 0f);
        psionicBar.anchorMax = new Vector2(b, 1f);
        psionicBar.offsetMin = Vector2.zero;
        psionicBar.offsetMax = Vector2.zero;
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (hpText != null)
        {
            float totalShield = currentBarrierShield + PsionicShield;
            if (totalShield > 0f)
            {
                hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, currentHealth))} / {Mathf.CeilToInt(maxHealth)} HP (+{Mathf.CeilToInt(totalShield)} Shield)";
            }
            else
            {
                hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, currentHealth))} / {Mathf.CeilToInt(maxHealth)} HP";
            }
        }
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPlayerDeadSFX();
        gameObject.SetActive(false);

        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.TriggerGameOver();
        }
    }

    public void SetMaxHealthFromStats(float newMaxHealth)
    {
        // Do NOT refill HP here: raise current HP only by the amount max HP grew.
        float delta = newMaxHealth - maxHealth;
        maxHealth = newMaxHealth;
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0f, delta), 0f, maxHealth);
        UpdateHealthUI();
        Debug.Log($"[PlayerHealth] Updated MaxHealth to: {currentHealth}");
    }
}
