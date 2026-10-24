using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Displays a circular ground warning telegraph at the player's position for 0.8s before emergence.
    /// </summary>
    public class EmergeTelegraphState : EnemyState
    {
        private readonly TelegraphIndicator telegraphIndicator;
        private readonly IState emergeState;
        private Vector3 emergePoint;
        private readonly float duration;

        public Vector3 EmergePoint => emergePoint;

        public EmergeTelegraphState(EnemyBase enemy, StateMachine stateMachine, TelegraphIndicator indicator, IState emergeState, float duration = 0.8f)
            : base(enemy, stateMachine)
        {
            this.telegraphIndicator = indicator;
            this.emergeState = emergeState;
            this.duration = duration;
        }

        public override void Enter()
        {
            base.Enter();

            enemy.StopMovement();

            // Lock emerge position at player's position
            emergePoint = enemy.Target != null ? enemy.Target.position : enemy.transform.position;

            // Teleport or place position directly under emerge point
            enemy.transform.position = emergePoint;
            if (enemy.Rigidbody != null)
            {
                enemy.Rigidbody.position = emergePoint;
            }

            // Display ground circle telegraph
            if (telegraphIndicator != null)
            {
                float radius = data != null ? data.EmergeAoERadius : 1.2f;
                telegraphIndicator.ShowCircle(emergePoint, radius, 28, 0.1f);
            }

            // Play Telegraph SFX
            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (StateElapsedTime >= duration)
            {
                if (telegraphIndicator != null)
                {
                    telegraphIndicator.Hide();
                }

                stateMachine.ChangeState(emergeState);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (telegraphIndicator != null)
            {
                telegraphIndicator.Hide();
            }
        }
    }
}
