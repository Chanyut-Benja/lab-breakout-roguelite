using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Digger Boss (Room 12 Encounter - Base Max HP: 80,000).
    /// Subterranean ambush predator that tests spatial awareness and rapid dodging.
    /// Phase 1 (100% - 66%): BurrowEmerge (single) + FanShot (RadialBurst).
    /// Phase 2 (66% - 33%): Move Speed +20%, adds SummonMinions (Burrower x2 capped) and Double Emerge combo.
    /// Phase 3 (< 33% or 180s - Enrage): Move Speed +35%, Triple Emerge + 360 Radial Burst, chains major attacks back-to-back.
    /// </summary>
    public class DiggerBoss : BossBase
    {
        public override float DefaultBaseMaxHP => 80000f;

        private TelegraphIndicator telegraphIndicator;

        // Attack Modules
        private BossBurrowEmergeState burrowEmergeAttack;
        private BossRadialBurstState radialBurstAttack;
        private BossSummonMinionsState summonMinionsAttack;

        // Attack Pools
        private readonly List<IState> phase1Attacks = new List<IState>();
        private readonly List<IState> phase2Attacks = new List<IState>();

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

            // Modular Attacks
            burrowEmergeAttack = new BossBurrowEmergeState(this, stateMachine, telegraphIndicator, repositionState);
            // Single Emerge in Phase 1, Double Emerge in Phase 2, Triple Emerge in Phase 3 (Enrage)
            burrowEmergeAttack.EmergeCountGetter = () => IsEnraged ? 3 : (CurrentPhase >= 2 ? 2 : 1);

            radialBurstAttack = new BossRadialBurstState(this, stateMachine, telegraphIndicator, repositionState);
            summonMinionsAttack = new BossSummonMinionsState(this, stateMachine, telegraphIndicator, repositionState);

            // Phase Pools
            phase1Attacks.Clear();
            phase1Attacks.Add(burrowEmergeAttack);
            phase1Attacks.Add(radialBurstAttack);

            phase2Attacks.Clear();
            phase2Attacks.Add(burrowEmergeAttack);
            phase2Attacks.Add(radialBurstAttack);
            phase2Attacks.Add(summonMinionsAttack);

            // Transition & Death
            phaseTransitionState = new BossPhaseTransitionState(this, stateMachine, repositionState, transitionTime);
            bossDeadState = new BossDeadState(this, stateMachine, 1.2f);
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
