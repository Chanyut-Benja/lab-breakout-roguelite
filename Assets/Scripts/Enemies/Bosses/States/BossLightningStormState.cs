using System.Collections.Generic;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses.States
{
    /// <summary>
    /// The Stormcaller Signature Attack: LightningStorm (พายุสายฟ้า / Thunder Strikes).
    /// Summons 4-6 target danger zones on the floor targeting the player.
    /// After a clear telegraph warning, lightning bolts strike the ground dealing explosive AoE damage.
    /// </summary>
    public class BossLightningStormState : EnemyState
    {
        private enum StormStep { Telegraph, Strike, Recover }

        private readonly BossBase boss;
        private readonly TelegraphIndicator indicator;
        private readonly IState nextState;

        private StormStep currentStep;
        private float stepStartTime;
        private readonly List<Vector3> strikePositions = new List<Vector3>();

        private const float StrikeRadius = 2.0f;
        private const float TelegraphDuration = 0.75f;

        public BossLightningStormState(BossBase boss, StateMachine stateMachine, TelegraphIndicator indicator, IState nextState)
            : base(boss, stateMachine)
        {
            this.boss = boss;
            this.indicator = indicator;
            this.nextState = nextState;
        }

        public override void Enter()
        {
            base.Enter();

            boss.StopMovement();
            currentStep = StormStep.Telegraph;
            stepStartTime = Time.time;
            strikePositions.Clear();

            int strikeCount = boss.CurrentPhase >= 2 ? 6 : 4;
            Vector3 centerPos = boss.Target != null ? boss.Target.position : boss.transform.position;

            // Pick randomized strike locations around player
            strikePositions.Add(centerPos); // One directly under player
            for (int i = 1; i < strikeCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * Random.Range(2.0f, 5.5f);
                strikePositions.Add(centerPos + new Vector3(randomOffset.x, randomOffset.y, 0f));
            }

            // Show primary telegraph circle on the direct target
            if (indicator != null)
            {
                indicator.ShowCircle(centerPos, StrikeRadius, 28, 0.25f);
            }

            if (data != null && data.TelegraphSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.TelegraphSFX);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Telegraph");
            }
        }

        public override void Tick()
        {
            base.Tick();

            float elapsed = Time.time - stepStartTime;

            switch (currentStep)
            {
                case StormStep.Telegraph:
                    if (elapsed >= TelegraphDuration)
                    {
                        if (indicator != null) indicator.Hide();

                        currentStep = StormStep.Strike;
                        stepStartTime = Time.time;
                        ExecuteLightningStrikes();
                    }
                    break;

                case StormStep.Strike:
                    if (elapsed >= 0.25f)
                    {
                        currentStep = StormStep.Recover;
                        stepStartTime = Time.time;

                        if (boss.Animator != null)
                        {
                            boss.Animator.SetBool("IsMoving", false);
                            boss.Animator.SetTrigger("Recover");
                        }
                    }
                    break;

                case StormStep.Recover:
                    float recoverTime = 1.0f * boss.RecoverDurationMultiplier;
                    if (elapsed >= recoverTime)
                    {
                        stateMachine.ChangeState(nextState);
                    }
                    break;
            }
        }

        private void ExecuteLightningStrikes()
        {
            float damage = (boss.Data != null ? boss.Data.ContactDamage * 1.5f : 25f) * boss.DifficultyMultiplier;

            if (boss.Animator != null) boss.Animator.SetTrigger("Attack");
            if (data != null && data.AttackSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(data.AttackSFX);
            }

            for (int i = 0; i < strikePositions.Count; i++)
            {
                Vector3 strikePos = strikePositions[i];

                // Deal damage to player if within strike radius
                if (boss.Target != null)
                {
                    float dist = Vector2.Distance(strikePos, boss.Target.position);
                    if (dist <= StrikeRadius)
                    {
                        IDamageable dmg = boss.Target.GetComponentInChildren<IDamageable>() ?? boss.Target.GetComponentInParent<IDamageable>();
                        if (dmg != null)
                        {
                            dmg.TakeDamage(damage, Vector2.up * 8f);
                            Debug.Log($"[{boss.name}] Lightning struck Player for {damage} damage!");
                        }
                    }
                }

                // Overlap query
                Collider2D[] cols = Physics2D.OverlapCircleAll(strikePos, StrikeRadius);
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] != null && cols[c].CompareTag("Player") && cols[c].transform != boss.Target)
                    {
                        IDamageable dmg = cols[c].GetComponent<IDamageable>() ?? cols[c].GetComponentInParent<IDamageable>();
                        if (dmg != null) dmg.TakeDamage(damage, Vector2.up * 8f);
                    }
                }
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (indicator != null) indicator.Hide();
            boss.StopMovement();
        }
    }
}
