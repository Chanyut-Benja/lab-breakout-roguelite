using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Modular Boss Attack: BurrowEmerge.
    /// Extended from Burrower logic:
    /// 1. Tunnels underground (invulnerable) towards player
    /// 2. Telegraph Ground Circle (0.8s) under player
    /// 3. Emerge dealing explosive AoE damage + knockback
    /// 4. Recover window (1.5s)
    /// </summary>
    public class BossBurrowEmergeState : EnemyState
    {
        private enum BurrowStep { Tunneling, Telegraph, Emerge, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private BurrowStep currentStep;
        private float stepStartTime;
        private Vector3 targetEmergePos;
        private int currentEmergeCount;

        /// <summary>
        /// Optional delegate to dynamically determine how many consecutive emerges to execute (e.g. 2 for Digger Phase 2).
        /// </summary>
        public System.Func<int> EmergeCountGetter { get; set; }

        public BossBurrowEmergeState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentEmergeCount = 0;
            currentStep = BurrowStep.Tunneling;
            stepStartTime = Time.time;

            if (boss.Health != null) boss.Health.IsInvulnerable = true;
            if (boss.Collider != null) boss.Collider.isTrigger = true;
            if (boss.SpriteRenderer != null) boss.SpriteRenderer.color = new Color(0.4f, 0.3f, 0.2f, 0.4f);

            if (boss.Animator != null) boss.Animator.SetTrigger("Burrow");
        }

        public override void FixedTick()
        {
            base.FixedTick();

            if (currentStep == BurrowStep.Tunneling)
            {
                if (boss.Target == null) return;

                Vector2 toTarget = (Vector2)(boss.Target.position - boss.transform.position);
                float dist = toTarget.magnitude;

                float speed = boss.Data != null ? boss.Data.UndergroundSpeed : 5.0f;
                boss.Move(toTarget.normalized, speed);

                float maxDuration = boss.BossConfig != null ? boss.BossConfig.BossBurrowDuration : 2.5f;
                if (dist <= 1.0f || (Time.time - stepStartTime) >= maxDuration)
                {
                    boss.StopMovement();

                    targetEmergePos = boss.Target != null ? boss.Target.position : boss.transform.position;
                    boss.transform.position = targetEmergePos;
                    if (boss.Rigidbody != null) boss.Rigidbody.position = targetEmergePos;

                    currentStep = BurrowStep.Telegraph;
                    stepStartTime = Time.time;

                    float radius = Mathf.Max(3.8f, boss.BossConfig != null ? boss.BossConfig.BossEmergeRadius : 3.8f);
                    if (indicator != null) indicator.ShowCircle(targetEmergePos, radius, 36, 0.3f);

                    if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(data.TelegraphSFX);
                    }
                }
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case BurrowStep.Telegraph:
                    float telegraphTime = boss.BossConfig != null ? boss.BossConfig.BossEmergeTelegraphDuration : 0.8f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = BurrowStep.Emerge;
                        stepStartTime = Time.time;
                        ExecuteEmerge();
                    }
                    break;

                case BurrowStep.Emerge:
                    if (elapsed >= 0.2f)
                    {
                        currentEmergeCount++;
                        int maxEmerges = EmergeCountGetter != null ? EmergeCountGetter() : 1;
                        if (currentEmergeCount < maxEmerges)
                        {
                            // Re-burrow immediately towards player for consecutive emerge
                            currentStep = BurrowStep.Tunneling;
                            stepStartTime = Time.time;

                            if (boss.Health != null) boss.Health.IsInvulnerable = true;
                            if (boss.Collider != null) boss.Collider.isTrigger = true;
                            if (boss.SpriteRenderer != null) boss.SpriteRenderer.color = new Color(0.4f, 0.3f, 0.2f, 0.4f);
                            if (boss.Animator != null) boss.Animator.SetTrigger("Burrow");
                        }
                        else
                        {
                            currentStep = BurrowStep.Recover;
                            stepStartTime = Time.time;

                            if (boss.Animator != null)
                            {
                                boss.Animator.SetBool("IsMoving", false);
                                boss.Animator.SetTrigger("Recover");
                            }
                        }
                    }
                    break;

                case BurrowStep.Recover:
                    float recoverTime = (boss.Data != null ? boss.Data.RecoverDuration : 1.5f) * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void ExecuteEmerge()
        {
            if (boss.Health != null) boss.Health.IsInvulnerable = false;
            if (boss.Collider != null) boss.Collider.isTrigger = false;
            if (boss.SpriteRenderer != null) boss.SpriteRenderer.color = Color.white;

            if (boss.Animator != null) boss.Animator.SetTrigger("Attack");
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }

            float radius = Mathf.Max(3.8f, boss.BossConfig != null ? boss.BossConfig.BossEmergeRadius : 3.8f);
            float damage = (boss.BossConfig != null ? boss.BossConfig.BossEmergeDamage : 28f) * boss.DifficultyMultiplier;

            bool hitPlayer = false;

            // 1. Direct Target check (evaluating distance to both boss center and emerge indicator center)
            if (boss.Target != null)
            {
                float distBoss = Vector2.Distance(boss.transform.position, boss.Target.position);
                float distTelegraph = Vector2.Distance(targetEmergePos, boss.Target.position);
                if (distBoss <= radius || distTelegraph <= radius)
                {
                    IDamageable dmg = boss.Target.GetComponentInChildren<IDamageable>() ?? boss.Target.GetComponentInParent<IDamageable>();
                    if (dmg != null)
                    {
                        Vector2 kb = ((Vector2)boss.Target.position - (Vector2)boss.transform.position).normalized;
                        if (kb == Vector2.zero) kb = Vector2.up;
                        dmg.TakeDamage(damage, kb * 10f);
                        hitPlayer = true;
                        Debug.Log($"[{boss.name}] Emerge hit Target for {damage} damage!");
                    }
                }
            }

            // 2. Overlap query to catch any player colliders
            if (!hitPlayer)
            {
                Collider2D[] cols = Physics2D.OverlapCircleAll(boss.transform.position, radius);
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider2D c = cols[i];
                    if (c == null || c.gameObject == boss.gameObject) continue;
                    if (c.CompareTag("Player"))
                    {
                        IDamageable dmg = c.GetComponent<IDamageable>() ?? c.GetComponentInParent<IDamageable>() ?? c.GetComponentInChildren<IDamageable>();
                        if (dmg != null)
                        {
                            dmg.TakeDamage(damage, Vector2.up * 10f);
                            hitPlayer = true;
                            Debug.Log($"[{boss.name}] Emerge Overlap hit Player for {damage} damage!");
                            break;
                        }
                    }
                }
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            if (boss.Health != null) boss.Health.IsInvulnerable = false;
            if (boss.Collider != null) boss.Collider.isTrigger = false;
            if (boss.SpriteRenderer != null) boss.SpriteRenderer.color = Color.white;
            boss.StopMovement();
        }
    }
}
