using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Immobile vulnerable recovery state following big attacks.
    /// Gives the player a fair reward window for successfully dodging telegraphed attacks.
    /// </summary>
    public class RecoverState : EnemyState
    {
        private IState nextState;
        private readonly float duration;

        public RecoverState(EnemyBase enemy, StateMachine stateMachine, IState nextState, float duration = 1.0f)
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

        public override void Exit()
        {
            base.Exit();
        }
    }
}
