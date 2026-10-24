using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Runner: Fast and fragile swarm mob.
    /// FSM flow: Spawn -> Chase (Fast) -> Dead
    /// Forces the player to keep moving and repositions aggressively in swarms.
    /// </summary>
    public class RunnerEnemy : EnemyBase
    {
        private SpawnState spawnState;
        private ChaseState chaseState;

        protected override void InitializeStates()
        {
            chaseState = new ChaseState(this, stateMachine);
            spawnState = new SpawnState(this, stateMachine, chaseState, 0.15f);
            deadState = new DeadState(this, stateMachine, 0.08f);
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
