using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// The Doctor (Room 9 Encounter - Base Max HP: 60,000).
    /// Deranged bio-chemist and surgical horror master commanding chemical hazards,
    /// needle gatling sprays, and grotesque mutant legions.
    /// Phase 1 (100% - 66%): Toxic Acid Flasks + Syringe Gatling Barrage + Chemical Radial Burst.
    /// Phase 2 (66% - 33%): Move Speed +20%, adds Mutagen Squad Summon + Scalpel Rush Charge.
    /// Phase 3 (< 33% or 180s - Experiment Overdose Enrage!): Move Speed +35%, injects mutagen (+20% size),
    /// fires 360-degree chemical ring, 18-round syringe barrage, and executes 2-Attack Combos!
    /// </summary>
    public class DoctorBoss : BossBase
    {
        public override float DefaultBaseMaxHP => 60000f;

        [Header("Acid Pool Hazard")]
        [SerializeField] private GameObject acidPoolPrefab;

        [Header("Mutant Legion Squad")]
        [SerializeField] private GameObject walkerPrefab;
        [SerializeField] private EnemyData walkerData;
        [SerializeField] private GameObject runnerPrefab;
        [SerializeField] private EnemyData runnerData;
        [SerializeField] private GameObject dasherPrefab;
        [SerializeField] private EnemyData dasherData;
        [SerializeField] private GameObject rangedPrefab;
        [SerializeField] private EnemyData rangedData;

        private TelegraphIndicator telegraphIndicator;
        private readonly List<GameObject> activeAcidPools = new List<GameObject>();

        // Attack Modules
        private BossAcidFlaskState acidFlaskAttack;
        private BossRapidBarrageState syringeGatlingAttack;
        private BossRadialBurstState chemicalNovaAttack;
        private BossChargeAttackState scalpelRushAttack;
        private BossMultiSummonState mutantSummonAttack;

        // Attack Pools
        private readonly List<IState> phase1Attacks = new List<IState>();
        private readonly List<IState> phase2Attacks = new List<IState>();

        // Combo Router for Phase 3 2-Attack Combos
        private DoctorComboRouterState comboRouterState;

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
            if (acidPoolPrefab == null) acidPoolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AcidPool.prefab");
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
            comboRouterState = new DoctorComboRouterState(this, stateMachine, pickAttackState, repositionState);

            // Modular Attacks (all report to repositionState; BossRepositionState handles 2-Attack Enrage chaining)
            acidFlaskAttack = new BossAcidFlaskState(this, stateMachine, telegraphIndicator, repositionState);
            syringeGatlingAttack = new BossRapidBarrageState(this, stateMachine, telegraphIndicator, repositionState);
            chemicalNovaAttack = new BossRadialBurstState(this, stateMachine, telegraphIndicator, repositionState);
            scalpelRushAttack = new BossChargeAttackState(this, stateMachine, telegraphIndicator, repositionState);

            // Mutant Squad
            List<BossMultiSummonState.MinionSquadEntry> squad = new List<BossMultiSummonState.MinionSquadEntry>
            {
                new BossMultiSummonState.MinionSquadEntry(walkerPrefab, walkerData, 2),
                new BossMultiSummonState.MinionSquadEntry(runnerPrefab, runnerData, 2),
                new BossMultiSummonState.MinionSquadEntry(dasherPrefab, dasherData, 1),
                new BossMultiSummonState.MinionSquadEntry(rangedPrefab, rangedData, 1)
            };
            mutantSummonAttack = new BossMultiSummonState(this, stateMachine, telegraphIndicator, repositionState, squad, 8);

            // Phase 1: Acid Flasks + Syringe Gatling + Chemical Nova
            phase1Attacks.Clear();
            phase1Attacks.Add(acidFlaskAttack);
            phase1Attacks.Add(syringeGatlingAttack);
            phase1Attacks.Add(chemicalNovaAttack);

            // Phase 2 & 3: Acid Flasks + Syringe Gatling + Chemical Nova + Scalpel Rush + Mutant Squad
            phase2Attacks.Clear();
            phase2Attacks.Add(acidFlaskAttack);
            phase2Attacks.Add(syringeGatlingAttack);
            phase2Attacks.Add(chemicalNovaAttack);
            phase2Attacks.Add(scalpelRushAttack);
            phase2Attacks.Add(mutantSummonAttack);

            // Transitions & Death
            phaseTransitionState = new BossPhaseTransitionState(this, stateMachine, repositionState, transitionTime);
            bossDeadState = new BossDeadState(this, stateMachine, 1.5f);
            deadState = bossDeadState;

            // Intro
            introState = new BossIntroState(this, stateMachine, repositionState, introTime);
        }

        public override void AdvancePhase()
        {
            base.AdvancePhase();

            // In Phase 3, The Doctor overdoses on his own mutagen Formula X
            if (CurrentPhase >= 3)
            {
                transform.localScale *= 1.2f;
                Debug.Log($"[{name}] Injected Formula X Mutagen! Size and speed massively increased!");
            }
        }

        public void ThrowAcidFlasks(Vector3 centerTargetPos, int count)
        {
            if (acidPoolPrefab == null)
            {
#if UNITY_EDITOR
                acidPoolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AcidPool.prefab");
#endif
            }

            if (acidPoolPrefab == null) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = centerTargetPos;
                if (i > 0)
                {
                    Vector2 offset = Random.insideUnitCircle * Random.Range(1.5f, 3.8f);
                    spawnPos += new Vector3(offset.x, offset.y, 0f);
                }

                GameObject poolObj = Instantiate(acidPoolPrefab, spawnPos, Quaternion.identity);
                activeAcidPools.Add(poolObj);
                AcidPool pool = poolObj.GetComponent<AcidPool>();
                if (pool != null)
                {
                    pool.Setup(7f * DifficultyMultiplier, 6.0f, 2.0f);
                }
            }

            Debug.Log($"[{name}] Threw {count} Toxic Acid Flasks!");
        }

        public override void NotifyBossDefeated()
        {
            foreach (GameObject pool in activeAcidPools)
                if (pool != null) Destroy(pool);
            activeAcidPools.Clear();
            base.NotifyBossDefeated();
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
        /// Router state implementing The Doctor's Phase 3 2-Attack Combos.
        /// </summary>
        private class DoctorComboRouterState : IState
        {
            private readonly DoctorBoss boss;
            private readonly StateMachine stateMachine;
            private readonly IState pickAttackState;
            private readonly IState repositionState;
            private int comboStep = 0;

            public DoctorComboRouterState(DoctorBoss boss, StateMachine stateMachine, IState pickAttackState, IState repositionState)
            {
                this.boss = boss;
                this.stateMachine = stateMachine;
                this.pickAttackState = pickAttackState;
                this.repositionState = repositionState;
            }

            public void Enter()
            {
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

                comboStep = 0;
                stateMachine.ChangeState(repositionState);
            }

            public void Tick() { }
            public void FixedTick() { }
            public void Exit() { }
        }
    }
}
