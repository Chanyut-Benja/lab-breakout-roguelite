using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// The Architect's Phase 2 Grid Laser attack (Room 6 Encounter).
    /// 1. Displays intersecting laser line telegraphs across the arena (1.0s warning).
    /// 2. Fires high-energy grid laser beams dealing damage to the player if intersected.
    /// 3. Recovers and returns to reposition.
    /// </summary>
    public class BossGridLaserState : EnemyState
    {
        private enum Step { Telegraph, Firing, Recover }

        private readonly ArchitectBoss architect;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private Step currentStep;
        private float stepStartTime;
        private bool hasDealtDamage;

        private const float TelegraphDuration = 1.0f;
        private const float FiringDuration = 0.8f;
        private const float LaserDamage = 20f;

        private readonly Vector3[] gridCenters = new Vector3[4];
        private readonly Vector2[] gridDirections = new Vector2[4];

        public BossGridLaserState(ArchitectBoss architect, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
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
            currentStep = Step.Telegraph;
            stepStartTime = Time.time;
            hasDealtDamage = false;

            CalculateGridLines();

            // Display grid line telegraphs
            if (indicator != null)
            {
                indicator.ShowLine(gridCenters[0], gridDirections[0], 28f, 0.25f);
            }
        }

        private void CalculateGridLines()
        {
            Vector3 center = architect.transform.position;
            // 2 Horizontal lines (above & below)
            gridCenters[0] = center + new Vector3(-14f, 2.5f, 0f);
            gridDirections[0] = Vector2.right;

            gridCenters[1] = center + new Vector3(-14f, -2.5f, 0f);
            gridDirections[1] = Vector2.right;

            // 2 Vertical lines (left & right)
            gridCenters[2] = center + new Vector3(2.5f, -14f, 0f);
            gridDirections[2] = Vector2.up;

            gridCenters[3] = center + new Vector3(-2.5f, -14f, 0f);
            gridDirections[3] = Vector2.up;
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
                        currentStep = Step.Firing;
                        stepStartTime = Time.time;
                        if (indicator != null) indicator.Hide();
                    }
                    break;

                case Step.Firing:
                    if (!hasDealtDamage)
                    {
                        CheckPlayerGridIntersection();
                    }

                    if (elapsed >= FiringDuration)
                    {
                        currentStep = Step.Recover;
                        stepStartTime = Time.time;
                    }
                    break;

                case Step.Recover:
                    float recoverTime = (architect.Data != null ? architect.Data.RecoverDuration : 1.2f) * architect.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void CheckPlayerGridIntersection()
        {
            if (architect.Target == null) return;

            Vector2 playerPos = architect.Target.position;
            Vector2 bossPos = architect.transform.position;

            // Check if player is near any of the grid lines (within 1.2m of +/- 2.5m lines)
            bool hitH1 = Mathf.Abs(playerPos.y - (bossPos.y + 2.5f)) <= 1.0f;
            bool hitH2 = Mathf.Abs(playerPos.y - (bossPos.y - 2.5f)) <= 1.0f;
            bool hitV1 = Mathf.Abs(playerPos.x - (bossPos.x + 2.5f)) <= 1.0f;
            bool hitV2 = Mathf.Abs(playerPos.x - (bossPos.x - 2.5f)) <= 1.0f;

            if (hitH1 || hitH2 || hitV1 || hitV2)
            {
                IDamageable dmg = architect.Target.GetComponentInChildren<IDamageable>() ?? architect.Target.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    float totalDamage = LaserDamage * architect.DifficultyMultiplier;
                    dmg.TakeDamage(totalDamage, Vector2.zero);
                    hasDealtDamage = true;
                    Debug.Log($"[{architect.name}] Grid Laser hit Player for {totalDamage} damage!");
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
            if (indicator != null) indicator.Hide();
        }
    }
}
