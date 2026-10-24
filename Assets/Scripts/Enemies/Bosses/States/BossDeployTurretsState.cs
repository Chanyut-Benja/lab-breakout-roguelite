using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// The Architect Signature Attack: DeployTurrets.
    /// Telegraphs and deploys autonomous gun turrets across the arena.
    /// </summary>
    public class BossDeployTurretsState : EnemyState
    {
        private enum DeployStep { Telegraph, Spawning, Recover }

        private readonly ArchitectBoss architect;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private DeployStep currentStep;
        private float stepStartTime;

        public BossDeployTurretsState(ArchitectBoss architect, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
            : base(architect, stateMachine)
        {
            this.architect = architect;
            this.indicator = indicator;
            this.nextState = nextState;
        }

        public override void Enter()
        {
            base.Enter();

            architect.StopMovement();
            currentStep = DeployStep.Telegraph;
            stepStartTime = Time.time;

            if (indicator != null)
            {
                indicator.ShowCircle(architect.transform.position, 2.5f, 28, 0.25f);
            }

            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }

            if (architect.Animator != null)
            {
                architect.Animator.SetTrigger("Telegraph");
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case DeployStep.Telegraph:
                    float telegraphTime = 0.6f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = DeployStep.Spawning;
                        stepStartTime = Time.time;
                        DeployTurrets();
                    }
                    break;

                case DeployStep.Spawning:
                    if (elapsed >= 0.2f)
                    {
                        currentStep = DeployStep.Recover;
                        stepStartTime = Time.time;

                        if (architect.Animator != null)
                        {
                            architect.Animator.SetBool("IsMoving", false);
                            architect.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case DeployStep.Recover:
                    float recoverTime = 1.0f * architect.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void DeployTurrets()
        {
            int turretCount = architect.CurrentPhase >= 2 ? 3 : 2;
            architect.DeployTurrets(turretCount);

            if (architect.Animator != null) architect.Animator.SetTrigger("Attack");
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            architect.StopMovement();
        }
    }
}
