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

    private SpriteRenderer blinkArt;
    private Sprite[] blinkFrames;

    private void Awake()
    {
        ProceduralSprites.EnsureVisible(GetComponentInChildren<SpriteRenderer>(), ProceduralSprites.Shot);
        SpriteRenderer art = GetComponentInChildren<SpriteRenderer>();
        Sprite shot = SkinLibrary.ProjectileSprite();
        if (art != null && shot != null) { art.sprite = shot; art.color = Color.white; }
        // the two dots are two frames: blink between them
        Sprite[] frames = SkinLibrary.ProjectileFrames();
        if (art != null && frames != null && frames.Length == 2)
        {
            blinkArt = art;
            blinkFrames = frames;
            art.sprite = frames[0];
        }
    }

    public void Setup(Vector2 shootDirection, float customDamage = -1f, float customSpeed = -1f)
    {
        direction = shootDirection.normalized;
        isReflected = false;

        if (customDamage > 0f) damage = customDamage * 0.55f;   // enemy shots hit softer
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
        if (blinkArt != null) blinkArt.sprite = blinkFrames[((int)(Time.time / 0.1f)) & 1];
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
