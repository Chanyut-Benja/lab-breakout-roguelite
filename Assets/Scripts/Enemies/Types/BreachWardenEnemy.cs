using Combat;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Breach Warden (Mini-Boss - Balance Sheet Section 8):
    /// Spawns at 01:00 (60 seconds into room) exclusively in rooms 2, 4, 5, 7, 8.
    /// Base HP: 1,500, Heavy Melee Slam, Drop EXP: 20, Awards 15 Nano-Credits on defeat.
    /// Counts towards the room enemy cap.
    /// </summary>
    public class BreachWardenEnemy : EnemyBase
    {
        private SpawnState spawnState;
        private ChaseState chaseState;

        [Header("Breach Warden (Mini-Boss) Stats")]
        [SerializeField] private float baseWardenHP = 1500f;
        [SerializeField] private float baseWardenSpeed = 1.8f;
        [SerializeField] private float baseWardenDamage = 16f;
        [SerializeField] private int baseExpDrop = 20;
        [SerializeField] private int creditReward = 15;

        public float BaseWardenHP => baseWardenHP;
        public float BaseWardenSpeed => baseWardenSpeed;
        public float BaseWardenDamage => baseWardenDamage;

        protected override void Awake()
        {
            base.Awake();

            // Imposing Mini-Boss presence (1.8x scale, dark crimson/gold cyber armor)
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.95f, 0.25f, 0.25f, 1f);
                transform.localScale = Vector3.one * 1.8f;
            }
        }

        protected override void InitializeStates()
        {
            chaseState = new ChaseState(this, stateMachine);
            spawnState = new SpawnState(this, stateMachine, chaseState, 0.35f);
            deadState = new DeadState(this, stateMachine, 0.2f);
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
                health.SetExactMaxHealth(baseWardenHP * hpMultiplier);
                health.SetExpDrop(baseExpDrop);
                health.OnDied -= HandleWardenDeath;
                health.OnDied += HandleWardenDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDied -= HandleWardenDeath;
            }
        }

        private void HandleWardenDeath(EnemyHealth deadHealth)
        {
            // Award Mini-Boss credits (+15 Credits according to Balance Sheet Page 10)
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddCredits(creditReward);
            }
            else if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.AddRunCredits(creditReward);
            }

            Debug.Log($"[BreachWarden] Mini-Boss defeated! Awarded {baseExpDrop} EXP and {creditReward} Credits.");
        }
    }

    public class BreachWarden : BreachWardenEnemy { }
    public class BreachWardenMiniBoss : BreachWardenEnemy { }
}

public class BreachWarden : Enemies.Types.BreachWardenEnemy { }
public class BreachWardenEnemy : Enemies.Types.BreachWardenEnemy { }
public class BreachWardenMiniBoss : Enemies.Types.BreachWardenEnemy { }
