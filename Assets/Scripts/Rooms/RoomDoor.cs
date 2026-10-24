using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class RoomDoor : MonoBehaviour
{
    [Header("Door Configuration")]
    [SerializeField] private RoomRewardType rewardType = RoomRewardType.BioGeneChamber;
    [SerializeField] private string doorTitle = "Bio-Gene Chamber";
    [SerializeField] private bool autoEnterOnTrigger = true; // เดินชนเข้าทันที หรือต้องกดปุ่ม
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Text titleText;
    [SerializeField] private TextMesh worldTextMesh;
    [SerializeField] private GameObject interactionPrompt;

    [Header("Reward Icons")]
    [SerializeField] private Sprite boonSprite;
    [SerializeField] private Sprite shopSprite;
    [SerializeField] private Sprite researchSprite;
    [SerializeField] private Sprite medicalPodSprite;
    [SerializeField] private Sprite geneEnhancerSprite;
    [SerializeField] private Sprite bossSprite;

    private bool isPlayerNearby = false;
    private bool isActivated = false;

    public RoomRewardType RewardType => rewardType;
    public string DoorTitle => doorTitle;

    private void Awake()
    {
        if (iconRenderer == null) iconRenderer = GetComponent<SpriteRenderer>();
        // บังคับ Collider ให้เป็น Trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    /// <summary>
    /// กำหนดประเภทรางวัลและชื่อของประตูนี้
    /// </summary>
    public void SetupDoor(RoomRewardType type, string title = "")
    {
        rewardType = type;
        doorTitle = string.IsNullOrEmpty(title) ? GetDefaultTitle(type) : title;

        UpdateVisuals();
    }

    public static string GetDefaultTitle(RoomRewardType type)
    {
        return type switch
        {
            RoomRewardType.BioGeneChamber => "Bio-Gene Chamber (Boon)",
            RoomRewardType.BlackMarket => "Black Market",
            RoomRewardType.CreditVault => "Credit Vault",
            RoomRewardType.MedicalPod => "Medical Pod (+30% HP)",
            RoomRewardType.GeneEnhancer => "Gene Enhancer (Rare ★)",
            RoomRewardType.Boss => "Apex Boss Chamber",
            _ => "Next Sector"
        };
    }

    private void UpdateVisuals()
    {
        if (titleText != null)
        {
            titleText.text = doorTitle;
        }

        if (worldTextMesh != null)
        {
            worldTextMesh.text = doorTitle;
            worldTextMesh.color = new Color(0.08f, 0.16f, 0.23f);
        }

        if (iconRenderer != null)
        {
            // บังคับให้ใช้รูปทรงคริสตัลดั้งเดิมเสมอ (ไม่โดนแทนที่ด้วยไอคอนสกิล)
            iconRenderer.sprite = ProceduralSprites.Crystal;

            // ปรับขนาด Scale ให้คงที่เป็นขนาดคริสตัลปกติ
            iconRenderer.transform.localScale = Vector3.one;

            // สลับสี Tint ตามประเภทประตู
            iconRenderer.color = rewardType switch
            {
                RoomRewardType.BioGeneChamber => new Color(0.25f, 0.75f, 0.95f), // สีฟ้า
                RoomRewardType.BlackMarket => new Color(0.6f, 0.45f, 0.9f),   // สีม่วง
                RoomRewardType.CreditVault => new Color(0.95f, 0.8f, 0.25f),  // สีทอง
                RoomRewardType.MedicalPod => new Color(0.25f, 0.85f, 0.55f), // สีเขียว
                RoomRewardType.GeneEnhancer => new Color(1f, 0.55f, 0.2f),     // สีส้ม
                RoomRewardType.Boss => new Color(0.95f, 0.4f, 0.45f),  // สีแดง
                _ => Color.white
            };
        }
    }

    private void Update()
    {
        if (!isPlayerNearby || isActivated) return;

        if (!autoEnterOnTrigger && Input.GetKeyDown(interactKey))
        {
            EnterDoor();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isActivated)
        {
            isPlayerNearby = true;
            if (interactionPrompt != null && !autoEnterOnTrigger)
            {
                interactionPrompt.SetActive(true);
            }

            if (autoEnterOnTrigger)
            {
                EnterDoor();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }

    public void EnterDoor()
    {
        if (isActivated) return;
        isActivated = true;

        // เพิ่ม Research Points เมื่อเข้าสู่ประตูและบันทึกลงไฟล์
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.AddResearchPoints(25);
        else
            SaveSystem.AddResearchPoints(25);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.doorSFX);

        Debug.Log($"[RoomDoor] Player entered door: {doorTitle} ({rewardType})");

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.OnDoorSelected(rewardType);
        }
    }
}
