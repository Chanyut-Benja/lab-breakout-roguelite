using Enemies.Core;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Shield Drone summoned by Overlord (Room 18) in Phase 2 (HP <= 66%).
    /// Projects an impenetrable force field over the Overlord, granting invulnerability
    /// until the player destroys the Shield Drone.
    /// </summary>
    public class OverlordShieldDrone : MonoBehaviour, IDamageable
    {
        [Header("Settings")]
        [SerializeField] private float maxHealth = 450f;
        [SerializeField] private float orbitRadius = 4.0f;
        [SerializeField] private float orbitSpeed = 50f;

        private float currentHealth = 450f;
        private OverlordBoss overlord;
        private LineRenderer shieldBeam;
        private SpriteRenderer spriteRenderer;
        private float currentAngle;

        public float CurrentHealth => currentHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.Minion);
            spriteRenderer.color = new Color(0f, 0.85f, 1f, 1f); // Neon Cyan Shield Core
            transform.localScale = Vector3.one * 1.35f;

            SetupShieldBeam();
        }

        private void SetupShieldBeam()
        {
            shieldBeam = GetComponent<LineRenderer>();
            if (shieldBeam == null)
            {
                shieldBeam = gameObject.AddComponent<LineRenderer>();
            }

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                shieldBeam.material = new Material(shader);
            }

            shieldBeam.useWorldSpace = true;
            shieldBeam.startWidth = 0.12f;
            shieldBeam.endWidth = 0.12f;
            shieldBeam.startColor = new Color(0f, 0.85f, 1f, 0.9f);
            shieldBeam.endColor = new Color(0.2f, 0.5f, 1f, 0.4f);
            shieldBeam.positionCount = 2;
            shieldBeam.enabled = true;
        }

        public void Initialize(OverlordBoss targetOverlord, float initialAngle = 90f)
        {
            currentHealth = maxHealth;
            overlord = targetOverlord;
            currentAngle = initialAngle;
            if (overlord != null)
            {
                transform.position = overlord.transform.position + new Vector3(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad), 0f) * orbitRadius;
                if (overlord.Health != null)
                {
                    overlord.Health.IsInvulnerable = true;
                }
            }
        }

        private void Update()
        {
            if (overlord == null || overlord.Health == null || overlord.Health.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            // Ensure Overlord remains shielded while drone is alive
            overlord.Health.IsInvulnerable = true;

            // Orbit Overlord
            currentAngle += orbitSpeed * Time.deltaTime;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 targetPos = overlord.transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 6f);

            // Connect shield beam
            if (shieldBeam != null)
            {
                shieldBeam.enabled = true;
                shieldBeam.SetPosition(0, transform.position);
                shieldBeam.SetPosition(1, overlord.transform.position);
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Vector2.zero);
        }

        public void TakeDamage(float amount, Vector2 knockback)
        {
            if (currentHealth <= 0f) return;

            currentHealth -= amount;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                Invoke(nameof(ResetColor), 0.08f);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void ResetColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0f, 0.85f, 1f, 1f);
            }
        }

        private void Die()
        {
            if (overlord != null && overlord.Health != null)
            {
                overlord.Health.IsInvulnerable = false;
                Debug.Log($"[{overlord.name}] Shield Drone destroyed! Invulnerability removed!");
            }

            if (shieldBeam != null) shieldBeam.enabled = false;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDisable()
        {
            if (overlord != null && overlord.Health != null)
            {
                overlord.Health.IsInvulnerable = false;
            }
            if (shieldBeam != null) shieldBeam.enabled = false;
        }
    }
}
