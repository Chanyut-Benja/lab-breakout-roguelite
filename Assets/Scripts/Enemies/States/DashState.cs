using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Executes rapid dash in a fixed direction without steering.
    /// Deals high collision damage to player upon impact.
    /// Transitions to RecoverState upon completion.
    /// </summary>
    public class DashState : EnemyState
    {
        private readonly IState nextState;
        private Vector2 dashDirection;
        private readonly float duration;
        private readonly float speed;
        private bool hasDealtDashDamage = false;

        public DashState(EnemyBase enemy, StateMachine stateMachine, IState nextState, float duration = 0.4f, float speed = 9f)
            : base(enemy, stateMachine)
        {
            this.nextState = nextState;
            this.duration = duration;
            this.speed = speed;
        }

        public void SetDirection(Vector2 direction)
        {
            this.dashDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        }

        public override void Enter()
        {
            base.Enter();
            hasDealtDashDamage = false;

            // Robust fallback if direction wasn't explicitly set
            if (dashDirection == Vector2.zero)
            {
                if (enemy.Target != null)
                {
                    Vector2 diff = (Vector2)(enemy.Target.position - enemy.transform.position);
                    dashDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.right;
                }
                else
                {
                    dashDirection = (enemy.SpriteRenderer != null && enemy.SpriteRenderer.flipX) ? Vector2.left : Vector2.right;
                }
            }

            enemy.UpdateFacing(dashDirection.x);

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Attack");
            }

            // Play Attack SFX
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }
        }

        public override void FixedTick()
        {
            base.FixedTick();

            // Drive Rigidbody2D forward in fixed direction
            enemy.Move(dashDirection, speed);

            // Proximity check for dash damage
            if (!hasDealtDashDamage && enemy.Target != null)
            {
                float dist = Vector2.Distance(enemy.transform.position, enemy.Target.position);
                if (dist <= (data != null ? data.AttackRange : 1.0f) + 0.4f)
                {
                    IDamageable damageable = enemy.Target.GetComponentInChildren<IDamageable>();
                    if (damageable != null)
                    {
                        float dashDmg = (data != null ? data.ContactDamage * 1.5f : 15f) * enemy.DifficultyMultiplier;
                        Vector2 knockback = dashDirection * 8f;
                        damageable.TakeDamage(dashDmg, knockback);
                        hasDealtDashDamage = true;
                    }
                }
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= duration)
            {
                enemy.StopMovement();
                stateMachine.ChangeState(nextState);
            }
        }

        public override void Exit()
        {
            base.Exit();
            enemy.StopMovement();
        }
    }
}
