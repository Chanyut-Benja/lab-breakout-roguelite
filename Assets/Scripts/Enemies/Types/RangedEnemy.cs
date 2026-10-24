using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Ranged: Applies long range pressure.
    /// FSM flow: Chase to sweet spot (~6m) -> Aim (0.4s) -> Shoot slow projectile -> Cooldown 2s (kites away if player < 3m)
    /// </summary>
    public class RangedEnemy : EnemyBase
    {
        private TelegraphIndicator telegraphIndicator;
        private SpawnState spawnState;
        private RangedChaseState chaseState;
        private AimState aimState;
        private ShootState shootState;

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
            float aimTime = data != null ? data.TelegraphDuration : 0.4f;

            shootState = new ShootState(this, stateMachine, null, 0.1f);
            aimState = new AimState(this, stateMachine, telegraphIndicator, shootState, aimTime);
            chaseState = new RangedChaseState(this, stateMachine, aimState);

            // Connect shoot return state to chase cleanly
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
    }
}
