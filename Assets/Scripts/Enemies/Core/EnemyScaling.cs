using UnityEngine;

namespace Enemies.Core
{
    /// <summary>
    /// Centralized enemy scaling calculations based on Balance Sheet Section 8:
    /// - HP Multiplier = 1 + 0.25 * (currentRoom - 1)
    /// - Damage Multiplier = 1 + 0.08 * (currentRoom - 1)
    /// - Speed Bonus = Mathf.Min(0.02f * (currentRoom - 1), 0.20f)
    /// - Speed Multiplier = 1 + Speed Bonus
    /// Applied to the base stats of every spawned enemy in the room.
    /// </summary>
    public static class EnemyScaling
    {
        public static float GetHPMultiplier(int currentRoom)
        {
            int room = Mathf.Max(1, currentRoom);
            return 1.5f * (1f + 0.40f * (room - 1));   // tougher: late rooms no longer melt
        }

        /// <summary>Number of evolved weapons / resonant boons the player holds right now.</summary>
        public static void CountPlayerPower(out int evolved, out int resonant)
        {
            evolved = 0; resonant = 0;
            PlayerInventory inv = PlayerInventory.Instance;
            if (inv == null) return;
            for (int i = 0; i < inv.Weapons.Count; i++) if (inv.Weapons[i] != null && inv.Weapons[i].isEvolved) evolved++;
            for (int i = 0; i < inv.Boons.Count; i++) if (inv.Boons[i] != null && inv.Boons[i].isResonant) resonant++;
        }

        /// <summary>Extra enemy HP once the player has evolved weapons / resonant boons (+120% per evolution, +60% per resonance).</summary>
        public static float GetPowerHPMultiplier()
        {
            CountPlayerPower(out int evo, out int reso);
            return 1f + 1.20f * evo + 0.60f * reso;
        }

        /// <summary>Spawn-rate multiplier (+80% per evolution, +50% per resonance, max x4).</summary>
        public static float GetPowerSpawnMultiplier()
        {
            CountPlayerPower(out int evo, out int reso);
            return Mathf.Min(4f, 1f + 0.80f * evo + 0.50f * reso);
        }

        /// <summary>On-screen enemy cap multiplier (+40% per evolution, +25% per resonance, max x2.5).</summary>
        public static float GetPowerCapMultiplier()
        {
            CountPlayerPower(out int evo, out int reso);
            return Mathf.Min(2.5f, 1f + 0.40f * evo + 0.25f * reso);
        }

        public static float GetDamageMultiplier(int currentRoom)
        {
            int room = Mathf.Max(1, currentRoom);
            return 1f + 0.08f * (room - 1);
        }

        public static float GetSpeedBonus(int currentRoom)
        {
            int room = Mathf.Max(1, currentRoom);
            return Mathf.Min(0.02f * (room - 1), 0.20f);
        }

        public static float GetSpeedMultiplier(int currentRoom)
        {
            return 1f + GetSpeedBonus(currentRoom);
        }

        public static int GetMaxCap(int currentRoom)
        {
            int room = Mathf.Max(1, currentRoom);
            return Mathf.RoundToInt(40f + 5.3f * (room - 1));
        }

        /// <summary>
        /// คำนวณและนำตัวคูณสเกล (HP, Damage, Speed) ไปปรับใช้กับศัตรูที่ถูกเสกออกมา
        /// </summary>
        public static void ApplyScaling(GameObject enemyObj, int currentRoom, Transform playerTarget = null)
        {
            if (enemyObj == null) return;

            if (enemyObj.GetComponent<Enemies.Bosses.BossBase>() == null)
            {
                SkinLibrary.ApplyEnemy(enemyObj);
                // the player (and dashes) can walk through ordinary enemies; contact damage still works via trigger callbacks
                foreach (Collider2D c in enemyObj.GetComponentsInChildren<Collider2D>(true)) c.isTrigger = true;
            }

            float hpMult = GetHPMultiplier(currentRoom);
            if (enemyObj.GetComponent<Enemies.Bosses.BossBase>() == null) hpMult *= GetPowerHPMultiplier();
            float dmgMult = GetDamageMultiplier(currentRoom);
            float spdMult = GetSpeedMultiplier(currentRoom);

            EnemyBase eb = enemyObj.GetComponent<EnemyBase>();
            if (eb != null)
            {
                eb.Init(playerTarget, hpMult, dmgMult, spdMult);
                return;
            }

            EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.SetScaledStats(hpMult, dmgMult, spdMult);
            }
        }
    }
}
