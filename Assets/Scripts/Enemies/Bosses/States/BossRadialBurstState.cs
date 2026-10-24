using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Modular Boss Attack: RadialBurst / FanShot.
    /// Extended from Ranged logic:
    /// 1. Telegraph Arc / Aim (0.7s)
    /// 2. Fires multi-projectile fan or 360 ring of EnemyProjectiles
    /// 3. Recover pause (1.0s)
    /// </summary>
    public class BossRadialBurstState : EnemyState
    {
        private enum BurstStep { Telegraph, Firing, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private BurstStep currentStep;
        private float stepStartTime;
        private Vector2 aimDirection;

        public BossRadialBurstState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentStep = BurstStep.Telegraph;
            stepStartTime = Time.time;

            if (boss.Target != null)
            {
                Vector2 diff = (Vector2)(boss.Target.position - boss.transform.position);
                aimDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.right;
            }
            else
            {
                aimDirection = boss.SpriteRenderer != null && boss.SpriteRenderer.flipX ? Vector2.left : Vector2.right;
            }

            boss.UpdateFacing(aimDirection.x);

            // Display telegraph line or circle
            if (indicator != null)
            {
                bool is360 = boss.IsFullRadial360;
                if (is360)
                {
                    indicator.ShowCircle(boss.transform.position, 3f, 32, 0.15f);
                }
                else
                {
                    indicator.ShowLine(boss.transform.position, aimDirection, 6f, 0.2f);
                }
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
                case BurstStep.Telegraph:
                    float telegraphTime = boss.BossConfig != null ? boss.BossConfig.RadialTelegraphDuration : 0.7f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = BurstStep.Firing;
                        stepStartTime = Time.time;
                        FireProjectiles();
                    }
                    break;

                case BurstStep.Firing:
                    if (elapsed >= 0.15f)
                    {
                        currentStep = BurstStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case BurstStep.Recover:
                    float recoverTime = 1.0f * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void FireProjectiles()
        {
            GameObject projPrefab = boss.Data != null ? boss.Data.ProjectilePrefab : null;
            if (projPrefab == null) return;

            int count = boss.BossConfig != null ? boss.BossConfig.ProjectileCount : 8;
            bool is360 = boss.IsFullRadial360;
            if (is360 && count < 12)
            {
                count = 12; // ensure full circle density
            }
            float spreadAngle = boss.BossConfig != null ? boss.BossConfig.FanSpreadAngle : 90f;
            float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            float speed = boss.Data != null ? boss.Data.ProjectileSpeed : 5f;
            float damage = (boss.Data != null ? boss.Data.ProjectileDamage : 10f) * boss.DifficultyMultiplier;

            if (boss.Animator != null) boss.Animator.SetTrigger("Attack");
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }

            if (is360)
            {
                float step = 360f / Mathf.Max(1, count);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * step;
                    Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    SpawnSingleProjectile(projPrefab, dir, damage, speed);
                }
            }
            else
            {
                float startAngle = baseAngle - (spreadAngle * 0.5f);
                float step = count > 1 ? spreadAngle / (count - 1) : 0f;

                for (int i = 0; i < count; i++)
                {
                    float angle = startAngle + (i * step);
                    Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    SpawnSingleProjectile(projPrefab, dir, damage, speed);
                }
            }
        }

        private void SpawnSingleProjectile(GameObject prefab, Vector2 direction, float damage, float speed)
        {
            Vector3 spawnPos = boss.transform.position + (Vector3)(direction * 0.6f);
            GameObject obj = Object.Instantiate(prefab, spawnPos, Quaternion.identity);

            EnemyProjectile proj = obj.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                proj.Setup(direction, damage, speed);
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
