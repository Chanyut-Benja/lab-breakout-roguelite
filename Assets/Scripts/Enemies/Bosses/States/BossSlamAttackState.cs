using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Modular Boss Attack: SlamAttack.
    /// 1. Telegraph Circle around boss (0.8s)
    /// 2. Heavy Ground Slam dealing large AoE damage and heavy radial knockback
    /// 3. Recover Window (1.5s) opening a punish opportunity for player
    /// </summary>
    public class BossSlamAttackState : EnemyState
    {
        private enum SlamStep { Telegraph, Impact, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private SlamStep currentStep;
        private float stepStartTime;

        public BossSlamAttackState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
            : base(boss, stateMachine)
        {
            this.boss = boss;
            this.indicator = indicator;
            this.nextState = nextState;
        }

        public override void Enter()
        {
            base.Enter();

            boss.StopMovement();
            currentStep = SlamStep.Telegraph;
            stepStartTime = Time.time;

            float radius = boss.BossConfig != null ? boss.BossConfig.SlamRadius : 3.2f;

            if (indicator != null)
            {
                indicator.ShowCircle(boss.transform.position, radius, 36, 0.35f);
            }

            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Telegraph");
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case SlamStep.Telegraph:
                    float telegraphTime = boss.BossConfig != null ? boss.BossConfig.SlamTelegraphDuration : 0.8f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = SlamStep.Impact;
                        stepStartTime = Time.time;
                        ExecuteSlam();
                    }
                    break;

                case SlamStep.Impact:
                    // Brief impact pause (0.15s) before entering recovery
                    if (elapsed >= 0.15f)
                    {
                        currentStep = SlamStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case SlamStep.Recover:
                    float recoverTime = (boss.Data != null ? boss.Data.RecoverDuration : 1.5f) * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void ExecuteSlam()
        {
            float radius = boss.BossConfig != null ? boss.BossConfig.SlamRadius : 3.2f;
            float damage = (boss.BossConfig != null ? boss.BossConfig.SlamDamage : 25f) * boss.DifficultyMultiplier;
            float knockback = boss.BossConfig != null ? boss.BossConfig.SlamKnockback : 10f;

            // Spawn VFX
            if (boss.BossConfig != null && boss.BossConfig.SlamShockwaveVFX != null)
            {
                Object.Instantiate(boss.BossConfig.SlamShockwaveVFX, boss.transform.position, Quaternion.identity);
            }

            // Play Slam SFX
            if (boss.BossConfig != null && boss.BossConfig.SlamSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(boss.BossConfig.SlamSFX);
            }
            else if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Attack");
            }

            // 1. Direct Target check
            if (boss.Target != null)
            {
                float dist = Vector2.Distance(boss.transform.position, boss.Target.position);
                if (dist <= radius)
                {
                    IDamageable damageable = boss.Target.GetComponentInChildren<IDamageable>();
                    if (damageable != null)
                    {
                        Vector2 kbDir = ((Vector2)boss.Target.position - (Vector2)boss.transform.position).normalized;
                        if (kbDir == Vector2.zero) kbDir = Vector2.up;
                        damageable.TakeDamage(damage, kbDir * knockback);
                        Debug.Log($"[{boss.name}] Slam hit Player for {damage} damage!");
                    }
                }
            }

            // 2. OverlapCircleAll query
            Collider2D[] colliders = Physics2D.OverlapCircleAll(boss.transform.position, radius);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || col.gameObject == boss.gameObject) continue;

                if (col.CompareTag("Player") && col.transform != boss.Target)
                {
                    IDamageable damageable = col.GetComponentInChildren<IDamageable>();
                    if (damageable != null)
                    {
                        Vector2 kbDir = ((Vector2)col.transform.position - (Vector2)boss.transform.position).normalized;
                        damageable.TakeDamage(damage, kbDir * knockback);
                    }
                }
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            boss.StopMovement();
        }
    }
}
