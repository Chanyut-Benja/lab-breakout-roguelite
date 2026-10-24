using Enemies.Core;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Spitter / Sentinel Bot alias (Balance Sheet Section 8).
    /// Base HP: 35, Drop EXP: 1.
    /// </summary>
    public class SpitterEnemy : SentinelBotEnemy
    {
        public GameObject GetAcidProjectilePrefab() => GetProjectilePrefab();
    }

    public class Spitter : SpitterEnemy { }
}

public class Spitter : Enemies.Types.SpitterEnemy { }
public class SpitterEnemy : Enemies.Types.SpitterEnemy { }
