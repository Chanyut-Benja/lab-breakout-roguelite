using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สกินปุ่มพิกเซลอาร์ต 4 สี (เทา / ส้ม / ฟ้า / เขียวอมฟ้า) จาก Resources/UI/PixelButtons
/// ใช้: PixelButtonSkin.Apply(button, PixelButtonSkin.Style.Orange);
/// </summary>
public static class PixelButtonSkin
{
    public enum Style { Grey, Orange, Blue, Teal }

    private const float SpriteHeightPx = 15f;                 // สูง 15 พิกเซลในสไปรต์ (รวมเงา)
    private static readonly Vector4 Border = new Vector4(4f, 5f, 4f, 4f);   // ซ้าย, ล่าง, ขวา, บน
    private static readonly Sprite[] cache = new Sprite[4];

    public static Sprite GetSprite(Style style)
    {
        int i = (int)style;
        if (cache[i] != null) return cache[i];

        string path = "UI/PixelButtons/Btn_" + style;
        Sprite s = Resources.Load<Sprite>(path);
        if (s == null)
        {
            // สำรอง: ถ้า Unity ยังไม่ได้นำเข้าเป็น Sprite ให้สร้างเองจากเท็กซ์เจอร์
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Border);
        }
        cache[i] = s;
        return s;
    }

    public static void Apply(Button btn, Style style)
    {
        if (btn == null) return;
        Sprite sprite = GetSprite(style);
        if (sprite == null) return;   // ไม่มีไฟล์ ก็ใช้หน้าตาเดิมต่อ

        Image img = btn.GetComponent<Image>();
        if (img == null) return;

        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;
        img.preserveAspect = false;
        img.fillCenter = true;

        // ให้ 1 พิกเซลของสไปรต์ = (ความสูงปุ่ม / 15) หน่วย เพื่อให้ขอบและมุมโค้งสัดส่วนเดิมทุกขนาดปุ่ม
        RectTransform rt = btn.GetComponent<RectTransform>();
        float h = rt != null && rt.rect.height > 1f ? rt.rect.height : (rt != null ? rt.sizeDelta.y : 60f);
        img.pixelsPerUnitMultiplier = SpriteHeightPx / Mathf.Max(8f, h);

        // ซ่อนพื้นหลัง/กรอบซ้อนที่ปุ่มบางแบบสร้างทับไว้ (เช่น ButtonBody ของหน้า Game Over)
        for (int ci = 0; ci < btn.transform.childCount; ci++)
        {
            Transform child = btn.transform.GetChild(ci);
            Image childImg = child.GetComponent<Image>();
            if (childImg == null) continue;
            string cn = child.name;
            if (cn.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0 || cn.IndexOf("Border", System.StringComparison.OrdinalIgnoreCase) >= 0
                || cn.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) >= 0 || cn.IndexOf("Frame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || cn.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0)
                childImg.enabled = false;
        }

        PixelButtonMark mark = btn.GetComponent<PixelButtonMark>();
        if (mark == null) mark = btn.gameObject.AddComponent<PixelButtonMark>();
        mark.style = style;

        // ลบกรอบเรืองแสงเดิม
        Outline[] outlines = btn.GetComponents<Outline>();
        for (int i = 0; i < outlines.Length; i++) Kill(outlines[i]);
        Shadow[] shadows = btn.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++) if (!(shadows[i] is Outline)) Kill(shadows[i]);

        // สีตอนชี้/กด: ใช้ ColorTint เทา ๆ ไม่ให้สีเพี้ยน
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        c.selectedColor = Color.white;
        c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        c.colorMultiplier = 1f;
        btn.colors = c;

        // สีตัวอักษร: ปุ่มเทาอ่อนใช้ตัวดำ ที่เหลือตัวขาว
        Text t = btn.GetComponentInChildren<Text>(true);
        if (t != null)
        {
            t.color = style == Style.Grey ? new Color(0.1f, 0.1f, 0.12f, 1f) : Color.white;
            Shadow sh = t.GetComponent<Shadow>();
            if (sh != null) sh.effectColor = style == Style.Grey ? new Color(1f, 1f, 1f, 0.35f) : new Color(0f, 0f, 0f, 0.7f);
        }
    }

    private static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }
}


// PixelButtonMark moved to its own file (a MonoBehaviour must live in a file with the same name, or scenes lose the script)

/// <summary>
/// ไล่ใส่สกินพิกเซลให้ปุ่มทุกปุ่มที่ยังไม่มีสกิน (ปุ่มข้อความแบบยาว และปุ่มลูกศร &lt; &gt;)
/// ไม่แตะการ์ด ช่องของ ปุ่มวงกลมของ Skill Tree และชื่อบนประตู
/// </summary>
public class PixelButtonAutoSkin : MonoBehaviour
{
    private static readonly string[] Excluded = { "Node", "Card", "Slot", "Toggle", "Icon", "Door" };
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject go = new GameObject("PixelButtonAutoSkin");
        DontDestroyOnLoad(go);
        go.AddComponent<PixelButtonAutoSkin>();
    }

    private static PixelButtonAutoSkin inst;

    private void Awake() { inst = this; }

    /// <summary>Skin every button right now (call right after opening a screen so the old look never shows).</summary>
    public static void ApplyNow() { if (inst != null) inst.Scan(); }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.35f;
        Scan();
    }

    private void Scan()
    {
        // hidden screens are skinned too, so they already look right the first time they open
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Sprite skinSprite = PixelButtonSkin.GetSprite(PixelButtonSkin.Style.Orange);
        if (skinSprite == null) return;   // ยังไม่มีไฟล์ปุ่ม

        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;

            PixelButtonMark mark = b.GetComponent<PixelButtonMark>();
            if (mark != null)
            {
                // ถ้าสคริปต์อื่นมาเปลี่ยนสี/สไปรต์ทับ ให้ใส่สกินกลับ
                Image im = b.GetComponent<Image>();
                if (im != null && (im.sprite != PixelButtonSkin.GetSprite(mark.style) || im.color != Color.white))
                    PixelButtonSkin.Apply(b, mark.style);
                continue;
            }

            if (!Eligible(b, out string label)) continue;
            Image img = b.GetComponent<Image>();
            PixelButtonSkin.Apply(b, Pick(b, label, img != null ? img.color : Color.white));
        }
    }

    private static bool Eligible(Button b, out string label)
    {
        label = null;
        Image img = b.GetComponent<Image>();
        if (img == null || !img.enabled) return false;

        Text t = b.GetComponentInChildren<Text>(true);
        if (t == null || string.IsNullOrWhiteSpace(t.text)) return false;
        label = t.text.Trim();
        if (label.Length > 24 || label.Contains("\n")) return false;

        string n = b.name;
        for (int i = 0; i < Excluded.Length; i++)
            if (n.IndexOf(Excluded[i], System.StringComparison.OrdinalIgnoreCase) >= 0) return false;

        RectTransform rt = b.GetComponent<RectTransform>();
        if (rt == null) return false;
        float w = rt.rect.width, h = rt.rect.height;
        if (h < 1f || w < 1f) return false;

        bool arrow = label == "<" || label == ">" || label == "◀" || label == "▶" || label == "‹" || label == "›";
        if (arrow) return h <= 130f;
        return w / h >= 1.5f && h >= 24f && h <= 130f;
    }

    private static PixelButtonSkin.Style Pick(Button b, string label, Color tint)
    {
        string s = (label + " " + b.name).ToUpperInvariant();
        string[] grey = { "BACK", "QUIT", "CLOSE", "CANCEL", "EXIT", "END RUN", "SKIP", "BANISH", "NO" };
        string[] teal = { "SETTING", "APPLY", "SAVE", "RESET", "OPTIONS" };
        string[] orange = { "SELECT", "DEPLOY", "PLAY", "START", "RESUME", "CONFIRM", "YES", "OK", "CONTINUE", "BUY", "PURCHASE", "REROLL", "UNLOCK", "NEXT", "RESTART", "AGAIN" };

        // ปุ่ม DEPLOY ของการ์ดตัวละคร: แดง→ส้ม, เขียว→เขียวอมฟ้า, น้ำเงิน→ฟ้า
        if (b.name.IndexOf("Deploy", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Color.RGBToHSV(tint, out float hue, out float sat, out _);
            if (sat > 0.2f)
            {
                if (hue > 0.25f && hue < 0.5f) return PixelButtonSkin.Style.Teal;
                if (hue >= 0.5f && hue < 0.8f) return PixelButtonSkin.Style.Blue;
                return PixelButtonSkin.Style.Orange;
            }
        }

        for (int i = 0; i < grey.Length; i++) if (WordHas(s, grey[i])) return PixelButtonSkin.Style.Grey;
        for (int i = 0; i < teal.Length; i++) if (s.Contains(teal[i])) return PixelButtonSkin.Style.Teal;
        for (int i = 0; i < orange.Length; i++) if (WordHas(s, orange[i])) return PixelButtonSkin.Style.Orange;
        return PixelButtonSkin.Style.Blue;
    }

    private static bool WordHas(string text, string word)
    {
        int idx = 0;
        while ((idx = text.IndexOf(word, idx, System.StringComparison.Ordinal)) >= 0)
        {
            bool startOk = idx == 0 || !char.IsLetter(text[idx - 1]);
            int end = idx + word.Length;
            bool endOk = end >= text.Length || !char.IsLetter(text[end]);
            if (startOk && endOk) return true;
            idx = end;
        }
        return false;
    }
}

