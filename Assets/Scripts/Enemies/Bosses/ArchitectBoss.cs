using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// The Architect Boss (Room 6 Encounter - Base Max HP: 30,000).
    /// Master mechanical engineer commanding autonomous gun turrets and electric EMP shockwaves.
    /// Phase 1 (100% - 66%): Deploys Gun Turrets + Electric EMP Slam + Radial Plasma Burst.
    /// Phase 2 (66% - 33% - Overclock Protocol): Move Speed +20%, Turrets fire at 2x rate, adds Plasma Gatling Barrage.
    /// Phase 3 (< 33% or 180s - Enrage): Move Speed +35%, 360 Plasma Burst, chains major attacks back-to-back.
    /// </summary>
    public class ArchitectBoss : BossBase
    {
        public override float DefaultBaseMaxHP => 30000f;

        [Header("Turret Prefab")]
        [SerializeField] private GameObject turretPrefab;
        [SerializeField] private int maxTurretsCap = 4;

        private TelegraphIndicator telegraphIndicator;
        private readonly List<BossTurret> activeTurrets = new List<BossTurret>();

        // Attack Modules
        private BossDeployTurretsState deployTurretsAttack;
        private BossSlamAttackState empSlamAttack;
        private BossRadialBurstState plasmaBurstAttack;
        private BossRapidBarrageState plasmaGatlingAttack;

        // Attack Pools
        private readonly List<IState> phase1Attacks = new List<IState>();
        private readonly List<IState> phase2Attacks = new List<IState>();
        private readonly List<IState> phase3Attacks = new List<IState>();

        public int ActiveTurretCount
        {
            get
            {
                activeTurrets.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
                return activeTurrets.Count;
            }
        }

        protected override void Awake()
        {
            MaxPhases = 3;
            telegraphIndicator = GetComponent<TelegraphIndicator>();
            if (telegraphIndicator == null)
            {
                telegraphIndicator = gameObject.AddComponent<TelegraphIndicator>();
            }

            base.Awake();
        }

        protected override void InitializeStates()
        {
            float introTime = bossConfig != null ? bossConfig.IntroDuration : 1.2f;
            float transitionTime = bossConfig != null ? bossConfig.PhaseTransitionDuration : 1.5f;

            // Decision & Reposition loop
            pickAttackState = new BossPickAttackState(this, stateMachine);
            float minRepo = bossConfig != null ? bossConfig.MinRepositionDuration : 0.5f;
            float maxRepo = bossConfig != null ? bossConfig.MaxRepositionDuration : 1.4f;
            repositionState = new BossRepositionState(this, stateMachine, pickAttackState, minRepo, maxRepo);

            // Modular Attacks
            deployTurretsAttack = new BossDeployTurretsState(this, stateMachine, telegraphIndicator, repositionState);
            empSlamAttack = new BossSlamAttackState(this, stateMachine, telegraphIndicator, repositionState);
            plasmaBurstAttack = new BossRadialBurstState(this, stateMachine, telegraphIndicator, repositionState);
            plasmaGatlingAttack = new BossRapidBarrageState(this, stateMachine, telegraphIndicator, repositionState);

            // Phase 1: Deploy Turrets + EMP Slam + Plasma Burst
            phase1Attacks.Clear();
            phase1Attacks.Add(deployTurretsAttack);
            phase1Attacks.Add(empSlamAttack);
            phase1Attacks.Add(plasmaBurstAttack);

            // Phase 2: Deploy Turrets + EMP Slam + Plasma Burst + Plasma Gatling
            phase2Attacks.Clear();
            phase2Attacks.Add(deployTurretsAttack);
            phase2Attacks.Add(empSlamAttack);
            phase2Attacks.Add(plasmaBurstAttack);
            phase2Attacks.Add(plasmaGatlingAttack);

            // Phase 3 (Enrage): Heavy barrage + 360 Plasma Burst + EMP Slam + Turrets
            phase3Attacks.Clear();
            phase3Attacks.Add(plasmaGatlingAttack);
            phase3Attacks.Add(plasmaBurstAttack);
            phase3Attacks.Add(empSlamAttack);
            phase3Attacks.Add(deployTurretsAttack);

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

            // Overclock all currently active turrets to fire 2x faster!
            for (int i = 0; i < activeTurrets.Count; i++)
            {
                if (activeTurrets[i] != null)
                {
                    activeTurrets[i].SetOverclock(true);
                }
            }
        }

        public void DeployTurrets(int count)
        {
            if (turretPrefab == null)
            {
                // Fallback: look in Resources or AssetDatabase
#if UNITY_EDITOR
                turretPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss_Turret.prefab");
#endif
            }

            if (turretPrefab == null) return;

            int currentCount = ActiveTurretCount;
            int availableToSpawn = Mathf.Max(0, maxTurretsCap - currentCount);
            int toSpawn = Mathf.Min(count, availableToSpawn);

            float deployRadius = 2.8f;
            float baseAngle = Random.Range(0f, 360f);

            for (int i = 0; i < toSpawn; i++)
            {
                float angle = (baseAngle + (i * (360f / toSpawn))) * Mathf.Deg2Rad;
                Vector3 spawnOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * deployRadius;
                Vector3 spawnPos = transform.position + spawnOffset;

                GameObject turretObj = Instantiate(turretPrefab, spawnPos, Quaternion.identity);
                BossTurret turret = turretObj.GetComponent<BossTurret>();

                if (turret != null)
                {
                    GameObject projPrefab = bossConfig != null ? bossConfig.ProjectilePrefab : null;
                    turret.Setup(projPrefab, 10f * DifficultyMultiplier, 6.5f);
                    if (CurrentPhase >= 2) turret.SetOverclock(true);

                    turret.OnTurretDestroyed += HandleTurretDestroyed;
                    activeTurrets.Add(turret);
                }
            }

            Debug.Log($"[{name}] Deployed {toSpawn} turrets! (Active: {ActiveTurretCount}/{maxTurretsCap})");
        }

        private void HandleTurretDestroyed(BossTurret turret)
        {
            activeTurrets.Remove(turret);
        }

        public override void NotifyBossDefeated()
        {
            // Destroy all active turrets when the Architect is defeated
            while (activeTurrets.Count > 0)
            {
                int last = activeTurrets.Count - 1;
                BossTurret turret = activeTurrets[last];
                activeTurrets.RemoveAt(last);
                if (turret != null) turret.DestroyTurret();
            }

            base.NotifyBossDefeated();
        }

        public override List<IState> GetCurrentPhaseAttacks()
        {
            if (IsEnraged || CurrentPhase >= 3)
            {
                return phase3Attacks;
            }
            if (CurrentPhase == 2)
            {
                return phase2Attacks;
            }

            return phase1Attacks;
        }
    }
}
