using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Initial spawn state for enemy.
    /// Handles spawn VFX/SFX and brief spawn buffer before engaging Chase.
    /// </summary>
    public class SpawnState : EnemyState
    {
        private readonly IState nextState;
        private readonly float spawnDuration;

        public SpawnState(EnemyBase enemy, StateMachine stateMachine, IState nextState, float spawnDuration = 0.2f)
            : base(enemy, stateMachine)
        {
            this.nextState = nextState;
            this.spawnDuration = spawnDuration;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            // Trigger Spawn SFX
            if (data != null && data.SpawnSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.SpawnSFX);
            }

            // Spawn VFX
            if (data != null && data.SpawnVFX != null)
            {
                Object.Instantiate(data.SpawnVFX, enemy.transform.position, Quaternion.identity);
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Spawn");
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= spawnDuration)
            {
                stateMachine.ChangeState(nextState);
            }
        }
    }
}
