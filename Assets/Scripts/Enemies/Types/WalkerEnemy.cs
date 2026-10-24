using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Walker: The primary swarm mob.
    /// FSM flow: Spawn -> Chase (deals contact damage every 0.5s when touching player) -> Dead
    /// </summary>
    public class WalkerEnemy : EnemyBase
    {
        private SpawnState spawnState;
        private ChaseState chaseState;

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

            // When pulled from pool, reset state back to spawnState if already initialized
            if (IsInitialized && stateMachine != null && spawnState != null)
            {
                stateMachine.ChangeState(spawnState);
            }
        }
    }
}
