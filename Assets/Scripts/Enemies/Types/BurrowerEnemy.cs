using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Burrower: Tunnels underground to remove player target lock.
    /// FSM flow: Chase -> Burrow (underground, invulnerable) -> Telegraph circle (0.8s) -> Emerge (AoE 10 dmg) -> Exposed (1.5s) -> Chase
    /// </summary>
    public class BurrowerEnemy : EnemyBase
    {
        private TelegraphIndicator telegraphIndicator;
        private SpawnState spawnState;
        private BurrowerChaseState chaseState;
        private BurrowState burrowState;
        private EmergeTelegraphState telegraphState;
        private EmergeState emergeState;
        private ExposedState exposedState;

        protected override void Awake()
        {
            telegraphIndicator = GetComponent<TelegraphIndicator>();
            if (telegraphIndicator == null)
            {
                telegraphIndicator = gameObject.AddComponent<TelegraphIndicator>();
            }

            base.Awake();
        }

        protected override void InitializeStates()
        {
            float telegraphTime = data != null ? data.EmergeTelegraphDuration : 0.8f;
            float exposedTime = data != null ? data.ExposedDuration : 1.5f;
            float maxBurrowTime = data != null ? data.BurrowDuration : 3.0f;

            exposedState = new ExposedState(this, stateMachine, null, exposedTime);
            emergeState = new EmergeState(this, stateMachine, exposedState, 0.2f);
            telegraphState = new EmergeTelegraphState(this, stateMachine, telegraphIndicator, emergeState, telegraphTime);
            burrowState = new BurrowState(this, stateMachine, telegraphState, maxBurrowTime);
            chaseState = new BurrowerChaseState(this, stateMachine, burrowState);

            // Connect exposed back to chase cleanly
            exposedState.SetNextState(chaseState);

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

        private class BurrowerChaseState : ChaseState
        {
            private readonly BurrowState burrowState;
            private float lastBurrowTime = -999f;

            public BurrowerChaseState(EnemyBase enemy, StateMachine stateMachine, BurrowState burrowState)
                : base(enemy, stateMachine)
            {
                this.burrowState = burrowState;
            }

            public override void Enter()
            {
                base.Enter();
                lastBurrowTime = Time.time;
            }

            public override void FixedTick()
            {
                base.FixedTick();

                if (enemy.Target == null) return;

                float cooldown = data != null ? data.AttackCooldown : 3.0f;
                if (Time.time >= lastBurrowTime + cooldown)
                {
                    stateMachine.ChangeState(burrowState);
                }
            }
        }
    }
}
