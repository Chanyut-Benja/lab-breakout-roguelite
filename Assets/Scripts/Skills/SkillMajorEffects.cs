using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enemies.Core;

/// <summary>
/// Runtime behaviour of the character passives and the Major skill-tree nodes (GDD section 9).
/// Attached to the Player by PlayerStats. Every Major effect only runs when its node is unlocked (SkillTreeRuntime.Has).
///   N-01 passive: Kinetic Absorption   N-02 passive: Mind Shock   N-03 passive: Overclock Dash (see PlayerController)
/// </summary>
public class SkillMajorEffects : MonoBehaviour
{
    public static SkillMajorEffects Instance { get; private set; }

    private PlayerHealth health;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    // movement based effects
    private Vector2 lastPos;
    private Vector2 moveDir = Vector2.right;
    private float stillTime;
    private float momentum;          // Juggernaut: 0 .. 0.20
    private float distAcc;
    private float overdrive;         // Overclock Overdrive: layers of 5%
    private float overdriveDecay;
    private float windTimer;

    // psionic barrier
    private float shield;
    private float shieldExpire;
    private float psionicReadyAt;   // Psionic Barrier: emergency heal + shield, 40s cooldown
    private const float PsionicCooldown = 40f;
    private const float PsionicHealPct = 0.15f;
    private const float PsionicShieldPct = 0.10f;

    // cooldowns / counters
    private int attackCount;
    private int kills;
    private float novaCd, waveCd, bulletCd, absoluteCd, dodgeCd;
    private bool guaranteedCrit;

    // persistent auras
    private GameObject shieldAura;
    private GameObject overdriveAura;

    // Mind Shock bookkeeping
    private readonly Dictionary<int, float> nextStun = new Dictionary<int, float>();
    private struct CritRecord { public float value; public float time; }
    private readonly CritRecord[] crits = new CritRecord[32];
    private int critIndex;

    public float DamageBonus
    {
        get { return SkillTreeRuntime.Has(SkillEffect.JuggernautMomentum) ? momentum : 0f; }
    }

    public float AttackSpeedBonus
    {
        get { return SkillTreeRuntime.Has(SkillEffect.OverclockOverdrive) ? overdrive : 0f; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        health = GetComponent<PlayerHealth>();
        lastPos = transform.position;

        Enemies.Bosses.BossBase.OnBossDied += HandleBossDied;
        EnemyHealth.OnEnemyDied += HandleEnemyDied;
        EnemyHealth.OnEnemyDamaged += HandleEnemyDamaged;   // Psionic Barrier charges from damage dealt
        EnemyHealth.OnAnyEnemyDiedWithPoisonState += HandlePoisonedDeath;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Enemies.Bosses.BossBase.OnBossDied -= HandleBossDied;
        EnemyHealth.OnEnemyDied -= HandleEnemyDied;
        EnemyHealth.OnEnemyDamaged -= HandleEnemyDamaged;
        EnemyHealth.OnAnyEnemyDiedWithPoisonState -= HandlePoisonedDeath;
        if (shieldAura != null) Destroy(shieldAura);
        if (overdriveAura != null) Destroy(overdriveAura);
    }

    private void Update()
    {
        Vector2 p = transform.position;
        Vector2 delta = p - lastPos;
        float d = delta.magnitude;
        lastPos = p;

        if (d > 0.002f)
        {
            stillTime = 0f;
            overdriveDecay = 0f;
            moveDir = delta / d;

            // Juggernaut: +4% damage per second of continuous movement, up to +20% (5s)
            if (SkillTreeRuntime.Has(SkillEffect.JuggernautMomentum))
                momentum = Mathf.Min(0.20f, momentum + 0.04f * Time.deltaTime);

            // Overclock Overdrive: +5% attack speed per 10m moved, up to +40%
            if (SkillTreeRuntime.Has(SkillEffect.OverclockOverdrive))
            {
                distAcc += d;
                while (distAcc >= 10f)
                {
                    distAcc -= 10f;
                    overdrive = Mathf.Min(0.40f, overdrive + 0.05f);
                }
            }
        }
        else
        {
            stillTime += Time.deltaTime;
            // Juggernaut: after standing still for 0.5s the bonus decays 10% per second
            if (stillTime > 0.5f) momentum = Mathf.Max(0f, momentum - 0.10f * Time.deltaTime);
            // Overdrive: lose one layer (5%) every 2 seconds while standing still
            overdriveDecay += Time.deltaTime;
            if (overdriveDecay >= 2f)
            {
                overdriveDecay = 0f;
                overdrive = Mathf.Max(0f, overdrive - 0.05f);
            }
        }

        if (shield > 0f && Time.time > shieldExpire) shield = 0f;

        UpdateVisuals();
    }

    // ------------------------------------------------------------- visuals
    private void UpdateVisuals()
    {
        // Juggernaut: white wind lines streaming behind the player while the bonus is active
        if (momentum > 0.06f && Time.time > windTimer)
        {
            windTimer = Time.time + Mathf.Lerp(0.12f, 0.04f, momentum / 0.20f);
            Vector2 side = new Vector2(-moveDir.y, moveDir.x) * Random.Range(-0.5f, 0.5f);
            Vector2 pos = (Vector2)transform.position - moveDir * 0.5f + side;
            WeaponVfx.Particle(pos, -moveDir * 3f, new Color(1f, 1f, 1f, 0.55f), 0.25f, 0.18f, 2f, 0f, WeaponVfx.Circle);
        }

        // Psionic Barrier: violet energy shell
        bool shellOn = shield > 0f;
        if (shellOn && shieldAura == null) shieldAura = MakeAura("PsionicShell", new Color(0.7f, 0.4f, 1f, 0.35f), 2.6f);
        if (shieldAura != null)
        {
            shieldAura.SetActive(shellOn);
            if (shellOn)
            {
                float k = health != null ? Mathf.Clamp01(shield / (health.MaxHealth * PsionicShieldPct)) : 1f;
                SetAura(shieldAura, new Color(0.7f, 0.4f, 1f, 0.2f + 0.3f * k), 2.4f + 0.2f * Mathf.Sin(Time.time * 6f));
            }
        }

        // Overclock Overdrive: orange aura that grows with the layers
        bool driveOn = overdrive > 0.01f;
        if (driveOn && overdriveAura == null) overdriveAura = MakeAura("OverdriveAura", new Color(1f, 0.55f, 0.15f, 0.3f), 2f);
        if (overdriveAura != null)
        {
            overdriveAura.SetActive(driveOn);
            if (driveOn) SetAura(overdriveAura, new Color(1f, 0.55f, 0.15f, 0.15f + overdrive), 1.6f + overdrive * 2f);
        }
    }

    private GameObject MakeAura(string name, Color color, float scale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WeaponVfx.Glow;
        sr.color = color;
        sr.sortingOrder = 12;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static void SetAura(GameObject go, Color color, float scale)
    {
        go.GetComponent<SpriteRenderer>().color = color;
        go.transform.localScale = Vector3.one * scale;
    }

    /// <summary>Temporary glow around the player (optionally blinking).</summary>
    private void ShowAura(Color color, float seconds, float scale, bool blink)
    {
        StartCoroutine(AuraRoutine(color, seconds, scale, blink));
    }

    private IEnumerator AuraRoutine(Color color, float seconds, float scale, bool blink)
    {
        GameObject aura = MakeAura("TempAura", color, scale);
        SpriteRenderer sr = aura.GetComponent<SpriteRenderer>();
        float t = 0f;
        while (t < seconds && aura != null)
        {
            t += Time.deltaTime;
            float a = color.a;
            // blinks faster in the last second
            if (blink) a *= (seconds - t < 1f ? 0.5f + 0.5f * Mathf.Sin(t * 30f) : 0.75f + 0.25f * Mathf.Sin(t * 8f));
            sr.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        if (aura != null) Destroy(aura);
    }

    public static void FloatText(string text, Vector2 pos, Color color)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject go = new GameObject("FloatText");
        go.transform.position = pos;
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = color;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (font != null) mr.sharedMaterial = font.material;
        mr.sortingOrder = 60;
        go.AddComponent<SkillFloatText>();
    }

    private void ScreenTint(Color color, float seconds)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        GameObject go = WeaponVfx.Spawn(WeaponVfx.Circle, cam.transform.position, 0f, color, 70f, 70f, seconds);
        if (go != null) go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
    }

    // ------------------------------------------------------------- incoming damage
    /// <summary>Psionic Barrier soaks damage first. Returns what is left.</summary>
    public float CurrentShield => (shield > 0f && Time.time <= shieldExpire) ? shield : 0f;

    /// <summary>
    /// Psionic Barrier (N-02): when the player is hit while under 30% HP, heal 15% of max HP and gain a
    /// shield worth 10% of max HP (fades after 5s). 40s cooldown. Call BEFORE the hit's damage is applied.
    /// </summary>
    public void TryPsionicEmergency()
    {
        if (!SkillTreeRuntime.Has(SkillEffect.PsionicBarrier) || health == null) return;
        if (Time.time < psionicReadyAt) return;
        if (health.CurrentHealth <= 0f || health.CurrentHealth >= health.MaxHealth * 0.30f) return;
        psionicReadyAt = Time.time + PsionicCooldown;
        health.Heal(health.MaxHealth * PsionicHealPct);
        shield = health.MaxHealth * PsionicShieldPct;
        shieldExpire = Time.time + 5f;
        FloatText("PSIONIC BARRIER!", (Vector2)health.transform.position + Vector2.up * 1.6f, new Color(0.75f, 0.45f, 1f, 1f));
    }

    public float AbsorbShield(float amount)
    {
        if (shield <= 0f) return amount;
        float soaked = Mathf.Min(shield, amount);
        shield -= soaked;
        if (shield <= 0.01f) shield = 0f;
        return amount - soaked;
    }

    /// <summary>
    /// Absolute Armor: when a hit drops HP under 30% the player becomes invulnerable for 3s (90s cooldown).
    /// Returns true when it fired, so the caller can keep the player alive (1 HP) if the hit was lethal.
    /// </summary>
    public bool TryAbsoluteArmor(float hpAfterHit, float maxHp)
    {
        if (!SkillTreeRuntime.Has(SkillEffect.AbsoluteArmor)) return false;
        if (Time.time < absoluteCd || hpAfterHit >= maxHp * 0.30f) return false;
        absoluteCd = Time.time + 90f;
        if (health != null) health.GrantInvulnerability(3f);
        ShowAura(new Color(1f, 0.85f, 0.3f, 0.55f), 3f, 3f, true);   // golden bubble that blinks near the end
        WeaponVfx.Shockwave(transform.position, new Color(1f, 0.85f, 0.3f, 0.9f), 6f, 0.4f);
        return true;
    }

    /// <summary>The player really took damage: N-01 Kinetic Absorption and the Kinetic Reflect node.</summary>
    public void OnPlayerHit()
    {
        // Kinetic Reflect is handled by PlayerHealth (armor reflect); nothing extra here.
    }

    /// <summary>A hit was avoided by the dash invulnerability: Perfect Dodge (Lightning Reflexes).</summary>
    public void OnDodged()
    {
        if (Time.time < dodgeCd) return;
        dodgeCd = Time.time + 0.5f;
        if (SkillTreeRuntime.Has(SkillEffect.LightningReflexes) && Random.value < 0.15f)
        {
            guaranteedCrit = true;
            WeaponVfx.Burst(transform.position, new Color(1f, 0.95f, 0.3f, 1f), 14, 6f, 0.35f, 0.12f);   // yellow sparks
        }
    }

    /// <summary>Called by WeaponBase.RollDamage: the next attack is a guaranteed critical.</summary>
    public static bool ConsumeGuaranteedCrit()
    {
        if (Instance == null || !Instance.guaranteedCrit) return false;
        Instance.guaranteedCrit = false;
        return true;
    }

    // ------------------------------------------------------------- outgoing damage
    private void HandleEnemyDamaged(EnemyData data, float amount, Vector2 position)
    {
        OnDamageDealt(amount);
    }

    public void OnDamageDealt(float amount)
    {
        // Psionic Barrier no longer builds from damage dealt: see TryPsionicEmergency
    }

    // ---- N-02 passive: Mind Shock (a crit stuns enemies within 2m of the target for 1s, 2s cooldown per target)
    // Mind Shock (N-02 passive) is implemented in PlayerStats.TryApplyMindShockOnCrit.
    public static void RegisterCrit(float damage) { }
    public static void CheckCritHit(Collider2D col, float damage) { }

    /// <summary>Stun = frozen for a while (bosses are immune).</summary>
    public static void Stun(Collider2D c, float seconds)
    {
        CombatUtil.Slow(c, 1f, seconds);
    }

    /// <summary>Overcharge Nova: every 3rd attack (10s cooldown) a wave destroys enemy shots within 8m and hits bosses for 100.</summary>
    public static void RegisterAttack()
    {
        if (Instance != null) Instance.CountAttack();
    }

    private void CountAttack()
    {
        if (!SkillTreeRuntime.Has(SkillEffect.OverchargeNova)) return;
        if (Time.time < waveCd) return;
        attackCount++;
        Vector2 center = transform.position;

        if (attackCount < 3)
        {
            // charge rings: one and two small rings before the wave
            WeaponVfx.Spawn(WeaponVfx.Ring, center, 0f, new Color(0.75f, 0.45f, 1f, 0.8f), 0.4f + attackCount, 0.8f + attackCount * 0.5f, 0.25f);
            return;
        }
        attackCount = 0;
        waveCd = Time.time + 10f;

        EnemyProjectile[] shots = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i] == null) continue;
            if (((Vector2)shots[i].transform.position - center).sqrMagnitude <= 64f) Destroy(shots[i].gameObject);
        }

        float mult = CombatUtil.DamageMult;
        int n = CombatUtil.QueryEnemies(center, 8f, hits);
        for (int i = 0; i < n && i < hits.Count; i++)
        {
            if (CombatUtil.IsBoss(hits[i])) CombatUtil.DealDamage(hits[i], 100f * mult, Vector2.zero);
        }
        WeaponVfx.Spawn(WeaponVfx.Ring, center, 0f, new Color(0.75f, 0.45f, 1f, 0.9f), 1f, 16f, 0.45f);
    }

    /// <summary>Blast Nova: a Nano-Blade swing that hit 3+ enemies releases a shockwave (3s cooldown).</summary>
    public static void OnNanoBladeSwing(Vector2 origin, int hitCount)
    {
        if (Instance != null) Instance.BlastNova(origin, hitCount);
    }

    private void BlastNova(Vector2 origin, int hitCount)
    {
        if (!SkillTreeRuntime.Has(SkillEffect.BlastNova) || hitCount < 3 || Time.time < novaCd) return;
        novaCd = Time.time + 3f;

        float mult = CombatUtil.DamageMult;
        float area = CombatUtil.AreaMult;
        float radius = 4f * area;
        int n = CombatUtil.QueryEnemies(origin, radius, hits);
        for (int i = 0; i < n && i < hits.Count; i++)
        {
            Vector2 to = (Vector2)hits[i].bounds.center - origin;
            CombatUtil.DealDamage(hits[i], 60f * mult, to.normalized * 7.5f);
        }
        WeaponVfx.Shockwave(origin, new Color(1f, 0.6f, 0.3f, 0.9f), radius * 2.2f, 0.35f);
        WeaponVfx.Burst(origin, new Color(1f, 0.75f, 0.4f, 1f), 16, 8f, 0.4f, 0.2f);
    }

    // ------------------------------------------------------------- kills / poison
    private void HandleBossDied(Enemies.Bosses.BossData data)
    {
        Titan(0.30f);
    }

    public static void OnMiniBossKilled()
    {
        if (Instance != null) Instance.Titan(0.15f);
    }

    // Titan Revitalization: green healing particles and a floating "+N" number
    private void Titan(float fraction)
    {
        if (!SkillTreeRuntime.Has(SkillEffect.TitanRevitalization) || health == null) return;
        float amount = health.MaxHealth * fraction;
        health.Heal(amount);
        Vector2 pos = transform.position;
        WeaponVfx.Burst(pos, new Color(0.35f, 1f, 0.45f, 0.95f), 18, 3.5f, 0.7f, 0.2f);
        FloatText("+" + Mathf.RoundToInt(amount), pos + Vector2.up * 1f, new Color(0.4f, 1f, 0.5f, 1f));
    }

    private void HandleEnemyDied(EnemyData data, Vector2 position)
    {
        if (!SkillTreeRuntime.Has(SkillEffect.TemporalRift)) return;
        kills++;
        if (kills % 50 != 0) return;

        int n = CombatUtil.QueryEnemies(transform.position, 80f, hits);
        for (int i = 0; i < n && i < hits.Count; i++)
        {
            bool boss = CombatUtil.IsBoss(hits[i]);
            CombatUtil.Slow(hits[i], 1f, boss ? 0.75f : 1.5f);   // slow 100% = frozen
        }
        ScreenTint(new Color(0.45f, 0.35f, 0.6f, 0.28f), 1.5f);   // gray-purple tint
        WeaponVfx.Shockwave(transform.position, new Color(0.7f, 0.6f, 0.95f, 0.8f), 30f, 0.6f);
    }

    // Venom Catalyst: a poisoned enemy that dies passes its remaining poison to the 2 nearest enemies within 4m.
    // Does not stack with Epidemic Outbreak (Toxic Spore Resonant), which already spreads the poison.
    private void HandlePoisonedDeath(GameObject dead, Vector2 position, bool wasPoisoned)
    {
        if (!wasPoisoned || !SkillTreeRuntime.Has(SkillEffect.VenomCatalyst)) return;
        VenomCarrier from = dead != null ? dead.GetComponent<VenomCarrier>() : null;
        int chain = from != null ? from.chain : 0;
        if (chain >= 2) return;

        List<Collider2D> local = new List<Collider2D>();
        int n = CombatUtil.QueryEnemies(position, 4f, local);
        float carried = 6f * (PlayerStats.Instance != null ? PlayerStats.Instance.DoTDamageMultiplier : 1f);
        for (int pass = 0; pass < 2; pass++)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (local[i] == null || CombatUtil.IsDead(local[i])) continue;
                float sqr = ((Vector2)local[i].bounds.center - position).sqrMagnitude;
                if (sqr < bestSqr && sqr > 0.0001f) { bestSqr = sqr; best = i; }
            }
            if (best < 0) break;
            EnemyHealth eh = local[best].GetComponentInParent<EnemyHealth>();
            if (eh != null)
            {
                eh.ApplyPoison(carried, 3f);
                VenomCarrier vc = eh.GetComponent<VenomCarrier>();
                if (vc == null) vc = eh.gameObject.AddComponent<VenomCarrier>();
                vc.chain = chain + 1;
            }
            WeaponVfx.Lightning(position, local[best].bounds.center, new Color(0.45f, 1f, 0.4f, 0.8f), 0.05f, 0.35f, 0);   // green threads
            local[best] = null;
        }
    }

    // ------------------------------------------------------------- dash effects
    public void OnDashStart(Vector2 position, Vector2 dashDirection)
    {
        if (SkillTreeRuntime.Has(SkillEffect.PhantomClone)) StartCoroutine(CloneRoutine(position));
    }

    // Phantom Clone: a decoy lures nearby enemies (not bosses) for 2s, then explodes
    private IEnumerator CloneRoutine(Vector2 position)
    {
        GameObject clone = new GameObject("PhantomClone");
        clone.transform.position = position;
        SpriteRenderer sr = clone.AddComponent<SpriteRenderer>();
        sr.sprite = ProceduralSprites.Agent;
        // look like the character the player is actually using (skin), not the old default agent
        GameObject playerObj = GameObject.FindWithTag("Player");
        SpriteRenderer playerSr = null;
        if (playerObj != null)
        {
            // the visible body is the child called "Skin" (boon rings / auras are other children and must not be copied)
            foreach (SpriteRenderer r in playerObj.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r != null && r.sprite != null && r.gameObject.name == "Skin") { playerSr = r; break; }
            }
            if (playerSr == null)
            {
                SpriteRenderer rootSr = playerObj.GetComponent<SpriteRenderer>();
                if (rootSr != null && rootSr.sprite != null) playerSr = rootSr;
            }
        }
        if (playerSr != null)
        {
            sr.sprite = playerSr.sprite;
            sr.flipX = playerSr.flipX;
            clone.transform.localScale = playerSr.transform.lossyScale;
            clone.transform.position = (Vector3)position + (playerSr.transform.position - playerObj.transform.position);
        }
        sr.color = new Color(0.4f, 0.9f, 1f, 0.45f);
        sr.sortingOrder = 5;

        List<EnemyBase> lured = new List<EnemyBase>();
        List<Collider2D> local = new List<Collider2D>();
        int n = CombatUtil.QueryEnemies(position, 6f, local);
        for (int i = 0; i < n; i++)
        {
            if (CombatUtil.IsBoss(local[i])) continue;
            EnemyBase e = local[i].GetComponentInParent<EnemyBase>();
            if (e == null || lured.Contains(e)) continue;
            e.OverrideTarget(clone.transform);
            lured.Add(e);
        }

        yield return new WaitForSeconds(2f);

        for (int i = 0; i < lured.Count; i++)
        {
            if (lured[i] != null) lured[i].RestoreTarget();
        }

        float mult = CombatUtil.DamageMult;
        float area = CombatUtil.AreaMult;
        float radius = 3f * area;
        Vector2 at = clone != null ? (Vector2)clone.transform.position : position;
        n = CombatUtil.QueryEnemies(at, radius, local);
        for (int i = 0; i < n && i < local.Count; i++) CombatUtil.DealDamage(local[i], 70f * mult, Vector2.zero);
        WeaponVfx.Spawn(WeaponVfx.Circle, at, 0f, new Color(0.4f, 0.9f, 1f, 0.7f), 1f, radius * 2f, 0.3f);
        if (clone != null) Destroy(clone);
    }

    /// <summary>Bullet Time: dashing through an enemy shot slows all enemies by 50% for 1.5s (5s cooldown).</summary>
    public void CheckPerfectDash(Vector2 position)
    {
        if (!SkillTreeRuntime.Has(SkillEffect.BulletTime) || Time.time < bulletCd) return;

        EnemyProjectile[] shots = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        bool near = false;
        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i] != null && ((Vector2)shots[i].transform.position - position).sqrMagnitude <= 1.44f) { near = true; break; }
        }
        if (!near) return;

        bulletCd = Time.time + 5f;
        int n = CombatUtil.QueryEnemies(position, 80f, hits);
        for (int i = 0; i < n && i < hits.Count; i++) CombatUtil.Slow(hits[i], 0.5f, 1.5f);
        ScreenTint(new Color(0.45f, 0.55f, 0.7f, 0.25f), 1.5f);   // gray-blue tint
        WeaponVfx.Shockwave(position, new Color(0.4f, 0.8f, 1f, 0.8f), 12f, 0.5f);
    }
}

/// <summary>Floating number that rises and fades (Titan Revitalization heal text).</summary>
public class SkillFloatText : MonoBehaviour
{
    private float t;
    private TextMesh tm;

    private void Awake() { tm = GetComponent<TextMesh>(); }

    private void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * 1.2f * Time.deltaTime;
        if (tm != null)
        {
            Color c = tm.color;
            c.a = Mathf.Clamp01(1f - t / 1.1f);
            tm.color = c;
        }
        if (t >= 1.1f) Destroy(gameObject);
    }
}
