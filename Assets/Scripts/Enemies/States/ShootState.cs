using Combat;
using Enemies.Core;
using Enemies.Types;
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
            GameObject prefabToSpawn = data != null ? data.ProjectilePrefab : null;
            if (enemy is SpitterEnemy spitter)
            {
                GameObject customAcidPrefab = spitter.GetAcidProjectilePrefab();
                if (customAcidPrefab != null) prefabToSpawn = customAcidPrefab;
            }

            Vector3 spawnPos = enemy.transform.position + (Vector3)(shootDirection * 0.4f);
            GameObject projObj = null;

            if (prefabToSpawn != null)
            {
                projObj = Object.Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            }
            else if (enemy is SpitterEnemy)
            {
                // Dynamic fallback for Spitter Acid Projectile
                projObj = new GameObject("AcidProjectile");
                projObj.transform.position = spawnPos;
                SpriteRenderer sr = projObj.AddComponent<SpriteRenderer>();
                sr.sprite = ProceduralSprites.Shot;
                sr.color = new Color(0.2f, 1f, 0.25f, 1f); // Acid Green
                CircleCollider2D col = projObj.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.2f;
                AcidProjectile acidProj = projObj.AddComponent<AcidProjectile>();
                float acidDmg = (data != null ? data.ProjectileDamage : 8f) * enemy.DifficultyMultiplier;
                float acidSpd = data != null ? data.ProjectileSpeed : 6f;
                acidProj.ConfigureAcid(shootDirection, acidDmg, acidSpd);
                return;
            }

            if (projObj == null) return;

            EnemyProjectile projectile = projObj.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                float dmg = (data != null ? data.ProjectileDamage : 10f) * enemy.DifficultyMultiplier;
                float spd = data != null ? data.ProjectileSpeed : 5f;
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
