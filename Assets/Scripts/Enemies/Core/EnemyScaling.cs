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
            return 1f + 0.25f * (room - 1);
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

            float hpMult = GetHPMultiplier(currentRoom);
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
