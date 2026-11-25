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
    private bool labelTuned;
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

    public static Color GetRewardColor(RoomRewardType type)
    {
        return type switch
        {
            RoomRewardType.BioGeneChamber => new Color(0.25f, 0.75f, 0.95f), // blue
            RoomRewardType.BlackMarket => new Color(0.6f, 0.45f, 0.9f),   // purple
            RoomRewardType.CreditVault => new Color(0.95f, 0.8f, 0.25f),  // gold
            RoomRewardType.MedicalPod => new Color(0.25f, 0.85f, 0.55f), // green
            RoomRewardType.GeneEnhancer => new Color(1f, 0.55f, 0.2f),     // orange
            RoomRewardType.Boss => new Color(0.95f, 0.4f, 0.45f),  // red
            _ => Color.white
        };
    }

    // a wide trigger in front of the doorway: walk up to the door and you go through
    private bool wideTrigger;
    private void EnsureWideTrigger()
    {
        if (wideTrigger) return;
        wideTrigger = true;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        // the trigger sits in the doorway itself (the name plate stays in front of it): the player has to walk up to the door
        // tall enough to start just below the name plate, so touching the plate or the doorway both count
        box.offset = new Vector2(0f, 0.95f);
        box.size = new Vector2(3.2f, 1.2f);
    }

    public static string GetDefaultTitle(RoomRewardType type)
    {
        return type switch
        {
            RoomRewardType.BioGeneChamber => "Bio-Gene Chamber (Boon)",
            RoomRewardType.BlackMarket => "Black Market",
            RoomRewardType.CreditVault => "Credit Vault",
            RoomRewardType.MedicalPod => "Medical Pod (+30% HP)",
            RoomRewardType.GeneEnhancer => "Gene Enhancer (Rare)",
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
            // crisp + smaller: render the font big, shrink characterSize to keep it small in the world
            if (!labelTuned)
            {
                labelTuned = true;
                int fs = worldTextMesh.fontSize > 0 ? worldTextMesh.fontSize : 32;
                float cs = worldTextMesh.characterSize > 0f ? worldTextMesh.characterSize : 0.1f;
                worldTextMesh.fontSize = 120;
                worldTextMesh.characterSize = cs * fs / 120f * 0.15f;
            }
            worldTextMesh.color = new Color(0.08f, 0.16f, 0.23f);
        }

        if (iconRenderer != null)
        {
            // บังคับให้ใช้รูปทรงคริสตัลดั้งเดิมเสมอ (ไม่โดนแทนที่ด้วยไอคอนสกิล)
            iconRenderer.sprite = ProceduralSprites.Crystal;

            // ปรับขนาด Scale ให้คงที่เป็นขนาดคริสตัลปกติ
            iconRenderer.transform.localScale = Vector3.one * 2.4f;

            // สลับสี Tint ตามประเภทประตู
            iconRenderer.color = GetRewardColor(rewardType);

            // With the separate room maps the door is the glowing doorway in the wall, not a crystal.
            if (RoomEnvironment.Instance != null)
            {
                iconRenderer.enabled = false;
                EnsureWideTrigger();
            }
        }
        PlaceLabel();
    }

    // put the room name right above the portal (above the highest sprite of the door), drawn on top of it
    private void PlaceLabel()
    {
        if (worldTextMesh == null) return;
        if (RoomEnvironment.Instance != null)
        {
            // name plate on the floor just below the doorway (the plate behind it is drawn by RoomEnvironment)
            worldTextMesh.anchor = TextAnchor.MiddleCenter;
            worldTextMesh.alignment = TextAlignment.Center;
            worldTextMesh.transform.rotation = Quaternion.identity;
            worldTextMesh.transform.position = new Vector3(transform.position.x, transform.position.y - 0.6f, transform.position.z);
            worldTextMesh.color = Color.white;
            MeshRenderer plateRenderer = worldTextMesh.GetComponent<MeshRenderer>();
            if (plateRenderer != null) plateRenderer.sortingOrder = 70;
            return;
        }
        bool any = false; Bounds bb = new Bounds(transform.position, Vector3.zero); int order = 0;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (sr == null || sr == arrowRenderer || !sr.enabled || sr.sprite == null) continue;
            if (!any) { bb = sr.bounds; any = true; } else bb.Encapsulate(sr.bounds);
            order = Mathf.Max(order, sr.sortingOrder);
        }
        if (!any) return;
        worldTextMesh.anchor = TextAnchor.LowerCenter;
        worldTextMesh.alignment = TextAlignment.Center;
        worldTextMesh.transform.rotation = Quaternion.identity;
        worldTextMesh.transform.position = new Vector3(bb.center.x, bb.max.y + 0.12f, transform.position.z);
        MeshRenderer mr = worldTextMesh.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = order + 5;
    }

    // ---- off-screen pointer arrow (like the treasure-chest arrow in Vampire Survivors) ----
    private Transform arrow;
    private SpriteRenderer arrowRenderer;
    private static Sprite arrowSprite;

    private static Sprite GetArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;
        const int n = 64;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // triangle pointing right: tip at (n-2, n/2), base at x=4
                float u = (x - 4f) / (n - 6f);                 // 0 at base, 1 at tip
                float half = (1f - u) * (n * 0.42f);
                bool inside = u >= 0f && u <= 1f && Mathf.Abs(y - n * 0.5f) <= half;
                t.SetPixel(x, y, inside ? Color.white : clear);
            }
        }
        t.Apply();
        arrowSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        return arrowSprite;
    }

    private void LateUpdate()
    {
        if (isActivated) { if (arrow != null) arrow.gameObject.SetActive(false); return; }
        Camera cam = Camera.main;
        if (cam == null) return;

        if (arrow == null)
        {
            GameObject g = new GameObject("DoorArrow");
            arrow = g.transform;
            arrowRenderer = g.AddComponent<SpriteRenderer>();
            arrowRenderer.sprite = GetArrowSprite();
            arrowRenderer.sortingOrder = 60;
            arrow.localScale = Vector3.one * 1.1f;
        }

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool onScreen = vp.z > 0f && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f;
        arrow.gameObject.SetActive(!onScreen);
        if (onScreen) return;

        Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f) dir = -dir;
        if (dir.sqrMagnitude < 1e-5f) dir = Vector2.up;
        // push the direction out to the screen border (with a margin)
        float sx = 0.45f / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
        float sy = 0.42f / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
        Vector2 edge = new Vector2(0.5f, 0.5f) + dir * Mathf.Min(sx, sy);
        Vector3 w = cam.ViewportToWorldPoint(new Vector3(edge.x, edge.y, Mathf.Abs(cam.transform.position.z)));
        w.z = 0f;
        arrow.position = w;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.rotation = Quaternion.Euler(0f, 0f, ang);
        if (arrowRenderer != null && iconRenderer != null) arrowRenderer.color = new Color(iconRenderer.color.r, iconRenderer.color.g, iconRenderer.color.b, 0.95f);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
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
