using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Modular Boss Attack: SummonMinions.
    /// 1. Telegraph Chant / Glow (0.8s)
    /// 2. Spawns capped number of minions (e.g. 4 Walkers for Brute) in an orbit ring around boss
    /// 3. Recover window (1.0s)
    /// </summary>
    public class BossSummonMinionsState : EnemyState
    {
        private enum SummonStep { Telegraph, Spawning, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private SummonStep currentStep;
        private float stepStartTime;

        public BossSummonMinionsState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentStep = SummonStep.Telegraph;
            stepStartTime = Time.time;

            if (indicator != null)
            {
                indicator.ShowCircle(boss.transform.position, 2.0f, 24, 0.25f);
            }

            if (boss.BossConfig != null && boss.BossConfig.SummonSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(boss.BossConfig.SummonSFX);
            }
            else if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
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
                case SummonStep.Telegraph:
                    float telegraphTime = boss.BossConfig != null ? boss.BossConfig.SummonTelegraphDuration : 0.8f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = SummonStep.Spawning;
                        stepStartTime = Time.time;
                        ExecuteSummon();
                    }
                    break;

                case SummonStep.Spawning:
                    if (elapsed >= 0.2f)
                    {
                        currentStep = SummonStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case SummonStep.Recover:
                    float recoverTime = 1.0f * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void ExecuteSummon()
        {
            GameObject minionPrefab = boss.BossConfig != null ? boss.BossConfig.MinionPrefab : null;
            if (minionPrefab == null)
            {
                // Fallback to Walker prefab from Resources or data
                minionPrefab = boss.Data != null ? boss.Data.Prefab : null;
            }

            if (minionPrefab == null) return;

            int spawnCount = boss.BossConfig != null ? boss.BossConfig.MinionSpawnCount : 4;
            int cap = boss.BossConfig != null ? boss.BossConfig.MaxActiveMinionsCap : 6;

            int activeCount = boss.ActiveMinionCount;
            int canSpawn = Mathf.Max(0, cap - activeCount);
            int countToSpawn = Mathf.Min(spawnCount, canSpawn);

            float radius = 2.2f;
            float angleStep = 360f / Mathf.Max(1, countToSpawn);

            for (int i = 0; i < countToSpawn; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Vector3 spawnPos = boss.transform.position + offset;

                GameObject minionObj = Object.Instantiate(minionPrefab, spawnPos, Quaternion.identity);
                EnemyBase minionEnemy = minionObj.GetComponent<EnemyBase>();

                if (minionEnemy != null)
                {
                    if (boss.BossConfig != null && boss.BossConfig.MinionData != null)
                    {
                        minionEnemy.SetData(boss.BossConfig.MinionData);
                    }

                    minionEnemy.Init(boss.Target, boss.DifficultyMultiplier);
                    boss.RegisterMinion(minionEnemy);
                }
            }

            Debug.Log($"[{boss.name}] Summoned {countToSpawn} minions (Active Cap: {boss.ActiveMinionCount}/{cap})");
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            boss.StopMovement();
        }
    }
}
