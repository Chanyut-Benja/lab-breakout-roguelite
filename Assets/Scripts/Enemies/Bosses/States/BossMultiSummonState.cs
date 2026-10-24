using System.Collections.Generic;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Overlord Signature Attack: MultiSummon (เสกลูกน้องหลายชนิดพร้อมกัน).
    /// Summons a diversified legion consisting of Walkers, Runners, Dashers, and Ranged enemies simultaneously.
    /// Spawns them in a wide ritual circle around the Overlord and respects active minion caps.
    /// </summary>
    public class BossMultiSummonState : EnemyState
    {
        public struct MinionSquadEntry
        {
            public GameObject Prefab;
            public EnemyData Data;
            public int Count;

            public MinionSquadEntry(GameObject prefab, EnemyData data, int count)
            {
                Prefab = prefab;
                Data = data;
                Count = count;
            }
        }

        private enum SummonStep { Telegraph, Spawning, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;
        private readonly List<MinionSquadEntry> squadEntries;
        private readonly int maxActiveCap;

        private SummonStep currentStep;
        private float stepStartTime;

        public BossMultiSummonState(
            BossBase boss,
            StateMachine stateMachine,
            TelegraphIndicator indicator,
            IState nextState,
            List<MinionSquadEntry> squadEntries,
            int maxActiveCap = 8)
            : base(boss, stateMachine)
        {
            this.boss = boss;
            this.indicator = indicator;
            this.nextState = nextState;
            this.squadEntries = squadEntries;
            this.maxActiveCap = maxActiveCap;
        }

        public override void Enter()
        {
            base.Enter();

            boss.StopMovement();
            currentStep = SummonStep.Telegraph;
            stepStartTime = Time.time;

            if (indicator != null)
            {
                indicator.ShowCircle(boss.transform.position, 3.2f, 36, 0.35f);
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
                        ExecuteMultiSummon();
                    }
                    break;

                case SummonStep.Spawning:
                    if (elapsed >= 0.25f)
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

        private void ExecuteMultiSummon()
        {
            if (squadEntries == null || squadEntries.Count == 0) return;

            int activeCount = boss.ActiveMinionCount;
            int remainingCap = Mathf.Max(0, maxActiveCap - activeCount);
            if (remainingCap <= 0)
            {
                Debug.Log($"[{boss.name}] Minion cap reached ({activeCount}/{maxActiveCap}), skipping summon.");
                return;
            }

            // Calculate total squad count requested
            int totalRequested = 0;
            for (int i = 0; i < squadEntries.Count; i++)
            {
                totalRequested += squadEntries[i].Count;
            }

            int spawnLimit = Mathf.Min(totalRequested, remainingCap);
            float radius = 3.2f;
            float angleStep = 360f / Mathf.Max(1, spawnLimit);
            int currentSpawnIndex = 0;

            for (int entryIdx = 0; entryIdx < squadEntries.Count; entryIdx++)
            {
                var entry = squadEntries[entryIdx];
                if (entry.Prefab == null) continue;

                for (int countIdx = 0; countIdx < entry.Count; countIdx++)
                {
                    if (currentSpawnIndex >= spawnLimit) break;

                    float angle = currentSpawnIndex * angleStep * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                    Vector3 spawnPos = boss.transform.position + offset;

                    GameObject obj = Object.Instantiate(entry.Prefab, spawnPos, Quaternion.identity);
                    EnemyBase minionEnemy = obj.GetComponent<EnemyBase>();

                    if (minionEnemy != null)
                    {
                        if (entry.Data != null) minionEnemy.SetData(entry.Data);
                        minionEnemy.Init(boss.Target, boss.DifficultyMultiplier);
                        boss.RegisterMinion(minionEnemy);
                    }

                    currentSpawnIndex++;
                }

                if (currentSpawnIndex >= spawnLimit) break;
            }

            Debug.Log($"[{boss.name}] Summoned {currentSpawnIndex} multi-type minions! (Active Cap: {boss.ActiveMinionCount}/{maxActiveCap})");
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            boss.StopMovement();
        }
    }
}
