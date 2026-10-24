using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Dasher: Tests player dodging reflex.
    /// FSM flow: Chase -> (dist <= 5 & cd ready) -> Telegraph (0.7s) -> Dash (0.4s) -> Recover (1.0s) -> Chase
    /// </summary>
    public class DasherEnemy : EnemyBase
    {
        private TelegraphIndicator telegraphIndicator;
        private SpawnState spawnState;
        private DasherChaseState chaseState;
        private TelegraphDashState telegraphState;
        private DashState dashState;
        private RecoverState recoverState;

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
            float telegraphTime = data != null ? data.TelegraphDuration : 0.7f;
            float dashTime = data != null ? data.DashDuration : 0.4f;
            float dashSpd = data != null ? data.DashSpeed : 9f;
            float recoverTime = data != null ? data.RecoverDuration : 1.0f;

            recoverState = new RecoverState(this, stateMachine, null, recoverTime);
            dashState = new DashState(this, stateMachine, recoverState, dashTime, dashSpd);
            telegraphState = new TelegraphDashState(this, stateMachine, telegraphIndicator, dashState, telegraphTime);
            chaseState = new DasherChaseState(this, stateMachine, telegraphState);

            // Connect recover back to chase cleanly
            recoverState.SetNextState(chaseState);

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

        private class DasherChaseState : ChaseState
        {
            private readonly TelegraphDashState telegraphState;
            private float lastDashTime = -999f;

            public DasherChaseState(EnemyBase enemy, StateMachine stateMachine, TelegraphDashState telegraphState)
                : base(enemy, stateMachine)
            {
                this.telegraphState = telegraphState;
            }

            public override void FixedTick()
            {
                base.FixedTick();

                if (enemy.Target == null) return;

                float dist = Vector2.Distance(enemy.transform.position, enemy.Target.position);
                float triggerDist = data != null ? data.DashTriggerDistance : 5.0f;
                float cooldown = data != null ? data.AttackCooldown : 2.5f;

                if (dist <= triggerDist && Time.time >= lastDashTime + cooldown)
                {
                    lastDashTime = Time.time;
                    stateMachine.ChangeState(telegraphState);
                }
            }
        }
    }
}
