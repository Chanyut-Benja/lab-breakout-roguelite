using System.Collections.Generic;
using UnityEngine;

public class EXPCrystal : MonoBehaviour
{
    private static readonly List<EXPCrystal> activeCrystals = new List<EXPCrystal>();

    [SerializeField] private int expValue = 10;
    [SerializeField] private float attractRadius = 4f;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float vacuumMoveSpeed = 22f;

    private Transform player;
    private bool isBeingAttracted = false;
    private bool isVacuumed = false;

    private void OnEnable()
    {
        if (!activeCrystals.Contains(this))
        {
            activeCrystals.Add(this);
        }
    }

    private void OnDisable()
    {
        activeCrystals.Remove(this);
    }

    private void Start()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = ProceduralSprites.Crystal;
        transform.localScale *= 2.2f;   // easier to see
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (player != null) return;
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    public void SetExpValue(int value)
    {
        expValue = value;
    }

    /// <summary>
    /// บังคับดูด EXP Crystal ก้อนนี้เข้าหาตัวผู้เล่นทันที (ใช้เมื่อเคลียร์ห้องครบ 90 วินาที)
    /// </summary>
    public void TriggerVacuum()
    {
        isBeingAttracted = true;
        isVacuumed = true;
    }

    /// <summary>
    /// Instantly collects every crystal left in the scene (used when a room ends, so no EXP is wasted).
    /// </summary>
    public static void CollectAllNow()
    {
        int total = 0;
        for (int i = activeCrystals.Count - 1; i >= 0; i--)
        {
            EXPCrystal c = activeCrystals[i];
            if (c == null) continue;
            total += c.expValue;
            Destroy(c.gameObject);
        }
        activeCrystals.Clear();
        if (total <= 0) return;

        if (ExpManager.Instance != null)
        {
            ExpManager.Instance.AddExperience(total);
        }
        else if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.AddExperience(total);
        }

        if (AudioManager.Instance != null && AudioManager.Instance.expSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.expSFX);
        }
    }

    /// <summary>
    /// ดูด EXP Crystal ทั้งหมดที่ตกค้างอยู่ในฉากเข้าหาตัวผู้เล่นอัตโนมัติ
    /// </summary>
    public static void VacuumAllActiveCrystals()
    {
        for (int i = activeCrystals.Count - 1; i >= 0; i--)
        {
            if (activeCrystals[i] != null && activeCrystals[i].gameObject.activeInHierarchy)
            {
                activeCrystals[i].TriggerVacuum();
            }
        }
    }

    private void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // Existing crystals also react when a pickup-range card is selected.
        if (PlayerStats.Instance != null)
            attractRadius = PlayerStats.Instance.PickupRadius;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= attractRadius)
        {
            isBeingAttracted = true;
        }

        if (isBeingAttracted)
        {
            float currentPullSpeed = isVacuumed ? Mathf.Max(moveSpeed, vacuumMoveSpeed) : moveSpeed;
            transform.position = Vector2.MoveTowards(transform.position, player.position, currentPullSpeed * Time.deltaTime);

            if (distance < 0.3f)
            {
                if (ExpManager.Instance != null)
                {
                    ExpManager.Instance.AddExperience(expValue);
                }
                else if (PlayerLevel.Instance != null)
                {
                    PlayerLevel.Instance.AddExperience(expValue);
                }
                else
                {
                    PlayerLevel level = player.GetComponent<PlayerLevel>();
                    if (level != null)
                    {
                        level.AddExperience(expValue);
                    }
                }

                if (AudioManager.Instance != null && AudioManager.Instance.expSFX != null)
                {
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.expSFX);
                }

                Destroy(gameObject);
            }
        }
    }
}

/// <summary>
/// ตัวจัดการระบบ EXP, เลเวลผู้เล่น, และการแปลงค่า EXP ล้นหลอดเป็น Nano-Credits
/// ตามเอกสาร Balance Sheet ข้อ 7:
/// 1. สูตรคำนวณ EXP: ExpRequired(level) = 10 + (6 * level);
/// 2. Max Level = 30;
/// 3. เมื่อผู้เล่นถึง Lv.30: ให้หยุดนับเลเวล และแปลง EXP ที่ได้รับหลังจากนั้นเป็น Credits อัตรา: 1 EXP = 0.5 Credits (Floor/Round)
/// </summary>
public class ExpManager : MonoBehaviour
{
    private static ExpManager instance;
    public static ExpManager Instance
    {
        get
        {
            if (instance == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                instance = Object.FindFirstObjectByType<ExpManager>();
#else
                instance = Object.FindObjectOfType<ExpManager>();
#endif
                if (instance == null)
                {
                    GameObject go = new GameObject("ExpManager");
                    instance = go.AddComponent<ExpManager>();
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    public const int MAX_LEVEL = 30;
    public const int BASE_EXP = 10;
    public const int EXP_LINEAR_STEP = 6;
    public const float EXP_TO_CREDIT_RATE = 0.5f;

    [Header("Settings")]
    [SerializeField] private int maxLevel = MAX_LEVEL;
    [SerializeField] private int baseExp = BASE_EXP;
    [SerializeField] private int expLinearStep = EXP_LINEAR_STEP;
    [SerializeField] private float expToCreditRate = EXP_TO_CREDIT_RATE;

    private float excessCreditBuffer = 0f;

    public int MaxLevel => maxLevel;
    public int BaseExp => baseExp;
    public int ExpLinearStep => expLinearStep;
    public float ExpToCreditRate => expToCreditRate;

    public static event System.Action<int, int> OnExpConvertedToCredits; // (expAmount, creditsAwarded)

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// สูตรคำนวณ EXP: ExpRequired(level) = 10 + (6 * level);
    /// </summary>
    public static int ExpRequired(int level)
    {
        return BASE_EXP + (EXP_LINEAR_STEP * Mathf.Max(1, level));
    }

    public int GetExpRequired(int level)
    {
        return baseExp + (expLinearStep * Mathf.Max(1, level));
    }

    /// <summary>
    /// แปลง EXP ที่ได้รับหลังจากผู้เล่นถึง Lv.30 เป็น Credits ในอัตรา 1 EXP = 0.5 Credits
    /// ใช้การสะสมบัฟเฟอร์ทศนิยมร่วมกับ FloorToInt เพื่อไม่ให้เศษเครดิตสูญหาย
    /// </summary>
    public int ConvertExcessExpToCredits(int excessExp)
    {
        if (excessExp <= 0) return 0;

        excessCreditBuffer += excessExp * expToCreditRate;
        int creditsToAward = Mathf.FloorToInt(excessCreditBuffer);

        if (creditsToAward > 0)
        {
            excessCreditBuffer -= creditsToAward;

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddCredits(creditsToAward);
            }
            else if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.AddRunCredits(creditsToAward);
            }

            OnExpConvertedToCredits?.Invoke(excessExp, creditsToAward);
            Debug.Log($"[ExpManager] Reached Max Lv.{maxLevel}: Converted +{excessExp} EXP into +{creditsToAward} Nano-Credits (Rate: 1 EXP = {expToCreditRate} Credits, Buffer: {excessCreditBuffer:F2})");
        }

        return creditsToAward;
    }

    /// <summary>
    /// เมธอดกลางในการรับ EXP และตรวจสอบการแปลงเป็น Credits อัตโนมัติเมื่อถึงเลเวล 30
    /// </summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        PlayerLevel playerLevel = PlayerLevel.Instance;
        if (playerLevel == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerLevel = player.GetComponent<PlayerLevel>();
        }

        if (playerLevel != null)
        {
            if (playerLevel.CurrentLevel >= maxLevel)
            {
                ConvertExcessExpToCredits(amount);
            }
            else
            {
                playerLevel.AddExperience(amount);
            }
        }
        else
        {
            ConvertExcessExpToCredits(amount);
        }
    }
}
