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
    ///
    /// Skill rotation: the Overlord owns the signature moves of every boss (Brute charge + slam, Digger burrow,
    /// Doctor acid flasks + mutant squad, Architect turrets + gatling, Stormcaller lightning + spiral bullets, nova burst),
    /// in stronger versions. It never repeats a skill until it has used every skill available to it, then the cycle restarts.
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
        private BossLightningStormState lightningStormAttack;
        private BossSpiralBulletHellState spiralBulletHellAttack;
        private IState acidFlaskAttack;
        private IState deployTurretsAttack;

        [Header("Borrowed signature skills")]
        [SerializeField] private GameObject acidPoolPrefab;
        [SerializeField] private GameObject turretPrefab;
        [SerializeField] private int maxTurretsCap = 8;
        private readonly List<GameObject> activeAcidPools = new List<GameObject>();
        private readonly List<BossTurret> activeTurrets = new List<BossTurret>();

        // skill rotation (no repeats until every skill was used)
        private readonly List<IState> skillsUsedThisCycle = new List<IState>();
        private IState lastSkill;
        private readonly List<IState> nextSkillHolder = new List<IState>(1);

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
            SkinLibrary.Apply(gameObject, "overlord");   // hand-drawn sprite
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
            if (acidPoolPrefab == null) acidPoolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AcidPool.prefab");
            if (turretPrefab == null) turretPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss_Turret.prefab");
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

            // signature skills borrowed from the other bosses (stronger versions)
            lightningStormAttack = new BossLightningStormState(this, stateMachine, telegraphIndicator, repositionState);      // Stormcaller
            spiralBulletHellAttack = new BossSpiralBulletHellState(this, stateMachine, telegraphIndicator, repositionState);  // Stormcaller
            acidFlaskAttack = new OverlordSkillState(this, stateMachine, telegraphIndicator, repositionState, true, 3.2f, 0.8f, 1.0f, ThrowAcidFlasks);   // Doctor
            deployTurretsAttack = new OverlordSkillState(this, stateMachine, telegraphIndicator, repositionState, false, 2.8f, 0.7f, 1.0f, DeployTurrets);  // Architect

            // Phase 1 pool: Charge + Slam + Nova + Barrage + Acid + Turrets + Lightning + Spiral
            phase1Attacks.Clear();
            phase1Attacks.Add(chargeAttack);
            phase1Attacks.Add(slamAttack);
            phase1Attacks.Add(radialBurstAttack);
            phase1Attacks.Add(rapidBarrageAttack);
            phase1Attacks.Add(acidFlaskAttack);
            phase1Attacks.Add(deployTurretsAttack);
            phase1Attacks.Add(lightningStormAttack);
            phase1Attacks.Add(spiralBulletHellAttack);

            // Phase 2 & 3 pool: everything above + BurrowEmerge + MultiSummon
            phase2Attacks.Clear();
            phase2Attacks.Add(chargeAttack);
            phase2Attacks.Add(slamAttack);
            phase2Attacks.Add(radialBurstAttack);
            phase2Attacks.Add(burrowEmergeAttack);
            phase2Attacks.Add(rapidBarrageAttack);
            phase2Attacks.Add(multiSummonAttack);
            phase2Attacks.Add(acidFlaskAttack);
            phase2Attacks.Add(deployTurretsAttack);
            phase2Attacks.Add(lightningStormAttack);
            phase2Attacks.Add(spiralBulletHellAttack);

            // Transitions & Death
            phaseTransitionState = new BossPhaseTransitionState(this, stateMachine, repositionState, transitionTime);
            bossDeadState = new BossDeadState(this, stateMachine, 1.5f);
            deadState = bossDeadState;

            // Intro
            introState = new BossIntroState(this, stateMachine, repositionState, introTime);
        }

        /// <summary>
        /// Returns ONE skill (the pick state then simply runs it). A skill is never chosen again until every skill of the
        /// current phase pool has been used once; then the cycle starts over (the first skill of a new cycle is never the last one used).
        /// </summary>
        public override List<IState> GetCurrentPhaseAttacks()
        {
            List<IState> pool = CurrentPhase >= 2 ? phase2Attacks : phase1Attacks;
            nextSkillHolder.Clear();
            if (pool.Count == 0) return nextSkillHolder;

            List<IState> candidates = new List<IState>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (!skillsUsedThisCycle.Contains(pool[i])) candidates.Add(pool[i]);
            }
            if (candidates.Count == 0)
            {
                skillsUsedThisCycle.Clear();   // every skill was used: start a new cycle
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool.Count == 1 || pool[i] != lastSkill) candidates.Add(pool[i]);
                }
            }

            IState chosen = candidates[Random.Range(0, candidates.Count)];
            skillsUsedThisCycle.Add(chosen);
            lastSkill = chosen;
            nextSkillHolder.Add(chosen);
            return nextSkillHolder;
        }

        // ------------------------------------------------------------------ Doctor's acid flasks (more, bigger, longer-lasting)
        private void ThrowAcidFlasks()
        {
            if (acidPoolPrefab == null) return;
            activeAcidPools.RemoveAll(x => x == null);

            int count = 5 + Mathf.Clamp(CurrentPhase, 1, 3);   // 6 / 7 / 8 pools
            Vector3 centre = Target != null ? Target.position : transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = centre;
                if (i > 0)
                {
                    Vector2 off = Random.insideUnitCircle * Random.Range(1.5f, 5.5f);
                    pos += new Vector3(off.x, off.y, 0f);
                }
                GameObject poolObj = Instantiate(acidPoolPrefab, pos, Quaternion.identity);
                activeAcidPools.Add(poolObj);
                AcidPool pool = poolObj.GetComponent<AcidPool>();
                if (pool != null) pool.Setup(10f * DifficultyMultiplier, 7.0f, 2.4f);
            }
        }

        // ------------------------------------------------------------------ Architect's turrets (more, overclocked from phase 2)
        private void DeployTurrets()
        {
            if (turretPrefab == null) return;
            activeTurrets.RemoveAll(x => x == null);

            int count = 3 + Mathf.Clamp(CurrentPhase, 1, 3);   // 4 / 5 / 6 turrets per cast
            int toSpawn = Mathf.Min(count, Mathf.Max(0, maxTurretsCap - activeTurrets.Count));
            float baseAngle = Random.Range(0f, 360f);
            for (int i = 0; i < toSpawn; i++)
            {
                float angle = (baseAngle + i * (360f / Mathf.Max(1, toSpawn))) * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 3.4f;
                GameObject obj = Instantiate(turretPrefab, pos, Quaternion.identity);
                BossTurret turret = obj.GetComponent<BossTurret>();
                if (turret == null) continue;
                GameObject projPrefab = bossConfig != null ? bossConfig.ProjectilePrefab : null;
                turret.Setup(projPrefab, 14f * DifficultyMultiplier, 7.5f);
                if (CurrentPhase >= 2) turret.SetOverclock(true);
                turret.OnTurretDestroyed += HandleTurretDestroyed;
                activeTurrets.Add(turret);
            }
        }

        private void HandleTurretDestroyed(BossTurret turret)
        {
            activeTurrets.Remove(turret);
        }

        public override void AdvancePhase()
        {
            base.AdvancePhase();
            for (int i = 0; i < activeTurrets.Count; i++)
            {
                if (activeTurrets[i] != null) activeTurrets[i].SetOverclock(true);
            }
        }

        public override void NotifyBossDefeated()
        {
            for (int i = activeTurrets.Count - 1; i >= 0; i--)
            {
                if (activeTurrets[i] != null) activeTurrets[i].DestroyTurret();
            }
            activeTurrets.Clear();
            for (int i = 0; i < activeAcidPools.Count; i++)
            {
                if (activeAcidPools[i] != null) Destroy(activeAcidPools[i]);
            }
            activeAcidPools.Clear();
            base.NotifyBossDefeated();
        }

        /// <summary>A simple signature move: telegraph circle, then fire an action, then recover.</summary>
        private class OverlordSkillState : EnemyState
        {
            private enum Step { Telegraph, Recover }

            private readonly OverlordBoss boss;
            private readonly TelegraphIndicator indicator;
            private readonly IState nextState;
            private readonly bool atTarget;
            private readonly float radius, telegraphTime, recoverTime;
            private readonly System.Action fire;

            private Step step;
            private float stepStart;

            public OverlordSkillState(OverlordBoss boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState,
                                      bool atTarget, float radius, float telegraphTime, float recoverTime, System.Action fire)
                : base(boss, stateMachine)
            {
                this.boss = boss;
                this.indicator = indicator;
                this.nextState = nextState;
                this.atTarget = atTarget;
                this.radius = radius;
                this.telegraphTime = telegraphTime;
                this.recoverTime = recoverTime;
                this.fire = fire;
            }

            public override void Enter()
            {
                base.Enter();
                boss.StopMovement();
                step = Step.Telegraph;
                stepStart = Time.time;

                Vector3 pos = atTarget && boss.Target != null ? boss.Target.position : boss.transform.position;
                if (indicator != null) indicator.ShowCircle(pos, radius, 28, 0.25f);
                if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(data.TelegraphSFX);
                if (boss.Animator != null) boss.Animator.SetTrigger("Telegraph");
            }

            public override void Tick()
            {
                base.Tick();
                float elapsed = Time.time - stepStart;

                if (step == Step.Telegraph)
                {
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();
                        if (fire != null) fire();
                        if (boss.Animator != null) boss.Animator.SetTrigger("Attack");
                        if (data != null && data.AttackSFX != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(data.AttackSFX);
                        step = Step.Recover;
                        stepStart = Time.time;
                    }
                }
                else if (elapsed >= recoverTime * boss.RecoverDurationMultiplier)
                {
                    stateMachine.ChangeState(nextState);
                }
            }

            public override void Exit()
            {
                base.Exit();
                if (indicator != null) indicator.Hide();
                boss.StopMovement();
            }
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
