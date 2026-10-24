using Enemies.Core;
using UnityEngine;

namespace Enemies.States
{
    /// <summary>
    /// Burrow state for Burrower enemy and Boss burrow attacks.
    /// Enemy becomes invulnerable and semi-transparent (underground) while tunneling fast toward player.
    /// Transitions to EmergeTelegraphState upon reaching target area.
    /// </summary>
    public class BurrowState : EnemyState
    {
        private readonly IState emergeTelegraphState;
        private readonly float maxBurrowDuration;
        private Color originalColor;

        public BurrowState(EnemyBase enemy, StateMachine stateMachine, IState emergeTelegraphState, float maxBurrowDuration = 3.0f)
            : base(enemy, stateMachine)
        {
            this.emergeTelegraphState = emergeTelegraphState;
            this.maxBurrowDuration = maxBurrowDuration;
        }

        public override void Enter()
        {
            base.Enter();

            // Set invulnerable per spec
            if (enemy.Health != null)
            {
                enemy.Health.IsInvulnerable = true;
            }

            // Visual underground effect (dim sprite to represent underground dirt shadow)
            if (enemy.SpriteRenderer != null)
            {
                originalColor = enemy.SpriteRenderer.color;
                enemy.SpriteRenderer.color = new Color(0.4f, 0.3f, 0.2f, 0.4f);
            }

            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Burrow");
            }
        }

        public override void FixedTick()
        {
            base.FixedTick();

            if (enemy.Target == null)
            {
                enemy.StopMovement();
                return;
            }

            // Move fast underground directly toward player
            Vector2 toTarget = (Vector2)(enemy.Target.position - enemy.transform.position);
            float dist = toTarget.magnitude;

            float burrowSpeed = data != null ? data.UndergroundSpeed : 4.0f;
            enemy.Move(toTarget.normalized, burrowSpeed);

            // Once within close range of player or max time elapsed, emerge
            if (dist <= 0.8f || StateElapsedTime >= maxBurrowDuration)
            {
                stateMachine.ChangeState(emergeTelegraphState);
            }
        }

        public override void Exit()
        {
            base.Exit();
            enemy.StopMovement();
        }
    }
}
