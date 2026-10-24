using System;
using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float defaultMoveSpeed = 5f;
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Dash Charges & Overclock Dash (N-03 Cyber-Swift)")]
    [SerializeField] private int maxDashCharges = 1;
    [SerializeField] private bool hasOverclockDashPassive = false;

    private Rigidbody2D rb;
    private PlayerHealth playerHealth;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    private bool isDashing;
    private int currentDashCharges = 1;
    private float dashRechargeTimer = 0f;
    private float nextBoundaryNotice;

    public Vector2 LastMoveDirection => lastMoveDirection;
    public bool IsDashing => isDashing;
    public int CurrentDashCharges => currentDashCharges;
    public int MaxDashCharges => maxDashCharges;
    public bool HasOverclockDashPassive => hasOverclockDashPassive;

    public event Action<int, int> OnDashChargesChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerHealth = GetComponent<PlayerHealth>();
        currentDashCharges = Mathf.Max(1, maxDashCharges);
    }

    private void Start()
    {
        SyncDashSettingsFromStats();
    }

    /// <summary>
    /// ตั้งค่าจำนวน Dash Charges และสกิล Overclock Dash ตามตัวละครที่เลือก (N-03 Cyber-Swift มี 2 Charges + Overclock Dash)
    /// </summary>
    public void ConfigureDashSettings(int maxCharges, bool enableOverclockDash)
    {
        maxDashCharges = Mathf.Max(1, maxCharges);
        hasOverclockDashPassive = enableOverclockDash;
        currentDashCharges = maxDashCharges;
        dashRechargeTimer = 0f;
        OnDashChargesChanged?.Invoke(currentDashCharges, maxDashCharges);
    }

    private void SyncDashSettingsFromStats()
    {
        PlayerStats stats = PlayerStats.Instance != null ? PlayerStats.Instance : GetComponent<PlayerStats>();
        if (stats != null)
        {
            ConfigureDashSettings(stats.MaxDashCharges, stats.HasOverclockDashPassive);
        }
    }

    private void Update()
    {
        UpdateDashRecharge();

        if (isDashing) return;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(moveX, moveY).normalized;

        if (moveInput.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = moveInput;
        }

        if (Input.GetKeyDown(KeyCode.Space) && currentDashCharges > 0)
        {
            Vector2 dashDirection = moveInput.sqrMagnitude > 0.001f ? moveInput : lastMoveDirection;
            if (dashDirection.sqrMagnitude < 0.001f) dashDirection = Vector2.right;
            StartCoroutine(PerformDash(dashDirection.normalized));
        }
    }

    /// <summary>
    /// รีชาร์จ Dash ทีละ 1 ชาร์จตามคูลดาวน์ (Sequential Charge Cooldown + Skill Tree Dash Cooldown Reduction)
    /// </summary>
    private void UpdateDashRecharge()
    {
        if (currentDashCharges >= maxDashCharges)
        {
            dashRechargeTimer = 0f;
            return;
        }

        float cdMult = PlayerStats.Instance != null ? PlayerStats.Instance.DashCooldownMultiplier : 1f;
        float effectiveCooldown = Mathf.Max(0.25f, dashCooldown * cdMult);

        dashRechargeTimer += Time.deltaTime;
        if (dashRechargeTimer >= effectiveCooldown)
        {
            dashRechargeTimer -= effectiveCooldown;
            currentDashCharges = Mathf.Min(currentDashCharges + 1, maxDashCharges);
            OnDashChargesChanged?.Invoke(currentDashCharges, maxDashCharges);
        }
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        // ดึงค่า MoveSpeed ล่าสุดจาก PlayerStats.Instance เสมอ
        float currentSpeed = GetCurrentMoveSpeed();
        rb.MovePosition(rb.position + moveInput * currentSpeed * Time.fixedDeltaTime);
    }

    private float GetCurrentMoveSpeed()
    {
        // 1. ดึงจาก Singleton Instance ของ PlayerStats ก่อน
        if (PlayerStats.Instance != null)
        {
            return PlayerStats.Instance.MoveSpeed;
        }

        // 2. ถ้า Instance ไม่มี ให้ลองดึงจาก Component บนตัวละคร
        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            return stats.MoveSpeed;
        }

        // 3. ถ้าไม่มีทั้งคู่ ให้ใช้ค่า Default
        return defaultMoveSpeed;
    }

    private IEnumerator PerformDash(Vector2 dashDirection)
    {
        currentDashCharges = Mathf.Max(0, currentDashCharges - 1);
        OnDashChargesChanged?.Invoke(currentDashCharges, maxDashCharges);
        isDashing = true;

        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        // Passive Skill - Overclock Dash (N-03 Cyber-Swift):
        // 1. ได้รับสถานะ I-Frame ตลอดช่วงที่กำลัง Dash (ไม่รับความเสียหายจากการโจมตีหรือการชนของศัตรู)
        // 2. ณ จังหวะที่เริ่มกด Dash ให้สั่งยิงกระสุนพลาสม่าสวนกลับไปยังศัตรูที่อยู่ใกล้ที่สุด 1 นัดทันที
        bool grantOverclockEffects = hasOverclockDashPassive || (PlayerStats.Instance != null && PlayerStats.Instance.HasOverclockDashPassive);
        if (grantOverclockEffects)
        {
            if (playerHealth != null)
            {
                playerHealth.SetDashInvulnerability(true);
            }

            PlasmaBlasterWeapon.FireOverclockDashShot(transform, dashDirection);
        }

        // สเกลความเร็ว Dash ให้สัมพันธ์กับ MoveSpeed ปัจจุบันด้วย
        float currentSpeed = GetCurrentMoveSpeed();
        float currentDashSpeed = Mathf.Max(dashSpeed, currentSpeed * 2.5f);

        rb.linearVelocity = dashDirection * currentDashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero;
        isDashing = false;

        if (playerHealth != null)
        {
            playerHealth.SetDashInvulnerability(false);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        NotifyIfAtBoundary(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        NotifyIfAtBoundary(collision);
    }

    private void NotifyIfAtBoundary(Collision2D collision)
    {
        Transform wall = collision.collider.transform;
        if (!wall.name.StartsWith("Wall_") || wall.parent == null || wall.parent.name != "MapBoundaries") return;
        if (Time.time < nextBoundaryNotice) return;
        nextBoundaryNotice = Time.time + 1.5f;
        if (RoomManager.Instance != null) RoomManager.Instance.ShowBoundaryNotice();
    }
}
