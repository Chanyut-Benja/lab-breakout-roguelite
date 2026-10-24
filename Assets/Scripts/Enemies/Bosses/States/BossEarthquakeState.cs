using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// Digger Boss Phase 3 Earthquake / Fissure attack (Room 12).
    /// 1. Displays arena-wide ground fissure telegraph warnings (1.0s warning).
    /// 2. Shakes the arena and shatters the floor, dealing damage to targets in the fissure zones.
    /// 3. Recovers and returns to reposition.
    /// </summary>
    public class BossEarthquakeState : EnemyState
    {
        private enum Step { Telegraph, Shatter, Recover }

        private readonly DiggerBoss digger;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private Step currentStep;
        private float stepStartTime;
        private bool hasDealtDamage;

        private const float TelegraphDuration = 1.0f;
        private const float ShatterDuration = 0.5f;
        private const float EarthquakeDamage = 25f;

        public BossEarthquakeState(DiggerBoss digger, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
            : base(digger, stateMachine)
        {
            this.digger = digger;
            this.indicator = indicator;
            this.nextState = nextState;
        }

        public override void Enter()
        {
            base.Enter();
            digger.StopMovement();
            currentStep = Step.Telegraph;
            stepStartTime = Time.time;
            hasDealtDamage = false;

            if (indicator != null)
            {
                // Circular tremor warning around arena
                indicator.ShowCircle(digger.transform.position, 9f, 36, 0.2f);
            }
        }

        public override void Tick()
        {
            base.Tick();
            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case Step.Telegraph:
                    if (elapsed >= TelegraphDuration)
                    {
                        currentStep = Step.Shatter;
                        stepStartTime = Time.time;
                        if (indicator != null) indicator.Hide();
                    }
                    break;

                case Step.Shatter:
                    if (!hasDealtDamage)
                    {
                        hasDealtDamage = true;
                        if (digger.Target != null)
                        {
                            float dist = Vector2.Distance(digger.transform.position, digger.Target.position);
                            if (dist <= 9.5f)
                            {
                                IDamageable dmg = digger.Target.GetComponentInChildren<IDamageable>() ?? digger.Target.GetComponentInParent<IDamageable>();
                                if (dmg != null)
                                {
                                    float totalDamage = EarthquakeDamage * digger.DifficultyMultiplier;
                                    Vector2 kb = ((Vector2)digger.Target.position - (Vector2)digger.transform.position).normalized * 5f;
                                    dmg.TakeDamage(totalDamage, kb);
                                    Debug.Log($"[{digger.name}] Earthquake shattered ground, hitting Target for {totalDamage} damage!");
                                }
                            }
                        }
                    }

                    if (elapsed >= ShatterDuration)
                    {
                        currentStep = Step.Recover;
                        stepStartTime = Time.time;
                    }
                    break;

                case Step.Recover:
                    float recoverTime = (digger.Data != null ? digger.Data.RecoverDuration : 1.2f) * digger.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        public override void Exit()
        {
            base.Exit();
            if (indicator != null) indicator.Hide();
        }
    }
}
