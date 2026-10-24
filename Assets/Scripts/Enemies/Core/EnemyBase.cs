using System;
using Enemies.States;
using UnityEngine;

namespace Enemies.Core
{
    /// <summary>
    /// Base class for all enemies.
    /// Manages FSM lifecycle, movement steering with separation, target tracking, and pool-friendly despawning.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Data Configuration")]
        [SerializeField] protected EnemyData data;

        [Header("Components")]
        [SerializeField] protected Rigidbody2D rb;
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;
        [SerializeField] protected EnemyHealth health;
        [SerializeField] protected Collider2D enemyCollider;

        [Header("Separation Settings")]
        [SerializeField] private LayerMask enemyLayerMask;
        [SerializeField] private int separationFrameInterval = 3;

        // Runtime FSM
        protected StateMachine stateMachine;
        protected DeadState deadState;

        public StateMachine StateMachine => stateMachine;
        public EnemyData Data => data;
        public Rigidbody2D Rigidbody => rb;
        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public EnemyHealth Health => health;
        public Collider2D Collider => enemyCollider;

        // Target & Difficulty
        public Transform Target { get; protected set; }
        public float DifficultyMultiplier { get; protected set; } = 1f;
        public float HPMultiplier { get; protected set; } = 1f;
        public float SpeedMultiplier { get; protected set; } = 1f;
        public bool IsInitialized { get; protected set; }

        // Status Effects (Mind Shock Stun & Slow)
        protected float stunRemainingTimer = 0f;
        protected float slowRemainingTimer = 0f;
        protected float currentSlowPercent = 0f;
        public bool IsStunned => stunRemainingTimer > 0f;
        public bool IsSlowed => slowRemainingTimer > 0f;
        public float SlowMultiplier => slowRemainingTimer > 0f ? Mathf.Clamp01(1f - currentSlowPercent) : 1f;

        // Separation cache (zero garbage collection)
        private static readonly Collider2D[] separationBuffer = new Collider2D[16];
        private Vector2 cachedSeparationVector = Vector2.zero;
        private int frameCounter = 0;

        // Despawn action for object pooling hook
        public Action<EnemyBase> OnDespawnRequested;

        protected virtual void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            // The exported prefabs reference a sprite/material absent from the package.
            ProceduralSprites.EnsureVisible(spriteRenderer,
                this is Enemies.Bosses.BossBase ? ProceduralSprites.Boss : ProceduralSprites.Minion);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (health == null) health = GetComponent<EnemyHealth>();
            if (enemyCollider == null) enemyCollider = GetComponent<Collider2D>();

            // Setup layer mask fallback if not assigned
            if (enemyLayerMask == 0)
            {
                enemyLayerMask = 1 << gameObject.layer;
            }

            stateMachine = new StateMachine();
            InitializeStates();
        }

        protected virtual void OnEnable()
        {
            // Reset physics and state when retrieved from pool
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (enemyCollider != null)
            {
                enemyCollider.enabled = true;
            }

            cachedSeparationVector = Vector2.zero;
            stunRemainingTimer = 0f;
            slowRemainingTimer = 0f;
            currentSlowPercent = 0f;
            frameCounter = UnityEngine.Random.Range(0, separationFrameInterval); // Desynchronize frame checks
        }

        protected virtual void Update()
        {
            if (slowRemainingTimer > 0f)
            {
                slowRemainingTimer -= Time.deltaTime;
                if (slowRemainingTimer <= 0f)
                {
                    slowRemainingTimer = 0f;
                    currentSlowPercent = 0f;
                }
            }

            if (stunRemainingTimer > 0f)
            {
                stunRemainingTimer -= Time.deltaTime;
                StopMovement();
                return;
            }

            stateMachine?.Tick();
        }

        protected virtual void FixedUpdate()
        {
            if (IsStunned)
            {
                StopMovement();
                return;
            }

            stateMachine?.FixedTick();
        }

        /// <summary>
        /// ทำให้ศัตรูติดสถานะ Stun หยุดการเคลื่อนที่และการโจมตีชั่วคราว (เช่นจาก Passive Mind Shock ของ N-02 Psionic)
        /// </summary>
        public virtual void ApplyStun(float duration)
        {
            if (duration <= 0f) return;
            stunRemainingTimer = Mathf.Max(stunRemainingTimer, duration);
            StopMovement();
            Debug.Log($"[{name}] Stunned for {duration:F1}s (Mind Shock)!");
        }

        /// <summary>
        /// ทำให้ศัตรูเคลื่อนที่ช้าลงตามเปอร์เซ็นต์ (เช่น Toxic Spore 15%->30% หรือ Magnetic Pulse 20%)
        /// </summary>
        public virtual void ApplySlow(float slowPercent, float duration)
        {
            if (slowPercent <= 0f || duration <= 0f) return;
            currentSlowPercent = Mathf.Clamp(Mathf.Max(currentSlowPercent, slowPercent), 0f, 0.85f);
            slowRemainingTimer = Mathf.Max(slowRemainingTimer, duration);
        }

        /// <summary>
        /// Contract with external systems (spawner, rooms).
        /// Injects target and difficulty without expensive Find() calls.
        /// </summary>
        public virtual void Init(Transform target, float difficultyMultiplier = 1f)
        {
            Init(target, difficultyMultiplier, difficultyMultiplier, 1f);
        }

        public virtual void Init(Transform target, float hpMultiplier, float damageMultiplier, float speedMultiplier)
        {
            this.Target = target;
            this.HPMultiplier = Mathf.Max(0.1f, hpMultiplier);
            this.DifficultyMultiplier = Mathf.Max(0.1f, damageMultiplier);
            this.SpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            this.IsInitialized = true;

            if (health != null)
            {
                health.Initialize(this, data, this.HPMultiplier);
            }

            StartInitialState();
        }

        /// <summary>
        /// Configure state classes for this enemy type.
        /// </summary>
        protected abstract void InitializeStates();

        /// <summary>
        /// Starts the starting state (e.g. SpawnState or ChaseState).
        /// </summary>
        protected abstract void StartInitialState();

        /// <summary>
        /// Calculates direct steering vector toward Target combined with separation to prevent crowding.
        /// Throttled to every N frames to ensure high performance with 100+ enemies.
        /// </summary>
        public virtual Vector2 CalculateSteeringDirection()
        {
            if (Target == null) return Vector2.zero;

            Vector2 toTarget = (Vector2)(Target.position - transform.position);
            float dist = toTarget.magnitude;
            if (dist < 0.001f) return Vector2.zero;

            Vector2 directDir = toTarget / dist;

            // Frame-throttled separation query
            frameCounter++;
            if (frameCounter >= separationFrameInterval)
            {
                frameCounter = 0;
                cachedSeparationVector = CalculateSeparationVector();
            }

            float separationWeight = data != null ? data.SeparationWeight : 0.4f;
            Vector2 combined = directDir + (cachedSeparationVector * separationWeight);
            return combined.sqrMagnitude > 0.001f ? combined.normalized : directDir;
        }

        private Vector2 CalculateSeparationVector()
        {
            float radius = data != null ? data.SeparationRadius : 0.8f;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(enemyLayerMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(transform.position, radius, filter, separationBuffer);

            Vector2 separation = Vector2.zero;
            int neighbors = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = separationBuffer[i];
                if (col == null || col == enemyCollider || col.gameObject == gameObject) continue;

                Vector2 diff = (Vector2)transform.position - (Vector2)col.transform.position;
                float distSqr = diff.sqrMagnitude;
                if (distSqr > 0.0001f)
                {
                    // Stronger push the closer the neighbor is
                    separation += diff.normalized / Mathf.Sqrt(distSqr);
                    neighbors++;
                }
            }

            if (neighbors > 0)
            {
                separation /= neighbors;
            }

            return separation;
        }

        /// <summary>
        /// Applies movement velocity directly to the Rigidbody2D.
        /// </summary>
        public virtual void Move(Vector2 direction, float speed)
        {
            if (rb == null) return;
            if (IsStunned)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = direction * (speed * SpeedMultiplier * SlowMultiplier);

            // Update sprite facing direction
            UpdateFacing(direction.x);
        }

        /// <summary>
        /// Stops Rigidbody2D movement immediately.
        /// </summary>
        public virtual void StopMovement()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Faces the sprite left or right based on movement direction or target position.
        /// </summary>
        public virtual void UpdateFacing(float xDirection)
        {
            if (spriteRenderer == null) return;

            if (Mathf.Abs(xDirection) > 0.05f)
            {
                spriteRenderer.flipX = xDirection < 0;
            }
        }

        protected virtual void OnCollisionStay2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                HandlePlayerContact(collision.gameObject);
            }
        }

        protected virtual void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                HandlePlayerContact(other.gameObject);
            }
        }

        public virtual void HandlePlayerContact(GameObject playerObj)
        {
            if (stateMachine != null && stateMachine.CurrentState is ChaseState chaseState)
            {
                chaseState.TryDealContactDamage(playerObj);
            }
        }

        /// <summary>
        /// Called when health reaches zero. Transitions FSM to DeadState.
        /// </summary>
        public virtual void OnDeath()
        {
            if (deadState != null && stateMachine != null)
            {
                stateMachine.ChangeState(deadState);
            }
            else
            {
                Despawn();
            }
        }

        /// <summary>
        /// Pool-friendly despawn contract.
        /// Instead of Destroy(), resets enemy and deactivates or notifies pool.
        /// </summary>
        public virtual void Despawn()
        {
            StopMovement();

            if (OnDespawnRequested != null)
            {
                OnDespawnRequested.Invoke(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void SetData(EnemyData newData)
        {
            this.data = newData;
        }
    }
}
