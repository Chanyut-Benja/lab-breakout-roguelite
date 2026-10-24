using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Repositioning state between boss attacks.
    /// Steers towards player to maintain combat pressure, then transitions to PickAttack.
    /// </summary>
    public class BossRepositionState : EnemyState
    {
        private readonly BossBase boss;
        private readonly IState pickAttackState;
        private readonly float minRepositionTime;
        private readonly float maxRepositionTime;
        private float targetDuration;

        public BossRepositionState(BossBase boss, StateMachine stateMachine, IState pickAttackState, float minTime = 1.0f, float maxTime = 2.0f)
            : base(boss, stateMachine)
        {
            this.boss = boss;
            this.pickAttackState = pickAttackState;
            this.minRepositionTime = minTime;
            this.maxRepositionTime = maxTime;
        }

        public override void Enter()
        {
            base.Enter();

            // ในเฟส 3 (Enrage: HP <= 33% หรือเกิน 180s) บอสจะปล่อยท่าใหญ่ต่อเนื่องทันที (Chain Attack)
            if (boss.IsEnraged && boss.TryConsumeEnrageChainAttack())
            {
                targetDuration = 0.08f;
                boss.StopMovement();
                return;
            }

            float min = minRepositionTime;
            float max = maxRepositionTime;

            // บาลานซ์ 8.3: เฟส 2 ท่าโจมตีถี่ขึ้น, เฟส 3 (Enrage) ปล่อยท่าใหญ่ต่อเนื่อง
            if (boss.IsEnraged)
            {
                min *= 0.45f;
                max *= 0.45f;
            }
            else if (boss.CurrentPhase == 2)
            {
                min *= 0.70f;
                max *= 0.70f;
            }

            targetDuration = Random.Range(min, max);

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsMoving", true);
            }
        }

        public override void FixedTick()
        {
            base.FixedTick();

            if (boss.Target == null)
            {
                boss.StopMovement();
                return;
            }

            Vector2 moveDir = boss.CalculateSteeringDirection();
            float speed = boss.Data != null ? boss.Data.MoveSpeed : 2.0f;

            // บาลานซ์ 8.3: เฟส 2 ความเร็วเคลื่อนที่ +20% (1.20x), เฟส 3 (Enrage) ความเร็วเคลื่อนที่ +35% (1.35x)
            speed *= boss.GetPhaseSpeedMultiplier();

            boss.Move(moveDir, speed);
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= targetDuration)
            {
                boss.StopMovement();
                stateMachine.ChangeState(pickAttackState);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsMoving", false);
            }

            boss.StopMovement();
        }
    }
}
