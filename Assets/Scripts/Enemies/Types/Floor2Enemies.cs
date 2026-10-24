using Combat;
using Enemies.Core;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Floor 2 Enemies (Rooms 10 - 18):
    /// Shares identical structural base stats with Floor 1 archetypes as specified in Balance Sheet Section 8:
    /// - Shield Drone: HP 20, Drop EXP 1 (Shield variant of Scrap Drone)
    /// - Repair Bot: HP 25, Drop EXP 1 (Healer bot)
    /// - Laser Turret: HP 35, Drop EXP 1 (Artillery variant of Sentinel Bot)
    /// - Phase Stalker: HP 15, Drop EXP 1 (Teleporting/fast variant of Mutant Crawler)
    /// - Siege Walker: HP 1,500, Drop EXP 20 (Mini-Boss for Floor 2 rooms 10, 11, 13, 14, 16, 17)
    /// </summary>
    public class ShieldDroneEnemy : ScrapDroneEnemy { }
    public class ShieldDrone : ShieldDroneEnemy { }

    public class RepairBotEnemy : ScrapDroneEnemy
    {
        [SerializeField] private float healRadius = 5.0f;
        [SerializeField] private float healAmountPerSec = 4.0f;
        private float nextHealTime = 0f;

        protected override void Awake()
        {
            base.Awake();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.3f, 1f, 0.7f, 1f); // Medical Cyan
            }
        }

        protected override void Update()
        {
            base.Update();
            if (Time.time >= nextHealTime)
            {
                nextHealTime = Time.time + 1.0f;
                HealNearbyAllies();
            }
        }

        private void HealNearbyAllies()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, healRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].CompareTag("Enemy") && hits[i].gameObject != gameObject)
                {
                    EnemyHealth eh = hits[i].GetComponent<EnemyHealth>();
                    if (eh != null && !eh.IsDead)
                    {
                        eh.Heal(healAmountPerSec);
                    }
                }
            }
        }
    }
    public class RepairBot : RepairBotEnemy { }

    public class LaserTurretEnemy : SentinelBotEnemy { }
    public class LaserTurret : LaserTurretEnemy { }

    public class PhaseStalkerEnemy : MutantCrawlerEnemy { }
    public class PhaseStalker : PhaseStalkerEnemy { }

    public class SiegeWalkerEnemy : BreachWardenEnemy { }
    public class SiegeWalker : SiegeWalkerEnemy { }
}

public class ShieldDrone : Enemies.Types.ShieldDroneEnemy { }
public class ShieldDroneEnemy : Enemies.Types.ShieldDroneEnemy { }
public class RepairBot : Enemies.Types.RepairBotEnemy { }
public class RepairBotEnemy : Enemies.Types.RepairBotEnemy { }
public class LaserTurret : Enemies.Types.LaserTurretEnemy { }
public class LaserTurretEnemy : Enemies.Types.LaserTurretEnemy { }
public class PhaseStalker : Enemies.Types.PhaseStalkerEnemy { }
public class PhaseStalkerEnemy : Enemies.Types.PhaseStalkerEnemy { }
public class SiegeWalker : Enemies.Types.SiegeWalkerEnemy { }
public class SiegeWalkerEnemy : Enemies.Types.SiegeWalkerEnemy { }
