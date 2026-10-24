using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Overlord Boss (Room 18 Final Encounter - Base Max HP: 160,000).
    /// Supreme 3-phase final boss with comprehensive moveset:
    /// - Phase 1 (100% - 66%): ChargeAttack + SlamAttack + FanShot + RapidBarrage (ยิงบอลรัวๆ).
    /// - Phase 2 (66% - 33%): Move Speed +20%, adds BurrowEmerge + MultiSummon (เสกลูกน้องหลายชนิดพร้อมกัน).
    /// - Phase 3 (< 33% or 180s - Enrage!): Move Speed +35%, RadialBurst becomes full 360 circle burst,
    ///   RapidBarrage fires 16 rapid fireballs, and executes 2-Attack Combos (chains two non-repeating attacks back-to-back).
    /// </summary>
    public class OverlordBoss : BossBase
    {
        public override float DefaultBaseMaxHP => 160000f;

        [Header("Multi-Minion Squad")]
        [SerializeField] private GameObject walkerPrefab;
        [SerializeField] private EnemyData walkerData;
        [SerializeField] private GameObject runnerPrefab;
        [SerializeField] private EnemyData runnerData;
        [SerializeField] private GameObject dasherPrefab;
        [SerializeField] private EnemyData dasherData;
        [SerializeField] private GameObject rangedPrefab;
        [SerializeField] private EnemyData rangedData;

        private TelegraphIndicator telegraphIndicator;

        // Attack Modules
        private BossChargeAttackState chargeAttack;
        private BossSlamAttackState slamAttack;
        private BossRadialBurstState radialBurstAttack;
        private BossBurrowEmergeState burrowEmergeAttack;
        private BossRapidBarrageState rapidBarrageAttack;
        private BossMultiSummonState multiSummonAttack;

        // Attack Pools
        private readonly List<IState> phase1Attacks = new List<IState>();
        private readonly List<IState> phase2Attacks = new List<IState>();

        // Combo Router State for Phase 3 2-Attack Combos
        private OverlordComboRouterState comboRouterState;

        public override bool IsFullRadial360 => CurrentPhase >= 3 || (bossConfig != null && bossConfig.FullRadial360);

        protected override void Awake()
        {
            MaxPhases = 3;

            telegraphIndicator = GetComponent<TelegraphIndicator>();
            if (telegraphIndicator == null)
            {
                telegraphIndicator = gameObject.AddComponent<TelegraphIndicator>();
            }

            ResolveMinionAssets();

            base.Awake();
        }

        private void ResolveMinionAssets()
        {
#if UNITY_EDITOR
            if (walkerPrefab == null) walkerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Walker.prefab");
            if (walkerData == null) walkerData = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/EnemyData_Walker.asset");
            if (runnerPrefab == null) runnerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Runner.prefab");
            if (runnerData == null) runnerData = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/EnemyData_Runner.asset");
            if (dasherPrefab == null) dasherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Dasher.prefab");
            if (dasherData == null) dasherData = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/EnemyData_Dasher.asset");
            if (rangedPrefab == null) rangedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Ranged.prefab");
            if (rangedData == null) rangedData = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/EnemyData_Ranged.asset");
#endif
        }

        protected override void InitializeStates()
        {
            float introTime = bossConfig != null ? bossConfig.IntroDuration : 1.2f;
            float transitionTime = bossConfig != null ? bossConfig.PhaseTransitionDuration : 1.5f;

            // Decision & Reposition loop
            pickAttackState = new BossPickAttackState(this, stateMachine);
            float minRepo = bossConfig != null ? bossConfig.MinRepositionDuration : 0.4f;
            float maxRepo = bossConfig != null ? bossConfig.MaxRepositionDuration : 1.2f;
            repositionState = new BossRepositionState(this, stateMachine, pickAttackState, minRepo, maxRepo);

            // Combo router manages returning to reposition or chaining attack #2 during Enrage
            comboRouterState = new OverlordComboRouterState(this, stateMachine, pickAttackState, repositionState);

            // Modular Attacks (all report to repositionState; BossRepositionState handles 2-Attack Enrage chaining)
            chargeAttack = new BossChargeAttackState(this, stateMachine, telegraphIndicator, repositionState);
            slamAttack = new BossSlamAttackState(this, stateMachine, telegraphIndicator, repositionState);
            radialBurstAttack = new BossRadialBurstState(this, stateMachine, telegraphIndicator, repositionState);
            burrowEmergeAttack = new BossBurrowEmergeState(this, stateMachine, telegraphIndicator, repositionState);
            rapidBarrageAttack = new BossRapidBarrageState(this, stateMachine, telegraphIndicator, repositionState);

            // Multi-Minion squad configuration
            List<BossMultiSummonState.MinionSquadEntry> squad = new List<BossMultiSummonState.MinionSquadEntry>
            {
                new BossMultiSummonState.MinionSquadEntry(walkerPrefab, walkerData, 2),
                new BossMultiSummonState.MinionSquadEntry(runnerPrefab, runnerData, 2),
                new BossMultiSummonState.MinionSquadEntry(dasherPrefab, dasherData, 1),
                new BossMultiSummonState.MinionSquadEntry(rangedPrefab, rangedData, 1)
            };
            multiSummonAttack = new BossMultiSummonState(this, stateMachine, telegraphIndicator, repositionState, squad, 8);

            // Phase 1: Charge + Slam + FanShot + RapidBarrage
            phase1Attacks.Clear();
            phase1Attacks.Add(chargeAttack);
            phase1Attacks.Add(slamAttack);
            phase1Attacks.Add(radialBurstAttack);
            phase1Attacks.Add(rapidBarrageAttack);

            // Phase 2 & 3: Charge + Slam + RadialBurst (360 in Phase 3) + BurrowEmerge + RapidBarrage + MultiSummon
            phase2Attacks.Clear();
            phase2Attacks.Add(chargeAttack);
            phase2Attacks.Add(slamAttack);
            phase2Attacks.Add(radialBurstAttack);
            phase2Attacks.Add(burrowEmergeAttack);
            phase2Attacks.Add(rapidBarrageAttack);
            phase2Attacks.Add(multiSummonAttack);

            // Transitions & Death
            phaseTransitionState = new BossPhaseTransitionState(this, stateMachine, repositionState, transitionTime);
            bossDeadState = new BossDeadState(this, stateMachine, 1.5f);
            deadState = bossDeadState;

            // Intro
            introState = new BossIntroState(this, stateMachine, repositionState, introTime);
        }

        public override List<IState> GetCurrentPhaseAttacks()
        {
            if (CurrentPhase >= 2)
            {
                return phase2Attacks;
            }

            return phase1Attacks;
        }

        /// <summary>
        /// Router state implementing the Phase 3 2-Attack Combo:
        /// After attack 1 finishes, immediately picks and launches attack 2.
        /// After attack 2 finishes, returns to repositioning state.
        /// </summary>
        private class OverlordComboRouterState : IState
        {
            private readonly OverlordBoss boss;
            private readonly StateMachine stateMachine;
            private readonly IState pickAttackState;
            private readonly IState repositionState;
            private int comboStep = 0;

            public OverlordComboRouterState(OverlordBoss boss, StateMachine stateMachine, IState pickAttackState, IState repositionState)
            {
                this.boss = boss;
                this.stateMachine = stateMachine;
                this.pickAttackState = pickAttackState;
                this.repositionState = repositionState;
            }

            public void Enter()
            {
                // In Phase 3 Enrage, execute 2-Attack combo
                if (boss.CurrentPhase >= 3)
                {
                    if (comboStep == 0)
                    {
                        comboStep = 1;
                        // Chain immediately into 2nd attack of combo!
                        stateMachine.ChangeState(pickAttackState);
                        return;
                    }
                }

                // Normal attack finished or 2nd combo attack finished
                comboStep = 0;
                stateMachine.ChangeState(repositionState);
            }

            public void Tick() { }
            public void FixedTick() { }
            public void Exit() { }
        }
    }
}
