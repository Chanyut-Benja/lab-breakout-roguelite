using Enemies.Core;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Guard / Armored Guard alias (Balance Sheet Section 8).
    /// Base HP: 120, Damage Reduction: 20%, Drop EXP: 2.
    /// </summary>
    public class GuardEnemy : ArmoredGuardEnemy
    {
    }

    public class Guard : GuardEnemy { }
}

public class Guard : Enemies.Types.GuardEnemy { }
public class GuardEnemy : Enemies.Types.GuardEnemy { }
