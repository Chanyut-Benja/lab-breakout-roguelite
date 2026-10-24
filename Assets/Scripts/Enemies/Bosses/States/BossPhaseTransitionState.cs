using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Phase Transition state triggered when boss HP reaches phase threshold (e.g. 50%).
    /// Per spec:
    /// - Brief invulnerability
    /// - Transition SFX & VFX (roar / enrage)
    /// - Screen wipe: Clears all enemy projectiles on screen ("ล้างกระสุน")
    /// - Advances boss phase and resumes combat with updated attack modules
    /// </summary>
    public class BossPhaseTransitionState : EnemyState
    {
        private readonly BossBase boss;
        private readonly IState nextState;
        private readonly float duration;

        public BossPhaseTransitionState(BossBase boss, StateMachine stateMachine, IState nextState, float duration = 1.5f)
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

            // Advance Phase
            boss.AdvancePhase();

            // Screen projectile wipe ("ล้างกระสุน")
            ClearAllProjectiles();

            // Spawn Phase VFX
            if (boss.BossConfig != null && boss.BossConfig.PhaseTransitionVFX != null)
            {
                Object.Instantiate(boss.BossConfig.PhaseTransitionVFX, boss.transform.position, Quaternion.identity);
            }

            // Play Transition SFX
            if (boss.BossConfig != null && boss.BossConfig.PhaseTransitionSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(boss.BossConfig.PhaseTransitionSFX);
            }
            else if (boss.BossConfig != null && boss.BossConfig.RoarSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(boss.BossConfig.RoarSFX);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Enrage");
            }

            Debug.Log($"[{boss.name}] Phase Transition triggered! Now in Phase {boss.CurrentPhase}");
        }

        private void ClearAllProjectiles()
        {
            EnemyProjectile[] projs = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projs.Length; i++)
            {
                if (projs[i] != null)
                {
                    Object.Destroy(projs[i].gameObject);
                }
            }
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
