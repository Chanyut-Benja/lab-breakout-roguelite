using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// The Doctor Signature Attack: AcidFlask (ปาขวดสารเคมีพิษ / Toxic Chemical Pools).
    /// Throws shatter-on-impact flasks that deal burst damage and spawn lingering acid puddles on the ground.
    /// In Phase 2/3, throws clusters of 3-5 flasks covering large combat zones.
    /// </summary>
    public class BossAcidFlaskState : EnemyState
    {
        private enum FlaskStep { Telegraph, Throwing, Recover }

        private readonly DoctorBoss doctor;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private FlaskStep currentStep;
        private float stepStartTime;
        private Vector3 targetThrowPos;

        public BossAcidFlaskState(DoctorBoss doctor, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
            : base(doctor, stateMachine)
        {
            this.doctor = doctor;
            this.indicator = indicator;
            this.nextState = nextState;
        }

        public override void Enter()
        {
            base.Enter();

            doctor.StopMovement();
            currentStep = FlaskStep.Telegraph;
            stepStartTime = Time.time;

            targetThrowPos = doctor.Target != null ? doctor.Target.position : doctor.transform.position;

            if (indicator != null)
            {
                indicator.ShowCircle(targetThrowPos, 2.5f, 28, 0.25f);
            }

            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }

            if (doctor.Animator != null)
            {
                doctor.Animator.SetTrigger("Telegraph");
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case FlaskStep.Telegraph:
                    float telegraphTime = 0.6f;
                    if (elapsed >= telegraphTime)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = FlaskStep.Throwing;
                        stepStartTime = Time.time;
                        ThrowFlasks();
                    }
                    break;

                case FlaskStep.Throwing:
                    if (elapsed >= 0.25f)
                    {
                        currentStep = FlaskStep.Recover;
                        stepStartTime = Time.time;

                        if (doctor.Animator != null)
                        {
                            doctor.Animator.SetBool("IsMoving", false);
                            doctor.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case FlaskStep.Recover:
                    float recoverTime = 1.0f * doctor.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void ThrowFlasks()
        {
            int flaskCount = doctor.CurrentPhase >= 3 ? 4 : (doctor.CurrentPhase == 2 ? 3 : 2);
            doctor.ThrowAcidFlasks(targetThrowPos, flaskCount);

            if (doctor.Animator != null) doctor.Animator.SetTrigger("Attack");
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            doctor.StopMovement();
        }
    }
}
