using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Abstract base class for all enemy FSM states.
    /// Provides access to EnemyBase, StateMachine, and EnemyData.
    /// </summary>
    public abstract class EnemyState : IState
    {
        protected readonly EnemyBase enemy;
        protected readonly StateMachine stateMachine;
        protected EnemyData data => enemy != null ? enemy.Data : null;

        protected float stateStartTime;
        public float StateElapsedTime => Time.time - stateStartTime;

        public EnemyState(EnemyBase enemy, StateMachine stateMachine)
        {
            this.enemy = enemy;
            this.stateMachine = stateMachine;
        }

        public virtual void Enter()
        {
            stateStartTime = Time.time;
        }

        public virtual void Tick()
        {
        }

        public virtual void FixedTick()
        {
        }

        public virtual void Exit()
        {
        }
    }
}
