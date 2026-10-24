using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Scrap Drone (Balance Sheet Section 8):
    /// Primary frontline swarm mob appearing from Room 1 onwards.
    /// Base HP: 20, Drop EXP: 1.
    /// </summary>
    public class ScrapDroneEnemy : EnemyBase
    {
        private SpawnState spawnState;
        private ChaseState chaseState;

        [Header("Scrap Drone Base Stats (Balance Sheet)")]
        [SerializeField] private float baseScrapDroneHP = 20f;
        [SerializeField] private float baseScrapDroneSpeed = 2.2f;
        [SerializeField] private float baseScrapDroneDamage = 5f;
        [SerializeField] private int baseExpDrop = 1;

        public float BaseScrapDroneHP => baseScrapDroneHP;
        public float BaseScrapDroneSpeed => baseScrapDroneSpeed;
        public float BaseScrapDroneDamage => baseScrapDroneDamage;

        protected override void Awake()
        {
            base.Awake();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.75f, 0.78f, 0.82f, 1f); // Metallic Scrap
                transform.localScale = Vector3.one * 0.95f;
            }
        }

        protected override void InitializeStates()
        {
            chaseState = new ChaseState(this, stateMachine);
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
                health.SetExactMaxHealth(baseScrapDroneHP * hpMultiplier);
                health.SetExpDrop(baseExpDrop);
            }
        }
    }

    public class ScrapDrone : ScrapDroneEnemy { }
}

public class ScrapDrone : Enemies.Types.ScrapDroneEnemy { }
public class ScrapDroneEnemy : Enemies.Types.ScrapDroneEnemy { }
