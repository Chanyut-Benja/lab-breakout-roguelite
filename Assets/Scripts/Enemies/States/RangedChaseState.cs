using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Ranged Chase and Kite state.
    /// Maintains preferred distance (~6m), flees/kites if player gets too close (<3m),
    /// and triggers Aim/Shoot when in range and cooldown is ready.
    /// </summary>
    public class RangedChaseState : EnemyState
    {
        private readonly IState aimState;
        private float lastShotTime = -999f;

        public RangedChaseState(EnemyBase enemy, StateMachine stateMachine, IState aimState)
            : base(enemy, stateMachine)
        {
            this.aimState = aimState;
        }

        public void NotifyShotFired()
        {
            lastShotTime = Time.time;
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

            float dist = Vector2.Distance(enemy.transform.position, enemy.Target.position);
            float kiteDist = data != null ? data.KiteDistance : 3.0f;
            float prefDist = data != null ? data.PreferredDistance : 6.0f;
            float moveSpeed = data != null ? data.MoveSpeed : 1.5f;

            if (dist < kiteDist)
            {
                // Flee / Kite backward away from player
                Vector2 fleeDir = ((Vector2)enemy.transform.position - (Vector2)enemy.Target.position).normalized;
                enemy.Move(fleeDir, moveSpeed * 1.1f);
            }
            else if (dist > prefDist)
            {
                // Advance toward player
                Vector2 advanceDir = enemy.CalculateSteeringDirection();
                enemy.Move(advanceDir, moveSpeed);
            }
            else
            {
                // Within ideal firing sweet spot
                enemy.StopMovement();

                // Check shoot cooldown (2.0s per spec)
                float cooldown = data != null ? data.AttackCooldown : 2.0f;
                if (Time.time >= lastShotTime + cooldown)
                {
                    stateMachine.ChangeState(aimState);
                }
            }
        }
    }
}
