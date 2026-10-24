using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Executes emergence from underground.
    /// Restores vulnerability, deals AoE damage to any player in radius, and triggers ExposedState.
    /// </summary>
    public class EmergeState : EnemyState
    {
        private readonly IState nextState;
        private readonly float animationDelay;
        private static readonly Collider2D[] hitBuffer = new Collider2D[8];

        public EmergeState(EnemyBase enemy, StateMachine stateMachine, IState nextState, float animationDelay = 0.2f)
            : base(enemy, stateMachine)
        {
            this.nextState = nextState;
            this.animationDelay = animationDelay;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            // Restore vulnerability & normal appearance
            if (enemy.Health != null)
            {
                enemy.Health.IsInvulnerable = false;
            }

            if (enemy.SpriteRenderer != null)
            {
                enemy.SpriteRenderer.color = Color.white;
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Attack");
            }

            // Play Attack/Emerge SFX
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }

            // Execute AoE Damage (radius ~1.2, damage 10 per spec)
            ExecuteAoEDamage();
        }

        private void ExecuteAoEDamage()
        {
            float radius = data != null ? data.EmergeAoERadius : 1.2f;
            float damage = (data != null ? data.EmergeDamage : 10f) * enemy.DifficultyMultiplier;
            bool hitPlayer = false;

            // 1. Direct Target Distance Check (guaranteed hit against assigned Player target)
            if (enemy.Target != null)
            {
                float dist = Vector2.Distance(enemy.transform.position, enemy.Target.position);
                if (dist <= radius)
                {
                    IDamageable damageable = enemy.Target.GetComponentInChildren<IDamageable>();
                    if (damageable != null)
                    {
                        Vector2 knockback = ((Vector2)enemy.Target.position - (Vector2)enemy.transform.position);
                        Vector2 knockbackDir = knockback.sqrMagnitude > 0.001f ? knockback.normalized : Vector2.up;
                        damageable.TakeDamage(damage, knockbackDir * 6f);
                        hitPlayer = true;
                        Debug.Log($"[{enemy.name}] Emerge dealt {damage} AoE damage directly to Player!");
                    }
                }
            }

            // 2. OverlapCircleAll query for all colliders in radius (hits non-triggers and triggers alike)
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(enemy.transform.position, radius);
            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider2D col = hitColliders[i];
                if (col == null || col.gameObject == enemy.gameObject) continue;

                if (col.CompareTag("Player") && !hitPlayer)
                {
                    IDamageable damageable = col.GetComponentInChildren<IDamageable>();
                    if (damageable != null)
                    {
                        Vector2 knockback = ((Vector2)col.transform.position - (Vector2)enemy.transform.position);
                        Vector2 knockbackDir = knockback.sqrMagnitude > 0.001f ? knockback.normalized : Vector2.up;
                        damageable.TakeDamage(damage, knockbackDir * 6f);
                        hitPlayer = true;
                        Debug.Log($"[{enemy.name}] Emerge dealt {damage} AoE damage to Player via OverlapCircleAll!");
                    }
                }
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= animationDelay)
            {
                stateMachine.ChangeState(nextState);
            }
        }
    }
}
