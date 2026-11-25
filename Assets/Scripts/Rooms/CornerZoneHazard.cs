using UnityEngine;

/// <summary>
/// Anti corner-camping: each of the four room corners is a danger zone.
/// Staying inside one for longer than GraceTime starts hurting the player (percent of max HP per second).
/// Leaving the zone resets the timer. Dashing is safe (PlayerHealth ignores damage while invulnerable).
/// Created automatically in the game scene; no setup needed.
/// </summary>
public class CornerZoneHazard : MonoBehaviour
{
    // ---- tuning ----
    const float ZoneSize = 2f;          // one floor tile (2 x 2 m) in each corner, measured from the inside of the walls
    const float GraceTime = 2f;         // seconds in a corner before damage starts
    const float DamagePercentPerSec = 0.06f;   // of max HP per second
    const float TickInterval = 0.5f;

    static CornerZoneHazard instance;
    PlayerHealth player;
    SpriteRenderer[] marks = new SpriteRenderer[4];
    float timeInCorner;
    float tickTimer;
    float warnTextCooldown;
    int loadedRoomForMarks = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("CornerZoneHazard");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CornerZoneHazard>();
    }

    void Update()
    {
        RoomEnvironment env = RoomEnvironment.Instance;
        RoomManager rm = RoomManager.Instance;
        if (env == null || rm == null || !rm.IsRoomActive)
        {
            Reset();
            return;
        }

        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        if (player == null || player.CurrentHealth <= 0f) { Reset(); return; }

        Bounds b = env.ArenaBounds;
        Vector2 half = new Vector2(b.extents.x - RoomEnvironment.WallThickness, b.extents.y - RoomEnvironment.WallThickness);
        Vector2 c = b.center;
        Vector2 p = player.transform.position;
        Vector2 rel = p - c;

        EnsureMarks(env, c, half);

        bool inCorner = Mathf.Abs(rel.x) >= half.x - ZoneSize && Mathf.Abs(rel.y) >= half.y - ZoneSize;
        UpdateMarks(inCorner);

        if (!inCorner)
        {
            timeInCorner = Mathf.Max(0f, timeInCorner - Time.deltaTime * 2f);   // cools down twice as fast as it builds
            tickTimer = 0f;
            return;
        }

        timeInCorner += Time.deltaTime;
        warnTextCooldown -= Time.deltaTime;
        if (timeInCorner > 0.3f && timeInCorner < GraceTime && warnTextCooldown <= 0f)
        {
            warnTextCooldown = 1.2f;
            SkillMajorEffects.FloatText("LEAVE THE CORNER!", p + Vector2.up * 1.6f, new Color(1f, 0.35f, 0.3f, 1f));
        }

        if (timeInCorner >= GraceTime)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                // Percent-of-max-HP damage: ignore Armor (which cut every tick down to 1) and shields; a dash still dodges it
                if (!player.IsDashInvulnerable)
                    player.TakePurgeDamage(player.MaxHealth * DamagePercentPerSec * TickInterval);
            }
        }
    }

    void Reset()
    {
        timeInCorner = 0f;
        tickTimer = 0f;
        for (int i = 0; i < marks.Length; i++) if (marks[i] != null) marks[i].enabled = false;
    }

    // ---------------------------------------------------------------- visuals
    static Sprite whiteSprite;
    static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px);
        t.Apply();
        whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);   // 1 unit square
        return whiteSprite;
    }

    void EnsureMarks(RoomEnvironment env, Vector2 c, Vector2 half)
    {
        if (loadedRoomForMarks == env.LoadedRoom && marks[0] != null) return;
        loadedRoomForMarks = env.LoadedRoom;
        for (int i = 0; i < 4; i++)
        {
            if (marks[i] == null)
            {
                var go = new GameObject("CornerMark" + i);
                go.transform.SetParent(transform, false);
                marks[i] = go.AddComponent<SpriteRenderer>();
                marks[i].sprite = White();
                marks[i].sortingOrder = 2;
            }
            float sx = (i & 1) == 0 ? 1f : -1f;
            float sy = (i & 2) == 0 ? 1f : -1f;
            marks[i].transform.position = new Vector3(c.x + sx * (half.x - ZoneSize * 0.5f), c.y + sy * (half.y - ZoneSize * 0.5f), 0f);
            marks[i].transform.localScale = new Vector3(ZoneSize, ZoneSize, 1f);
            marks[i].enabled = true;
        }
    }

    void UpdateMarks(bool playerInside)
    {
        float danger = Mathf.Clamp01(timeInCorner / GraceTime);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (timeInCorner >= GraceTime ? 14f : 5f));
        float a = playerInside ? Mathf.Lerp(0.14f, 0.38f, danger) * (0.6f + 0.4f * pulse) : 0.07f;
        for (int i = 0; i < 4; i++)
        {
            if (marks[i] == null) continue;
            marks[i].enabled = true;
            marks[i].color = new Color(1f, 0.15f, 0.1f, a);
        }
    }
}
