using UnityEngine;

namespace Enemies.Core
{
    /// <summary>
    /// ScriptableObject defining all stats and parameters for an enemy type.
    /// Centralized data source so designers can tune balance without modifying code.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Roguelite/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string enemyName = "Walker";
        [SerializeField] private string enemyId = "walker";
        [SerializeField] private GameObject prefab;

        [Header("Health & Defense")]
        [SerializeField] private float maxHP = 10f;
        [Range(0f, 1f)]
        [SerializeField] private float knockbackResistance = 0f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float separationRadius = 0.8f;
        [SerializeField] private float separationWeight = 0.4f;

        [Header("Combat & Contact Damage")]
        [SerializeField] private float contactDamage = 5f;
        [SerializeField] private float contactDamageCooldown = 0.5f;
        [SerializeField] private float attackCooldown = 1.0f;
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private float detectRange = 15f;

        [Header("Telegraph & Attack Timing (General)")]
        [Tooltip("Telegraph duration must be >= 0.4 - 0.8s per game fairness spec")]
        [SerializeField] private float telegraphDuration = 0.6f;
        [SerializeField] private float recoverDuration = 1.0f;

        [Header("Variant: Dasher Parameters")]
        [SerializeField] private float dashSpeed = 9f;
        [SerializeField] private float dashDuration = 0.4f;
        [SerializeField] private float dashTriggerDistance = 5.0f;

        [Header("Variant: Ranged Parameters")]
        [SerializeField] private float preferredDistance = 6.0f;
        [SerializeField] private float kiteDistance = 3.0f;
        [SerializeField] private float projectileSpeed = 5.0f;
        [SerializeField] private float projectileDamage = 10.0f;
        [SerializeField] private GameObject projectilePrefab;

        [Header("Variant: Burrower Parameters")]
        [SerializeField] private float undergroundSpeed = 4.0f;
        [SerializeField] private float burrowDuration = 2.0f;
        [SerializeField] private float emergeTelegraphDuration = 0.8f;
        [SerializeField] private float emergeAoERadius = 1.2f;
        [SerializeField] private float emergeDamage = 10.0f;
        [SerializeField] private float exposedDuration = 1.5f;

        [Header("EXP & Rewards")]
        [SerializeField] private int expDrop = 5;
        [SerializeField] private GameObject expPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float creditDropRate = 0.35f;
        [SerializeField] private int creditDropAmount = 1;
        [SerializeField] private GameObject creditPrefab;

        [Header("Visual Effects (VFX)")]
        [SerializeField] private GameObject spawnVFX;
        [SerializeField] private GameObject deathVFX;
        [SerializeField] private GameObject telegraphVFX;
        [SerializeField] private GameObject hitVFX;

        [Header("Sound Effects (SFX)")]
        [SerializeField] private AudioClip spawnSFX;
        [SerializeField] private AudioClip hitSFX;
        [SerializeField] private AudioClip dieSFX;
        [SerializeField] private AudioClip telegraphSFX;
        [SerializeField] private AudioClip attackSFX;

        // Public Properties
        public string EnemyName => enemyName;
        public string EnemyId => enemyId;
        public GameObject Prefab => prefab;

        public float MaxHP => maxHP;
        public float KnockbackResistance => knockbackResistance;

        public float MoveSpeed => moveSpeed;
        public float SeparationRadius => separationRadius;
        public float SeparationWeight => separationWeight;

        public float ContactDamage => contactDamage;
        public float ContactDamageCooldown => contactDamageCooldown;
        public float AttackCooldown => attackCooldown;
        public float AttackRange => attackRange;
        public float DetectRange => detectRange;

        public float TelegraphDuration => telegraphDuration;
        public float RecoverDuration => recoverDuration;

        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashTriggerDistance => dashTriggerDistance;

        public float PreferredDistance => preferredDistance;
        public float KiteDistance => kiteDistance;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileDamage => projectileDamage;
        public GameObject ProjectilePrefab => projectilePrefab;

        public float UndergroundSpeed => undergroundSpeed;
        public float BurrowDuration => burrowDuration;
        public float EmergeTelegraphDuration => emergeTelegraphDuration;
        public float EmergeAoERadius => emergeAoERadius;
        public float EmergeDamage => emergeDamage;
        public float ExposedDuration => exposedDuration;

        public int ExpDrop => expDrop;
        public GameObject ExpPrefab => expPrefab;
        public float CreditDropRate => creditDropRate;
        public int CreditDropAmount => creditDropAmount;
        public GameObject CreditPrefab => creditPrefab;

        public GameObject SpawnVFX => spawnVFX;
        public GameObject DeathVFX => deathVFX;
        public GameObject TelegraphVFX => telegraphVFX;
        public GameObject HitVFX => hitVFX;

        public AudioClip SpawnSFX => spawnSFX;
        public AudioClip HitSFX => hitSFX;
        public AudioClip DieSFX => dieSFX;
        public AudioClip TelegraphSFX => telegraphSFX;
        public AudioClip AttackSFX => attackSFX;
    }
}
