using Combat;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Sentinel Bot (Balance Sheet Section 8):
    /// Ranged artillery bot appearing from Room 2 onwards.
    /// Base HP: 35, Drop EXP: 1.
    /// </summary>
    public class SentinelBotEnemy : EnemyBase
    {
        private TelegraphIndicator telegraphIndicator;
        private SpawnState spawnState;
        private RangedChaseState chaseState;
        private AimState aimState;
        private ShootState shootState;

        [Header("Sentinel Bot Base Stats (Balance Sheet)")]
        [SerializeField] private float baseSentinelHP = 35f;
        [SerializeField] private float baseSentinelSpeed = 1.6f;
        [SerializeField] private int baseExpDrop = 1;
        [SerializeField] private GameObject projectilePrefab;

        public float BaseSentinelHP => baseSentinelHP;
        public float BaseSentinelSpeed => baseSentinelSpeed;

        protected override void Awake()
        {
            telegraphIndicator = GetComponent<TelegraphIndicator>();
            if (telegraphIndicator == null)
            {
                telegraphIndicator = gameObject.AddComponent<TelegraphIndicator>();
            }

            base.Awake();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.3f, 0.9f, 0.5f, 1f); // Neon Energy Green
                transform.localScale = Vector3.one * 1.05f;
            }
        }

        protected override void InitializeStates()
        {
            float aimTime = data != null ? data.TelegraphDuration : 0.4f;

            shootState = new ShootState(this, stateMachine, null, 0.12f);
            aimState = new AimState(this, stateMachine, telegraphIndicator, shootState, aimTime);
            chaseState = new RangedChaseState(this, stateMachine, aimState);

            shootState.SetReturnState(chaseState);

            spawnState = new SpawnState(this, stateMachine, chaseState, 0.2f);
            deadState = new DeadState(this, stateMachine, 0.1f);
        }

        protected override void StartInitialState()
        {
            if (stateMachine != null && spawnState != null)
            {
                stateMachine.Initialize(spawnState);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (IsInitialized && stateMachine != null && spawnState != null)
            {
                stateMachine.ChangeState(spawnState);
            }
        }

        public override void Init(Transform target, float hpMultiplier, float damageMultiplier, float speedMultiplier)
        {
            base.Init(target, hpMultiplier, damageMultiplier, speedMultiplier);

            if (health != null)
            {
                health.SetExactMaxHealth(baseSentinelHP * hpMultiplier);
                health.SetExpDrop(baseExpDrop);
            }
        }

        public GameObject GetProjectilePrefab()
        {
            if (projectilePrefab != null) return projectilePrefab;
            if (data != null && data.ProjectilePrefab != null) return data.ProjectilePrefab;
            return null;
        }
    }

    public class SentinelBot : SentinelBotEnemy { }
}

public class SentinelBot : Enemies.Types.SentinelBotEnemy { }
public class SentinelBotEnemy : Enemies.Types.SentinelBotEnemy { }
