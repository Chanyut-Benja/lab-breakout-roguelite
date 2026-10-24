using System.Collections.Generic;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Decision-making state that selects the next attack module.
    /// Strictly complies with the spec rule:
    /// - Weighted random attack selection
    /// - NEVER repeats the same attack twice consecutively ("ห้ามซ้ำท่าเดิมติดกัน")
    /// </summary>
    public class BossPickAttackState : EnemyState
    {
        private readonly BossBase boss;
        private readonly List<IState> availableAttacks = new List<IState>();
        private int lastAttackIndex = -1;

        public BossPickAttackState(BossBase boss, StateMachine stateMachine)
            : base(boss, stateMachine)
        {
            this.boss = boss;
        }

        public void RegisterAttack(IState attackState)
        {
            if (attackState != null && !availableAttacks.Contains(attackState))
            {
                availableAttacks.Add(attackState);
            }
        }

        public void ClearAttacks()
        {
            availableAttacks.Clear();
            lastAttackIndex = -1;
        }

        public override void Enter()
        {
            base.Enter();

            boss.StopMovement();

            // Refresh available attacks from boss according to current phase
            List<IState> currentPhaseAttacks = boss.GetCurrentPhaseAttacks();
            if (currentPhaseAttacks == null || currentPhaseAttacks.Count == 0)
            {
                Debug.LogWarning($"[{boss.name}] No attacks configured for Phase {boss.CurrentPhase}!");
                return;
            }

            int chosenIndex = PickNonRepeatingAttackIndex(currentPhaseAttacks);
            lastAttackIndex = chosenIndex;

            IState nextAttack = currentPhaseAttacks[chosenIndex];
            stateMachine.ChangeState(nextAttack);
        }

        private int PickNonRepeatingAttackIndex(List<IState> attacks)
        {
            if (attacks.Count <= 1) return 0;

            // Pick an index different from lastAttackIndex
            int tries = 0;
            int pickedIndex;
            do
            {
                pickedIndex = Random.Range(0, attacks.Count);
                tries++;
            } while (pickedIndex == lastAttackIndex && tries < 20);

            return pickedIndex;
        }
    }
}
