using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// The Stormcaller Signature Attack: SpiralBulletHell (ม่านกระสุนหมุนวน / Spiral Nova).
    /// Fires rotating multi-arm spiral streams of energy projectiles outward in 360 degrees.
    /// Creates dense bullet hell patterns that test player positioning and dodging.
    /// </summary>
    public class BossSpiralBulletHellState : EnemyState
    {
        private enum SpiralStep { Telegraph, Channelling, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private SpiralStep currentStep;
        private float stepStartTime;
        private float nextWaveTime;
        private float currentAngle;

        private const float ChannelDuration = 2.2f;
        private const float WaveInterval = 0.09f;
        private const float RotationSpeed = 190f; // Degrees per second

        public BossSpiralBulletHellState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentStep = SpiralStep.Telegraph;
            stepStartTime = Time.time;
            currentAngle = 0f;

            if (indicator != null)
            {
                indicator.ShowCircle(boss.transform.position, 3.5f, 32, 0.25f);
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
                case SpiralStep.Telegraph:
                    float telegraphTime = 0.6f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = SpiralStep.Channelling;
                        stepStartTime = Time.time;
                        nextWaveTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetTrigger("Attack");
                        }
                    }
                    break;

                case SpiralStep.Channelling:
                    // Rotate the firing angle
                    currentAngle += RotationSpeed * Time.deltaTime;

                    // Fire spiral wave periodically
                    if (Time.time >= nextWaveTime)
                    {
                        FireSpiralArms();
                        nextWaveTime = Time.time + WaveInterval;
                    }

                    if (elapsed >= ChannelDuration)
                    {
                        currentStep = SpiralStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case SpiralStep.Recover:
                    float recoverTime = 1.0f * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void FireSpiralArms()
        {
            GameObject projPrefab = boss.Data != null ? boss.Data.ProjectilePrefab : null;
            if (projPrefab == null) return;

            int armCount = boss.CurrentPhase >= 2 ? 6 : 4;
            float angleStep = 360f / armCount;

            float speed = (boss.Data != null ? boss.Data.ProjectileSpeed : 5.5f) * (boss.CurrentPhase >= 2 ? 1.15f : 1.0f);
            float damage = (boss.Data != null ? boss.Data.ProjectileDamage : 8f) * boss.DifficultyMultiplier * 0.75f;

            for (int i = 0; i < armCount; i++)
            {
                float angle = (currentAngle + (i * angleStep)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                Vector3 spawnPos = boss.transform.position + (Vector3)(dir * 0.6f);
                GameObject obj = Object.Instantiate(projPrefab, spawnPos, Quaternion.identity);

                EnemyProjectile proj = obj.GetComponent<EnemyProjectile>();
                if (proj != null)
                {
                    proj.Setup(dir, damage, speed);
                }
            }

            if (data != null && data.AttackSFX != null && AudioManager.Instance != null && Random.value < 0.25f)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
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
