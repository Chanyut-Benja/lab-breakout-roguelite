using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 7f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float lifeTime = 4f;

    private Vector2 direction;
    private float timer;
    private bool isReflected = false;

    public bool IsReflected => isReflected;

    private void Awake()
    {
        ProceduralSprites.EnsureVisible(GetComponentInChildren<SpriteRenderer>(), ProceduralSprites.Shot);
    }

    public void Setup(Vector2 shootDirection, float customDamage = -1f, float customSpeed = -1f)
    {
        direction = shootDirection.normalized;
        isReflected = false;

        if (customDamage > 0f) damage = customDamage;
        if (customSpeed > 0f) speed = customSpeed;

        timer = 0f;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// สะท้อนกระสุนกลับไปหาศัตรู (จาก Magnetic Pulse Boon)
    /// </summary>
    public void ReflectProjectile()
    {
        if (isReflected) return;
        isReflected = true;
        direction = -direction;
        timer = 0f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.55f, 0.90f, 1f, 1f);
        }
    }

    private void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime);
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;

        if (isReflected)
        {
            if (collision.CompareTag("Player")) return;

            GameObject targetObj = collision.attachedRigidbody != null ? collision.attachedRigidbody.gameObject : collision.gameObject;
            if (targetObj == null || targetObj.CompareTag("Player")) return;

            IDamageable enemyDmg = targetObj.GetComponent<IDamageable>() ?? collision.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
            EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? collision.GetComponent<EnemyAI>();

            if (collision.CompareTag("Enemy") || enemyDmg != null || enemyAI != null)
            {
                if (enemyDmg != null) enemyDmg.TakeDamage(damage * 2f);
                else if (enemyAI != null) enemyAI.TakeDamage(damage * 2f);
                Destroy(gameObject);
            }
            return;
        }

        if (collision.CompareTag("Player"))
        {
            IDamageable playerDamageable = collision.GetComponent<IDamageable>();
            if (playerDamageable != null)
            {
                playerDamageable.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
    }
}
