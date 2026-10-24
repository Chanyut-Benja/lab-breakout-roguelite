using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Telegraph state for Dash attacks.
    /// Locks the target direction at the beginning of the telegraph and displays a warning line.
    /// Complies with the >= 0.4 - 0.8s fairness rule (0.7s for Dasher).
    /// </summary>
    public class TelegraphDashState : EnemyState
    {
        private readonly TelegraphIndicator telegraphIndicator;
        private readonly IState dashState;
        private Vector2 lockedDirection;
        private readonly float duration;

        public Vector2 LockedDirection => lockedDirection;

        public TelegraphDashState(EnemyBase enemy, StateMachine stateMachine, TelegraphIndicator indicator, IState dashState, float duration = 0.7f)
            : base(enemy, stateMachine)
        {
            this.telegraphIndicator = indicator;
            this.dashState = dashState;
            this.duration = duration;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            // Lock direction towards player now
            if (enemy.Target != null)
            {
                Vector2 diff = enemy.Target.position - enemy.transform.position;
                lockedDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.right;
            }
            else
            {
                lockedDirection = Vector2.right;
            }

            enemy.UpdateFacing(lockedDirection.x);

            // Pass direction to dash state immediately so it is prepared
            if (dashState is DashState dsEnter)
            {
                dsEnter.SetDirection(lockedDirection);
            }

            // Display directional line telegraph
            if (telegraphIndicator != null)
            {
                float dashDistance = (data != null ? data.DashSpeed * data.DashDuration : 3.6f);
                telegraphIndicator.ShowLine(enemy.transform.position, lockedDirection, dashDistance, 0.25f);
            }

            // Play Telegraph SFX
            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Telegraph");
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= duration)
            {
                if (telegraphIndicator != null)
                {
                    telegraphIndicator.Hide();
                }

                if (dashState is DashState dsTick)
                {
                    dsTick.SetDirection(lockedDirection);
                }

                stateMachine.ChangeState(dashState);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (telegraphIndicator != null)
            {
                telegraphIndicator.Hide();
            }

            if (dashState is DashState dsExit)
            {
                dsExit.SetDirection(lockedDirection);
            }
        }
    }
}
