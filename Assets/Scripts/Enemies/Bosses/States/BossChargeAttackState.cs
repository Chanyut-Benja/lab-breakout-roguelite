using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Modular Boss Attack: ChargeAttack.
    /// Extended from Dasher logic:
    /// 1. Telegraph Line (>= 0.6-0.7s) with directional warning indicator
    /// 2. Rapid Dash in fixed direction dealing high damage + knockback
    /// 3. Recover State (~1.5s) offering a clear punishment window for player
    /// </summary>
    public class BossChargeAttackState : EnemyState
    {
        private enum ChargeStep { Telegraph, Dashing, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private ChargeStep currentStep;
        private Vector2 lockedDirection;
        private float stepStartTime;
        private bool hasDealtDamage;

        public BossChargeAttackState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentStep = ChargeStep.Telegraph;
            stepStartTime = Time.time;
            hasDealtDamage = false;

            // Lock direction towards player
            if (boss.Target != null)
            {
                Vector2 diff = (Vector2)(boss.Target.position - boss.transform.position);
                lockedDirection = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.right;
            }
            else
            {
                lockedDirection = boss.SpriteRenderer != null && boss.SpriteRenderer.flipX ? Vector2.left : Vector2.right;
            }

            boss.UpdateFacing(lockedDirection.x);

            // Display directional line telegraph (length based on charge distance)
            float speed = boss.BossConfig != null ? boss.BossConfig.ChargeSpeed : 12f;
            float duration = boss.BossConfig != null ? boss.BossConfig.ChargeDuration : 0.5f;
            float distance = speed * duration;

            if (indicator != null)
            {
                indicator.ShowLine(boss.transform.position, lockedDirection, distance, 0.4f);
            }

            // Play Telegraph SFX
            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Telegraph");
            }
        }

        public void OnPlayerContact(GameObject playerObj)
        {
            if (currentStep == ChargeStep.Dashing && !hasDealtDamage)
            {
                ApplyChargeDamage(playerObj);
            }
        }

        private void ApplyChargeDamage(GameObject playerObj)
        {
            IDamageable damageable = playerObj.GetComponentInChildren<IDamageable>() ?? playerObj.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                float dmg = (boss.BossConfig != null ? boss.BossConfig.ChargeDamage : 30f) * boss.DifficultyMultiplier;
                float kb = boss.BossConfig != null ? boss.BossConfig.ChargeKnockback : 12f;
                damageable.TakeDamage(dmg, lockedDirection * kb);
                hasDealtDamage = true;
                Debug.Log($"[{boss.name}] Charge hit Player for {dmg} damage!");
            }
        }

        public override void FixedTick()
        {
            base.FixedTick();

            if (currentStep == ChargeStep.Dashing)
            {
                float speed = boss.BossConfig != null ? boss.BossConfig.ChargeSpeed : 12f;
                boss.Move(lockedDirection, speed);

                // Proactive collision check with player using broad bounding radius
                if (!hasDealtDamage)
                {
                    float hitRadius = (boss.Collider != null ? boss.Collider.bounds.extents.magnitude : 2.0f) + 0.9f;
                    Collider2D[] hits = Physics2D.OverlapCircleAll(boss.transform.position, hitRadius);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        if (hits[i] != null && hits[i].CompareTag("Player"))
                        {
                            ApplyChargeDamage(hits[i].gameObject);
                            break;
                        }
                    }
                }
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsedInStep = Time.time - stepStartTime;

            switch (currentStep)
            {
                case ChargeStep.Telegraph:
                    float telegraphTime = boss.BossConfig != null ? boss.BossConfig.ChargeTelegraphDuration : 0.7f;
                    if (elapsedInStep >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = ChargeStep.Dashing;
                        stepStartTime = Time.time;

                        if (boss.Animator != null) boss.Animator.SetTrigger("Attack");
                        if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlaySFX(data.AttackSFX);
                        }
                    }
                    break;

                case ChargeStep.Dashing:
                    float dashTime = boss.BossConfig != null ? boss.BossConfig.ChargeDuration : 0.5f;
                    if (elapsedInStep >= dashTime)
                    {
                        boss.StopMovement();

                        currentStep = ChargeStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case ChargeStep.Recover:
                    float recoverTime = (boss.Data != null ? boss.Data.RecoverDuration : 1.5f) * boss.RecoverDurationMultiplier;
                    if (elapsedInStep >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
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
