using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private int remainingPierces = 1;

    private Vector2 direction;
    private float timer;
    private readonly HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

    private void Awake()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = ProceduralSprites.Shot;
    }

    /// <summary>
    /// กำหนดทิศทางและพลังโจมตีของกระสุน (เรียกใช้จากสคริปต์ยิง)
    /// </summary>
    public void Setup(Vector2 targetDirection, float customDamage = -1f, int pierceCount = 1, float customSpeed = -1f, Color? customColor = null)
    {
        direction = targetDirection.sqrMagnitude > 0.0001f ? targetDirection.normalized : Vector2.right;

        if (customDamage > 0f)
        {
            damage = customDamage;
        }

        if (customSpeed > 0f)
        {
            speed = customSpeed;
        }

        remainingPierces = Mathf.Max(1, pierceCount);
        hitEnemies.Clear();
        timer = 0f;

        if (customColor.HasValue)
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = customColor.Value;
        }

        // หมุนตัวกระสุนให้หันหน้าไปตามทิศทางการบิน
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        // เคลื่อนที่กระสุนไปข้างหน้าตามทิศทางที่หมุนไว้
        transform.Translate(Vector3.right * speed * Time.deltaTime);

        // นับเวลาถอยหลังทำลายกระสุนเมื่อหมดอายุขัย
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null || collision.CompareTag("Player")) return;

        GameObject targetObj = collision.attachedRigidbody != null ? collision.attachedRigidbody.gameObject : collision.gameObject;
        if (targetObj == null || hitEnemies.Contains(targetObj)) return;

        // ตรวจจับเมื่อกระสุนชนวัตถุที่มี Tag เป็น Enemy หรือมี IDamageable
        IDamageable enemyDamageable = targetObj.GetComponent<IDamageable>() ?? collision.GetComponent<IDamageable>();
        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? collision.GetComponent<EnemyAI>();

        if (collision.CompareTag("Enemy") || enemyDamageable != null || enemyAI != null)
        {
            hitEnemies.Add(targetObj);

            bool isCrit = false;
            float finalDamage = damage;
            if (PlayerStats.Instance != null)
            {
                finalDamage = PlayerStats.Instance.RollCriticalDamage(damage, out isCrit);
            }

            if (enemyDamageable != null)
            {
                enemyDamageable.TakeDamage(finalDamage);
            }
            else if (enemyAI != null)
            {
                enemyAI.TakeDamage(finalDamage);
            }

            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.TryApplyMindShockOnCrit(targetObj, isCrit);
            }

            remainingPierces--;
            if (remainingPierces <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
