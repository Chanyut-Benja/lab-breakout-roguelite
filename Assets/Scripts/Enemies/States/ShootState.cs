using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Executes projectile firing for Ranged enemies and Boss fan shots.
    /// Spawns an EnemyProjectile, applies damage and speed, then returns to chase/kite.
    /// </summary>
    public class ShootState : EnemyState
    {
        private RangedChaseState returnState;
        private Vector2 shootDirection;
        private readonly float shootDelay;

        public ShootState(EnemyBase enemy, StateMachine stateMachine, RangedChaseState returnState, float shootDelay = 0.1f)
            : base(enemy, stateMachine)
        {
            this.returnState = returnState;
            this.shootDelay = shootDelay;
        }

        public void SetReturnState(RangedChaseState returnState)
        {
            this.returnState = returnState;
        }

        public void SetDirection(Vector2 direction)
        {
            this.shootDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            // Robust fallback if shootDirection was not set
            if (shootDirection == Vector2.zero)
            {
                if (enemy.Target != null)
                {
                    Vector2 diff = (Vector2)(enemy.Target.position - enemy.transform.position);
                    shootDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.right;
                }
                else
                {
                    shootDirection = (enemy.SpriteRenderer != null && enemy.SpriteRenderer.flipX) ? Vector2.left : Vector2.right;
                }
            }

            enemy.UpdateFacing(shootDirection.x);

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Attack");
            }

            // Play Attack/Shoot SFX
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }

            FireProjectile();
        }

        private void FireProjectile()
        {
            if (data == null || data.ProjectilePrefab == null) return;

            Vector3 spawnPos = enemy.transform.position + (Vector3)(shootDirection * 0.4f);
            GameObject projObj = Object.Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.identity);

            EnemyProjectile projectile = projObj.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                float dmg = data.ProjectileDamage * enemy.DifficultyMultiplier;
                float spd = data.ProjectileSpeed;
                projectile.Setup(shootDirection, dmg, spd);
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= shootDelay)
            {
                returnState?.NotifyShotFired();
                stateMachine.ChangeState(returnState);
            }
        }
    }
}
