using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Brute Boss (Room 3 Encounter - Base Max HP: 12,000).
    /// Heavy melee juggernaut that tests player evasion and repositioning.
    /// Phase 1 (100% - 66%): ChargeAttack + SlamAttack (alternating, never consecutive).
    /// Phase 2 (66% - 33%): Move Speed +20%, faster attacks, adds SummonMinions (Walker x4 capped).
    /// Phase 3 (< 33% or 180s - Enrage): Move Speed +35%, chains major Charge & Slam attacks back-to-back.
    /// </summary>
    public class BruteBoss : BossBase
    {
        public override float DefaultBaseMaxHP => 12000f;

        private TelegraphIndicator telegraphIndicator;

        // Attack Modules
        private BossChargeAttackState chargeAttack;
        private BossSlamAttackState slamAttack;
        private BossSummonMinionsState summonMinions;

        // Attack Pools
        private readonly List<IState> phase1Attacks = new List<IState>();
        private readonly List<IState> phase2Attacks = new List<IState>();
        private readonly List<IState> phase3Attacks = new List<IState>();

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
            float maxRepo = bossConfig != null ? bossConfig.MaxRepositionDuration : 1.5f;
            repositionState = new BossRepositionState(this, stateMachine, pickAttackState, minRepo, maxRepo);

            // Attack Modules (each returns to repositionState after vulnerable recovery)
            chargeAttack = new BossChargeAttackState(this, stateMachine, telegraphIndicator, repositionState);
            slamAttack = new BossSlamAttackState(this, stateMachine, telegraphIndicator, repositionState);
            summonMinions = new BossSummonMinionsState(this, stateMachine, telegraphIndicator, repositionState);

            // Phase Pools
            phase1Attacks.Clear();
            phase1Attacks.Add(chargeAttack);
            phase1Attacks.Add(slamAttack);

            phase2Attacks.Clear();
            phase2Attacks.Add(chargeAttack);
            phase2Attacks.Add(slamAttack);
            phase2Attacks.Add(summonMinions);

            phase3Attacks.Clear();
            phase3Attacks.Add(chargeAttack);
            phase3Attacks.Add(slamAttack);
            phase3Attacks.Add(chargeAttack);
            phase3Attacks.Add(slamAttack);
            phase3Attacks.Add(summonMinions);

            // Transition & Death
            phaseTransitionState = new BossPhaseTransitionState(this, stateMachine, repositionState, transitionTime);
            bossDeadState = new BossDeadState(this, stateMachine, 1.2f);
            deadState = bossDeadState;

            // Intro
            introState = new BossIntroState(this, stateMachine, repositionState, introTime);
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
