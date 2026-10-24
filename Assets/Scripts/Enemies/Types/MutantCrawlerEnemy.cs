using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Mutant Crawler (Balance Sheet Section 8):
    /// Fast swarm chaser appearing from Room 1 onwards.
    /// Base HP: 15, Fast Speed (3.8m/s), Drop EXP: 1.
    /// </summary>
    public class MutantCrawlerEnemy : EnemyBase
    {
        private SpawnState spawnState;
        private ChaseState chaseState;

        [Header("Mutant Crawler Base Stats (Balance Sheet)")]
        [SerializeField] private float baseCrawlerHP = 15f;
        [SerializeField] private float baseCrawlerSpeed = 3.8f;
        [SerializeField] private float baseCrawlerDamage = 4f;
        [SerializeField] private int baseExpDrop = 1;

        public float BaseCrawlerHP => baseCrawlerHP;
        public float BaseCrawlerSpeed => baseCrawlerSpeed;
        public float BaseCrawlerDamage => baseCrawlerDamage;

        protected override void Awake()
        {
            base.Awake();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 0.45f, 0.35f, 1f); // Fast Orange-Red
                transform.localScale = Vector3.one * 0.9f;
            }
        }

        protected override void InitializeStates()
        {
            chaseState = new ChaseState(this, stateMachine);
            spawnState = new SpawnState(this, stateMachine, chaseState, 0.15f);
            deadState = new DeadState(this, stateMachine, 0.08f);
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
                health.SetExactMaxHealth(baseCrawlerHP * hpMultiplier);
                health.SetExpDrop(baseExpDrop);
            }
        }
    }

    public class MutantCrawler : MutantCrawlerEnemy { }
}

public class MutantCrawler : Enemies.Types.MutantCrawlerEnemy { }
public class MutantCrawlerEnemy : Enemies.Types.MutantCrawlerEnemy { }
