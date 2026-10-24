using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Overlord Signature Attack: RapidBarrage (ยิงบอลรัวๆ / Gatling Barrage).
    /// Fires a continuous rapid stream of energy projectiles tracking towards the player.
    /// Phase 1/2: 10 rapid shots.
    /// Phase 3 (Enrage): 16 rapid shots with faster projectile speed.
    /// </summary>
    public class BossRapidBarrageState : EnemyState
    {
        private enum BarrageStep { Telegraph, Firing, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private BarrageStep currentStep;
        private float stepStartTime;
        private Vector2 aimDirection;

        // Firing sequence state
        private int totalShotsToFire;
        private int shotsFired;
        private float nextShotTime;
        private const float ShotInterval = 0.08f;

        public BossRapidBarrageState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentStep = BarrageStep.Telegraph;
            stepStartTime = Time.time;
            shotsFired = 0;

            totalShotsToFire = boss.CurrentPhase >= 3 ? 16 : 10;

            UpdateAimDirection();
            boss.UpdateFacing(aimDirection.x);

            // Display laser line telegraph
            if (indicator != null)
            {
                indicator.ShowLine(boss.transform.position, aimDirection, 10f, 0.25f);
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

        private void UpdateAimDirection()
        {
            if (boss.Target != null)
            {
                Vector2 diff = (Vector2)(boss.Target.position - boss.transform.position);
                if (diff.sqrMagnitude > 0.001f)
                {
                    aimDirection = diff.normalized;
                }
            }
            else
            {
                aimDirection = boss.SpriteRenderer != null && boss.SpriteRenderer.flipX ? Vector2.left : Vector2.right;
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case BarrageStep.Telegraph:
                    float telegraphTime = 0.6f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = BarrageStep.Firing;
                        stepStartTime = Time.time;
                        nextShotTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetTrigger("Attack");
                        }
                    }
                    break;

                case BarrageStep.Firing:
                    if (shotsFired < totalShotsToFire)
                    {
                        if (Time.time >= nextShotTime)
                        {
                            FireSingleBarrageShot();
                            shotsFired++;
                            nextShotTime = Time.time + ShotInterval;
                        }
                    }
                    else
                    {
                        currentStep = BarrageStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case BarrageStep.Recover:
                    float recoverTime = 1.0f * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void FireSingleBarrageShot()
        {
            GameObject projPrefab = boss.Data != null ? boss.Data.ProjectilePrefab : null;
            if (projPrefab == null) return;

            // Smoothly track towards target with slight spread
            UpdateAimDirection();
            float spreadAngle = Random.Range(-6f, 6f);
            float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float finalAngle = (baseAngle + spreadAngle) * Mathf.Deg2Rad;
            Vector2 fireDir = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));

            float speed = (boss.Data != null ? boss.Data.ProjectileSpeed : 6.5f) * (boss.CurrentPhase >= 3 ? 1.25f : 1.1f);
            float damage = (boss.Data != null ? boss.Data.ProjectileDamage : 12f) * boss.DifficultyMultiplier * 0.85f;

            Vector3 spawnPos = boss.transform.position + (Vector3)(fireDir * 0.7f);
            GameObject obj = Object.Instantiate(projPrefab, spawnPos, Quaternion.identity);

            EnemyProjectile proj = obj.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                proj.Setup(fireDir, damage, speed);
            }

            if (data != null && data.AttackSFX != null && AudioManager.Instance != null && shotsFired % 3 == 0)
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
