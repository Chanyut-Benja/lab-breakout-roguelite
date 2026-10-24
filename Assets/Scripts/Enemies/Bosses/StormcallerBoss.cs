using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// The Stormcaller Boss (Room 15 Encounter - Base Max HP: 110,000).
    /// Elemental storm sovereign specializing in dense Bullet Hell patterns and targeted lightning strikes.
    /// Phase 1 (100% - 66%): Spiral Bullet Hell + Lightning Storm Strikes + Electric Radial Burst.
    /// Phase 2 (66% - 33% - Supercell Vortex): Move Speed +20%, Dual-Spiral 6-arm bullet storm, faster lightning strikes,
    /// and 360-degree electric nova bursts.
    /// Phase 3 (< 33% or 180s - Enrage): Move Speed +35%, chains major lightning & spiral attacks back-to-back.
    /// </summary>
    public class StormcallerBoss : BossBase
    {
        public override float DefaultBaseMaxHP => 110000f;

        private TelegraphIndicator telegraphIndicator;

        // Attack Modules
        private BossSpiralBulletHellState spiralBulletHellAttack;
        private BossLightningStormState lightningStormAttack;
        private BossRadialBurstState electricNovaAttack;

        // Attack Pools
        private readonly List<IState> phase1Attacks = new List<IState>();
        private readonly List<IState> phase2Attacks = new List<IState>();

        public override bool IsFullRadial360 => CurrentPhase >= 2 || (bossConfig != null && bossConfig.FullRadial360);

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
            float minRepo = bossConfig != null ? bossConfig.MinRepositionDuration : 0.4f;
            float maxRepo = bossConfig != null ? bossConfig.MaxRepositionDuration : 1.2f;
            repositionState = new BossRepositionState(this, stateMachine, pickAttackState, minRepo, maxRepo);

            // Modular Attacks
            spiralBulletHellAttack = new BossSpiralBulletHellState(this, stateMachine, telegraphIndicator, repositionState);
            lightningStormAttack = new BossLightningStormState(this, stateMachine, telegraphIndicator, repositionState);
            electricNovaAttack = new BossRadialBurstState(this, stateMachine, telegraphIndicator, repositionState);

            // Phase 1: Spiral Bullet Hell + Lightning Storm + Electric Nova
            phase1Attacks.Clear();
            phase1Attacks.Add(spiralBulletHellAttack);
            phase1Attacks.Add(lightningStormAttack);
            phase1Attacks.Add(electricNovaAttack);

            // Phase 2: Enhanced Spiral (6-arm) + Lightning Storm + 360 Electric Nova
            phase2Attacks.Clear();
            phase2Attacks.Add(spiralBulletHellAttack);
            phase2Attacks.Add(lightningStormAttack);
            phase2Attacks.Add(electricNovaAttack);

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
    }
}
