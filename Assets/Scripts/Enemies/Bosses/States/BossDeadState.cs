using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Boss Dead state.
    /// Halts movement, disables collider, triggers death VFX/SFX,
    /// broadcasts OnBossDied event for upgrade/meta-progression systems,
    /// and informs RoomManager that the boss is defeated.
    /// </summary>
    public class BossDeadState : DeadState
    {
        private readonly BossBase boss;
        private readonly float delay;

        public BossDeadState(BossBase boss, StateMachine stateMachine, float despawnDelay = 1.0f)
            : base(boss, stateMachine, despawnDelay)
        {
            this.boss = boss;
            this.delay = despawnDelay;
        }

        public override void Enter()
        {
            base.Enter();

            boss.StopMovement();

            if (boss.Collider != null)
            {
                boss.Collider.enabled = false;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Die");
            }

            // Spawn Death VFX
            if (boss.Data != null && boss.Data.DeathVFX != null)
            {
                Object.Instantiate(boss.Data.DeathVFX, boss.transform.position, Quaternion.identity);
            }

            // Play Die SFX
            if (boss.Data != null && boss.Data.DieSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(boss.Data.DieSFX);
            }

            boss.NotifyBossDefeated();
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= delay)
            {
                boss.Despawn();
            }
        }
    }
}
