using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Dead State for FSM.
    /// Halts movement, disables collision, and triggers pool-friendly despawn.
    /// </summary>
    public class DeadState : EnemyState
    {
        private readonly float despawnDelay;

        public DeadState(EnemyBase enemy, StateMachine stateMachine, float despawnDelay = 0.05f)
            : base(enemy, stateMachine)
        {
            this.despawnDelay = despawnDelay;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            if (enemy.Collider != null)
            {
                enemy.Collider.enabled = false;
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Die");
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= despawnDelay)
            {
                enemy.Despawn();
            }
        }
    }
}
