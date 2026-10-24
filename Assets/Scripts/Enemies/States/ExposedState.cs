using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Post-emergence exposed state for Burrower.
    /// Enemy remains stationary and completely vulnerable to player attacks for 1.5s per spec.
    /// </summary>
    public class ExposedState : EnemyState
    {
        private IState nextState;
        private readonly float duration;

        public ExposedState(EnemyBase enemy, StateMachine stateMachine, IState nextState, float duration = 1.5f)
            : base(enemy, stateMachine)
        {
            this.nextState = nextState;
            this.duration = duration;
        }

        public void SetNextState(IState next)
        {
            this.nextState = next;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            if (enemy.Health != null)
            {
                enemy.Health.IsInvulnerable = false;
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetBool("IsMoving", false);
                enemy.Animator.SetTrigger("Recover");
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= duration)
            {
                stateMachine.ChangeState(nextState);
            }
        }
    }
}
