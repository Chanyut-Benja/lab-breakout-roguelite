using UnityEngine;

/// <summary>
/// ไอเทม Evolution Core ที่ดรอปจากบอส (ห้อง 3, 6, 9)
/// ผู้เล่นสามารถเดินชนเพื่อเก็บเข้ากระเป๋า (Inventory)
/// เมื่อเก็บสำเร็จ จะเรียก PlayerInventory.Instance.AddEvolutionCore(1)
/// ซึ่งจะทริกเกอร์ระบบ Auto-Evolution ตรวจสอบเงื่อนไขการรวมร่างอาวุธอัตโนมัติทันที
/// </summary>
public class EvolutionCorePickup : MonoBehaviour
{
    [Header("Core Settings")]
    [SerializeField] private int coreAmount = 1;
    [SerializeField] private float bobbingSpeed = 3.5f;
    [SerializeField] private float bobbingHeight = 0.25f;
    [SerializeField] private float rotationSpeed = 100f;

    [Header("Audio & FX")]
    [SerializeField] private AudioClip pickupSFX;

    private Vector3 startPosition;
    private bool isCollected = false;

    private void Start()
    {
        startPosition = transform.position;

        // ตรวจสอบ Collider
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.65f;
        }
        else
        {
            col.isTrigger = true;
        }

        // ตรวจสอบ SpriteRenderer
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
        }
        if (sr.sprite == null)
        {
            ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Crystal);
            sr.color = new Color(1f, 0.85f, 0.2f, 1f); // Glowing Gold Core
        }
    }

    private void Update()
    {
        if (isCollected) return;

        // แอนิเมชันลอยขึ้น-ลง (Bobbing) และหมุน (Rotation) สไตล์ Sci-Fi Energy Core
        float yOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
        transform.position = startPosition + new Vector3(0f, yOffset, 0f);
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        if (collision.CompareTag("Player") || collision.GetComponent<PlayerController>() != null || collision.GetComponent<PlayerHealth>() != null)
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            inv.AddEvolutionCore(coreAmount);
            Debug.Log($"[EvolutionCorePickup] Player collected Evolution Core (+{coreAmount})! Current Cores: {inv.EvolutionCores}");
        }

        if (pickupSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(pickupSFX);
        }
        else if (AudioManager.Instance != null && AudioManager.Instance.expSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.expSFX);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// สปอว์น Evolution Core Pickup ลงบนตำแหน่งในฉาก
    /// </summary>
    public static GameObject Spawn(Vector3 position, int amount = 1)
    {
        GameObject coreGo = new GameObject("EvolutionCore_Pickup");
        coreGo.transform.position = position;
        EvolutionCorePickup pickup = coreGo.AddComponent<EvolutionCorePickup>();
        pickup.coreAmount = amount;
        return coreGo;
    }
}
