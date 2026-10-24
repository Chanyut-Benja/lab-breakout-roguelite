using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Reusable Chase State.
    /// Steers directly toward the player while applying flock separation.
    /// Manages contact damage when in proximity to the player.
    /// </summary>
    public class ChaseState : EnemyState
    {
        private float lastContactDamageTime = -999f;

        public ChaseState(EnemyBase enemy, StateMachine stateMachine)
            : base(enemy, stateMachine)
        {
        }

        public override void Enter()
        {
            base.Enter();

            if (enemy.Animator != null)
            {
                enemy.Animator.SetBool("IsMoving", true);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (enemy.Animator != null)
            {
                enemy.Animator.SetBool("IsMoving", false);
            }

            enemy.StopMovement();
        }

        public override void FixedTick()
        {
            base.FixedTick();

            if (enemy.Target == null)
            {
                enemy.StopMovement();
                return;
            }

            Vector2 steerDirection = enemy.CalculateSteeringDirection();
            float speed = data != null ? data.MoveSpeed : 1.5f;

            enemy.Move(steerDirection, speed);

            // Proximity check for contact damage
            CheckContactDamageProximity();
        }

        private void CheckContactDamageProximity()
        {
            if (enemy.Target == null || data == null) return;

            float dist = Vector2.Distance(enemy.transform.position, enemy.Target.position);
            if (dist <= data.AttackRange)
            {
                TryDealContactDamage(enemy.Target.gameObject);
            }
        }

        public void TryDealContactDamage(GameObject targetObj)
        {
            if (data == null || targetObj == null) return;

            float cooldown = data.ContactDamageCooldown;
            if (Time.time < lastContactDamageTime + cooldown)
            {
                return;
            }

            IDamageable damageable = targetObj.GetComponentInChildren<IDamageable>();
            if (damageable != null)
            {
                float damageAmount = data.ContactDamage * enemy.DifficultyMultiplier;
                damageable.TakeDamage(damageAmount);
                lastContactDamageTime = Time.time;

                // Log metric for test tracking
                Debug.Log($"[{enemy.name}] Dealt {damageAmount} contact damage to Player.");
            }
        }
    }
}
