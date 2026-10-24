using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Boss Intro state.
    /// Temporarily invulnerable, broadcasts OnBossSpawned event for HUD/Boss HP bar,
    /// plays entrance roar/SFX, then begins attack loop.
    /// </summary>
    public class BossIntroState : EnemyState
    {
        private readonly BossBase boss;
        private readonly IState nextState;
        private readonly float duration;

        public BossIntroState(BossBase boss, StateMachine stateMachine, IState nextState, float duration = 1.2f)
            : base(boss, stateMachine)
        {
            this.boss = boss;
            this.nextState = nextState;
            this.duration = duration;
        }

        public override void Enter()
        {
            base.Enter();

            boss.StopMovement();

            if (boss.Health != null)
            {
                boss.Health.IsInvulnerable = true;
            }

            // Play Roar SFX
            if (boss.BossConfig != null && boss.BossConfig.RoarSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(boss.BossConfig.RoarSFX);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Roar");
            }

            boss.NotifyBossSpawned();
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= duration)
            {
                stateMachine.ChangeState(nextState);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (boss.Health != null)
            {
                boss.Health.IsInvulnerable = false;
            }
        }
    }
}
