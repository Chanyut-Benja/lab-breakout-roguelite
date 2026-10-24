using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Types
{
    /// <summary>
    /// Crawler / Mutant Crawler alias (Balance Sheet Section 8).
    /// Base HP: 15, Speed: 3.8, Drop EXP: 1.
    /// </summary>
    public class CrawlerEnemy : MutantCrawlerEnemy
    {
    }

    public class Crawler : CrawlerEnemy { }
}

public class Crawler : Enemies.Types.CrawlerEnemy { }
public class CrawlerEnemy : Enemies.Types.CrawlerEnemy { }
