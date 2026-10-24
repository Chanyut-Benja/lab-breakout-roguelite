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
                if (PlayerLevel.Instance != null)
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
