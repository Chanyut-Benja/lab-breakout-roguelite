using System;
using Enemies.Core;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Repair Bot summoned by The Doctor boss (Room 9).
    /// Emits a green restorative nanite beam to The Doctor, healing 2% of Max HP per second.
    /// Can be targeted and destroyed by the player (implements IDamageable).
    /// </summary>
    public class DoctorRepairBot : MonoBehaviour, IDamageable
    {
        [Header("Settings")]
        [SerializeField] private float maxHealth = 250f;
        [SerializeField] private float healRatePercentPerSec = 0.02f; // ซ่อม 2%/s
        [SerializeField] private float orbitRadius = 3.5f;
        [SerializeField] private float orbitSpeed = 45f; // Deg/s

        private float currentHealth = 250f;
        private DoctorBoss doctor;
        private LineRenderer tetherLine;
        private SpriteRenderer spriteRenderer;
        private float currentAngle;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.Minion);
            spriteRenderer.color = new Color(0.2f, 0.95f, 0.4f, 1f); // Neon Bio-Green
            transform.localScale = Vector3.one * 1.2f;

            SetupTether();
        }

        private void SetupTether()
        {
            tetherLine = GetComponent<LineRenderer>();
            if (tetherLine == null)
            {
                tetherLine = gameObject.AddComponent<LineRenderer>();
            }

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                tetherLine.material = new Material(shader);
            }

            tetherLine.useWorldSpace = true;
            tetherLine.startWidth = 0.08f;
            tetherLine.endWidth = 0.08f;
            tetherLine.startColor = new Color(0.2f, 1f, 0.4f, 0.8f);
            tetherLine.endColor = new Color(0.1f, 0.8f, 0.3f, 0.3f);
            tetherLine.positionCount = 2;
            tetherLine.enabled = true;
        }

        public void Initialize(DoctorBoss targetDoctor, float initialAngle = 0f)
        {
            currentHealth = maxHealth;
            doctor = targetDoctor;
            currentAngle = initialAngle;
            if (doctor != null)
            {
                transform.position = doctor.transform.position + new Vector3(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad), 0f) * orbitRadius;
            }
        }

        private void Update()
        {
            if (doctor == null || doctor.Health == null || doctor.Health.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            // 1. Orbit smooth movement around Doctor
            currentAngle += orbitSpeed * Time.deltaTime;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 targetPos = doctor.transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);

            // 2. Heal Doctor 2% of Max HP per second
            float healAmount = doctor.Health.MaxHealth * healRatePercentPerSec * Time.deltaTime;
            doctor.Health.Heal(healAmount);

            // 3. Render Tether Beam to Doctor
            if (tetherLine != null)
            {
                tetherLine.enabled = true;
                tetherLine.SetPosition(0, transform.position);
                tetherLine.SetPosition(1, doctor.transform.position);
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
                spriteRenderer.color = new Color(0.2f, 0.95f, 0.4f, 1f);
            }
        }

        private void Die()
        {
            if (tetherLine != null) tetherLine.enabled = false;
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (tetherLine != null) tetherLine.enabled = false;
        }
    }
}
