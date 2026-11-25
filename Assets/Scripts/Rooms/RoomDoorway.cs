using UnityEngine;

/// <summary>
/// One doorway in the top wall of a room prefab. Locked = dark shutter with a red bar.
/// Open = glows in the reward colour (pulsing) with a name plate on the floor, so the player knows it is ready.
/// </summary>
public class RoomDoorway : MonoBehaviour
{
    public SpriteRenderer shutter;
    public SpriteRenderer shutterBar;
    public SpriteRenderer glow;
    public SpriteRenderer glowLight;
    public SpriteRenderer plate;

    private bool open;
    private Color color = Color.white;
    private float phase;

    private void Awake()
    {
        phase = transform.position.x;
    }

    public void SetLocked()
    {
        open = false;
        Show(shutter, true);
        Show(shutterBar, true);
        Show(glow, false);
        Show(glowLight, false);
        Show(plate, false);
    }

    public void SetOpen(Color c)
    {
        open = true;
        color = c;
        Show(shutter, false);
        Show(shutterBar, false);
        Show(glow, true);
        Show(glowLight, true);
        Show(plate, true);
        if (plate != null) { Vector3 lp = plate.transform.localPosition; lp.y = -3.1f; plate.transform.localPosition = lp; } // plate right under the doorway
    }

    private static void Show(SpriteRenderer sr, bool on)
    {
        if (sr != null) sr.enabled = on;
    }

    private void Update()
    {
        if (!open) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f + phase);
        if (glow != null)
            glow.color = new Color(Mathf.Lerp(color.r, 1f, 0.25f), Mathf.Lerp(color.g, 1f, 0.25f), Mathf.Lerp(color.b, 1f, 0.25f), 0.60f + 0.30f * k);
        if (glowLight != null)
            glowLight.color = new Color(color.r, color.g, color.b, 0.22f + 0.14f * k);
    }
}
