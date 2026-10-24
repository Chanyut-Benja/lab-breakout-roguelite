using Enemies.Core;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Specialized ScriptableObject for Boss entities.
    /// Extends EnemyData with phase thresholds, modular attack parameters, minion summoning caps, and boss audio.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBossData", menuName = "Roguelite/Boss Data")]
    public class BossData : EnemyData
    {
        [Header("Boss Identity")]
        [SerializeField] private string bossTitle = "The Brute";
        [SerializeField] private int bossRoomNumber = 3;

        [Header("Phases & Enrage (Balance 8.3)")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float phase2HPThreshold = 0.66f;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float phase3HPThreshold = 0.33f;
        [SerializeField] private float phase2SpeedMultiplier = 1.20f; // +20% Move Speed in Phase 2
        [SerializeField] private float phase3SpeedMultiplier = 1.35f; // +35% Move Speed in Phase 3 (Enrage)
        [SerializeField] private float enrageTimeLimitSeconds = 180f; // Auto-Enrage if boss fight exceeds 3 minutes (180s)
        [SerializeField] private float phaseTransitionDuration = 1.5f;
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float attackDecisionCooldown = 1.2f;

        [Header("Pacing & Skill Frequency")]
        [Tooltip("Minimum walking/reposition delay between attacks (lower = boss attacks much faster)")]
        [SerializeField] private float minRepositionDuration = 0.5f;
        [Tooltip("Maximum walking/reposition delay between attacks")]
        [SerializeField] private float maxRepositionDuration = 1.5f;

        [Header("Modular Attack: Slam")]
        [SerializeField] private float slamRadius = 3.2f;
        [SerializeField] private float slamDamage = 25f;
        [SerializeField] private float slamTelegraphDuration = 0.8f;
        [SerializeField] private float slamKnockback = 10f;
        [SerializeField] private GameObject slamShockwaveVFX;

        [Header("Modular Attack: Charge")]
        [SerializeField] private float chargeSpeed = 12f;
        [SerializeField] private float chargeDuration = 0.5f;
        [SerializeField] private float chargeDamage = 30f;
        [SerializeField] private float chargeTelegraphDuration = 0.7f;
        [SerializeField] private float chargeKnockback = 12f;

        [Header("Modular Attack: Radial / Fan Shot")]
        [SerializeField] private int projectileCount = 8;
        [SerializeField] private float fanSpreadAngle = 90f;
        [SerializeField] private bool fullRadial360 = false;
        [SerializeField] private float radialTelegraphDuration = 0.7f;

        [Header("Modular Attack: Summon Minions")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private EnemyData minionData;
        [SerializeField] private int minionSpawnCount = 4;
        [SerializeField] private int maxActiveMinionsCap = 6;
        [SerializeField] private float summonTelegraphDuration = 0.8f;

        [Header("Modular Attack: Burrow & Emerge")]
        [SerializeField] private float bossBurrowDuration = 2.5f;
        [SerializeField] private float bossEmergeRadius = 2.5f;
        [SerializeField] private float bossEmergeDamage = 20f;
        [SerializeField] private float bossEmergeTelegraphDuration = 0.8f;

        [Header("Boss Audio & Visuals")]
        [SerializeField] private AudioClip roarSFX;
        [SerializeField] private AudioClip phaseTransitionSFX;
        [SerializeField] private GameObject phaseTransitionVFX;
        [SerializeField] private AudioClip slamSFX;
        [SerializeField] private AudioClip summonSFX;

        // Public Properties
        public string BossTitle => bossTitle;
        public int BossRoomNumber => bossRoomNumber;

        public float Phase2HPThreshold => phase2HPThreshold;
        public float Phase3HPThreshold => phase3HPThreshold;
        public float Phase2SpeedMultiplier => phase2SpeedMultiplier;
        public float Phase3SpeedMultiplier => phase3SpeedMultiplier;
        public float EnrageTimeLimitSeconds => enrageTimeLimitSeconds;
        public float PhaseTransitionDuration => phaseTransitionDuration;
        public float IntroDuration => introDuration;
        public float AttackDecisionCooldown => attackDecisionCooldown;
        public float MinRepositionDuration => minRepositionDuration;
        public float MaxRepositionDuration => maxRepositionDuration;

        /// <summary>
        /// คืนค่า Max HP พื้นฐานของบอสตามลำดับห้องในเอกสารบาลานซ์หัวข้อ 8.3
        /// ห้อง 3: Brute (12,000), ห้อง 6: Architect (30,000), ห้อง 9: Doctor (60,000),
        /// ห้อง 12: Digger (80,000), ห้อง 15: Stormcaller (110,000), ห้อง 18: Overlord (160,000)
        /// </summary>
        public static float GetBalancedMaxHPByRoom(int roomNumber)
        {
            return roomNumber switch
            {
                3 => 12000f,
                6 => 30000f,
                9 => 60000f,
                12 => 80000f,
                15 => 110000f,
                18 => 160000f,
                _ => 12000f
            };
        }

        public float SlamRadius => slamRadius;
        public float SlamDamage => slamDamage;
        public float SlamTelegraphDuration => slamTelegraphDuration;
        public float SlamKnockback => slamKnockback;
        public GameObject SlamShockwaveVFX => slamShockwaveVFX;

        public float ChargeSpeed => chargeSpeed;
        public float ChargeDuration => chargeDuration;
        public float ChargeDamage => chargeDamage;
        public float ChargeTelegraphDuration => chargeTelegraphDuration;
        public float ChargeKnockback => chargeKnockback;

        public int ProjectileCount => projectileCount;
        public float FanSpreadAngle => fanSpreadAngle;
        public bool FullRadial360 => fullRadial360;
        public float RadialTelegraphDuration => radialTelegraphDuration;

        public GameObject MinionPrefab => minionPrefab;
        public EnemyData MinionData => minionData;
        public int MinionSpawnCount => minionSpawnCount;
        public int MaxActiveMinionsCap => maxActiveMinionsCap;
        public float SummonTelegraphDuration => summonTelegraphDuration;

        public float BossBurrowDuration => bossBurrowDuration;
        public float BossEmergeRadius => bossEmergeRadius;
        public float BossEmergeDamage => bossEmergeDamage;
        public float BossEmergeTelegraphDuration => bossEmergeTelegraphDuration;

        public AudioClip RoarSFX => roarSFX;
        public AudioClip PhaseTransitionSFX => phaseTransitionSFX;
        public GameObject PhaseTransitionVFX => phaseTransitionVFX;
        public AudioClip SlamSFX => slamSFX;
        public AudioClip SummonSFX => summonSFX;
    }
}
