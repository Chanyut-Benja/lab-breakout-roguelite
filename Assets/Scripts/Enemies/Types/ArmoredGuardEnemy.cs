using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Armored Guard (Balance Sheet Section 8):
    /// Heavy frontline tank appearing from Room 4 onwards.
    /// Base HP: 120, Damage Reduction: minimum 20%, Knockback Resistance: 85%, Drop EXP: 2.
    /// </summary>
    public class ArmoredGuardEnemy : EnemyBase
    {
        private SpawnState spawnState;
        private ChaseState chaseState;

        [Header("Armored Guard Base Stats (Balance Sheet)")]
        [SerializeField] private float baseGuardHP = 120f;
        [SerializeField] private float baseGuardSpeed = 1.2f;
        [SerializeField] private float baseGuardDamage = 8f;
        [Range(0f, 1f)]
        [SerializeField] private float baseKnockbackResistance = 0.85f;
        [Range(0f, 1f)]
        [SerializeField] private float damageReductionPercent = 0.20f; // ลดดาเมจขั้นต่ำ 20%
        [SerializeField] private int baseExpDrop = 2;

        public float BaseGuardHP => baseGuardHP;
        public float BaseGuardSpeed => baseGuardSpeed;
        public float BaseGuardDamage => baseGuardDamage;
        public float KnockbackResistance => data != null ? Mathf.Max(data.KnockbackResistance, baseKnockbackResistance) : baseKnockbackResistance;
        public float DamageReductionPercent => damageReductionPercent;

        protected override void Awake()
        {
            base.Awake();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.35f, 0.55f, 0.80f, 1f); // Reinforced Steel Armor
                transform.localScale = Vector3.one * 1.35f;
            }
        }

        protected override void InitializeStates()
        {
            chaseState = new ChaseState(this, stateMachine);
            spawnState = new SpawnState(this, stateMachine, chaseState, 0.25f);
            deadState = new DeadState(this, stateMachine, 0.12f);
        }

        protected override void StartInitialState()
        {
            if (stateMachine != null && spawnState != null)
            {
                stateMachine.Initialize(spawnState);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (IsInitialized && stateMachine != null && spawnState != null)
            {
                stateMachine.ChangeState(spawnState);
            }
        }

        public override void Init(Transform target, float hpMultiplier, float damageMultiplier, float speedMultiplier)
        {
            base.Init(target, hpMultiplier, damageMultiplier, speedMultiplier);

            if (health != null)
            {
                health.SetExactMaxHealth(baseGuardHP * hpMultiplier);
                health.SetExpDrop(baseExpDrop);
                health.SetDamageReduction(damageReductionPercent); // ลดดาเมจขั้นต่ำ 20%
            }
        }
    }

    public class ArmoredGuard : ArmoredGuardEnemy { }
}

public class ArmoredGuard : Enemies.Types.ArmoredGuardEnemy { }
public class ArmoredGuardEnemy : Enemies.Types.ArmoredGuardEnemy { }
