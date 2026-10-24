using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Aim telegraph state for Ranged enemies.
    /// Halts movement, locks firing angle toward player for 0.4s telegraph.
    /// </summary>
    public class AimState : EnemyState
    {
        private readonly TelegraphIndicator telegraphIndicator;
        private readonly IState shootState;
        private Vector2 aimDirection;
        private readonly float aimDuration;

        public Vector2 AimDirection => aimDirection;

        public AimState(EnemyBase enemy, StateMachine stateMachine, TelegraphIndicator indicator, IState shootState, float aimDuration = 0.4f)
            : base(enemy, stateMachine)
        {
            this.telegraphIndicator = indicator;
            this.shootState = shootState;
            this.aimDuration = aimDuration;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            if (enemy.Target != null)
            {
                Vector2 diff = enemy.Target.position - enemy.transform.position;
                aimDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.right;
            }
            else
            {
                aimDirection = Vector2.right;
            }

            enemy.UpdateFacing(aimDirection.x);

            // Pass aim direction to shootState immediately so projectile shoots along the telegraph line
            if (shootState is ShootState ssEnter)
            {
                ssEnter.SetDirection(aimDirection);
            }

            // Display subtle aim line
            if (telegraphIndicator != null)
            {
                telegraphIndicator.ShowLine(enemy.transform.position, aimDirection, 5f, 0.08f);
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Telegraph");
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= aimDuration)
            {
                if (telegraphIndicator != null)
                {
                    telegraphIndicator.Hide();
                }

                if (shootState is ShootState ssTick)
                {
                    ssTick.SetDirection(aimDirection);
                }

                stateMachine.ChangeState(shootState);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (telegraphIndicator != null)
            {
                telegraphIndicator.Hide();
            }

            if (shootState is ShootState ssExit)
            {
                ssExit.SetDirection(aimDirection);
            }
        }
    }
}
