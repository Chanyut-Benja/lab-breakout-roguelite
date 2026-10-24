using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sci-Fi HUD Slot UI Component
/// Manages an individual slot (Weapon, Tech Passive, or Boon) with:
/// - Background Image (Carbon fiber / dark sci-fi slate #0D1117)
/// - Outer Border Image (Stylized thin neon border)
/// - Icon Image (Sprite display with auto-hide when empty)
/// - Level Text / Badge (TextMeshProUGUI displaying "Lv.1" - "Lv.5" or "MAX")
/// - Resonance Indicator (Glowing aura/border in gold/purple for Resonant Boons / Evolved Weapons)
/// </summary>
public class HUDSlotUI : MonoBehaviour
{
    public enum SlotType
    {
        Weapon,
        TechPassive,
        Boon
    }

    [Header("Slot Identity")]
    [SerializeField] private SlotType slotType = SlotType.Weapon;
    [SerializeField] private int slotIndex = 0;

    [Header("Visual Components")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI levelTextTMP;
    [SerializeField] private Text levelTextLegacy;
    [SerializeField] private TextMeshProUGUI nameTextTMP;
    [SerializeField] private Text nameTextLegacy;

    [Header("Resonance FX Indicator")]
    [SerializeField] private GameObject resonanceIndicator;
    [SerializeField] private Image resonanceGlowImage;

    [Header("Color Palette (Dark Sci-Fi)")]
    private static readonly Color EmptyBg = new Color(0.04f, 0.06f, 0.08f, 0.90f);         // #0A0F14
    private static readonly Color EmptyBorder = new Color(0.16f, 0.21f, 0.29f, 0.85f);     // #29354A
    private static readonly Color WeaponBorder = new Color(0.22f, 0.74f, 0.97f, 1f);       // #38BDF8 Neon Cyan
    private static readonly Color WeaponBg = new Color(0.04f, 0.09f, 0.15f, 0.95f);
    private static readonly Color TechBorder = new Color(0.06f, 0.73f, 0.51f, 1f);         // #10B981 Emerald
    private static readonly Color TechBg = new Color(0.03f, 0.12f, 0.08f, 0.95f);
    private static readonly Color BoonBorder = new Color(0.66f, 0.33f, 0.97f, 1f);         // #A855F7 Neon Purple
    private static readonly Color BoonBg = new Color(0.10f, 0.05f, 0.16f, 0.95f);
    private static readonly Color ResonantGold = new Color(0.96f, 0.62f, 0.04f, 1f);       // #F59E0B Radiant Gold
    private static readonly Color ResonantPurple = new Color(0.85f, 0.45f, 1.0f, 1f);     // #D973FF
    private static readonly Color MutedText = new Color(0.35f, 0.42f, 0.52f, 0.6f);

    private bool isResonantActive = false;
    private float pulseTimer = 0f;

    public SlotType Type => slotType;
    public int Index => slotIndex;
    public Image BorderImage => borderImage;
    public Image BgImage => bgImage;
    public Image IconImage => iconImage;
    public TextMeshProUGUI LevelTextTMP => levelTextTMP;
    public GameObject ResonanceIndicator => resonanceIndicator;

    private void Update()
    {
        if (isResonantActive && resonanceGlowImage != null)
        {
            // Shimmering / pulsing golden-purple energy aura (3.5 Hz)
            pulseTimer += Time.unscaledDeltaTime * 4.5f;
            float alpha = 0.55f + Mathf.Sin(pulseTimer) * 0.35f;
            Color c = resonanceGlowImage.color;
            c.a = Mathf.Clamp01(alpha);
            resonanceGlowImage.color = c;
        }
    }

    public void SetupSlot(SlotType type, int index)
    {
        slotType = type;
        slotIndex = index;
    }

    public void SetReferences(Image border, Image bg, Image icon, TextMeshProUGUI lvlTMP, Text lvlLegacy,
                              TextMeshProUGUI nameTMP, Text nameLegacy, GameObject resObj, Image resGlow)
    {
        borderImage = border;
        bgImage = bg;
        iconImage = icon;
        levelTextTMP = lvlTMP;
        levelTextLegacy = lvlLegacy;
        nameTextTMP = nameTMP;
        nameTextLegacy = nameLegacy;
        resonanceIndicator = resObj;
        resonanceGlowImage = resGlow;
    }

    public void SetEmpty()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }

        if (borderImage != null) borderImage.color = EmptyBorder;
        if (bgImage != null) bgImage.color = EmptyBg;

        SetLevelText("");
        SetNameText("[ - ]", MutedText);
        SetResonanceActive(false);
    }

    public void SetWeapon(Sprite icon, int level, bool isEvolved, string fallbackName)
    {
        bool hasIcon = icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(hasIcon);
        }

        if (isEvolved)
        {
            if (borderImage != null) borderImage.color = ResonantGold;
            if (bgImage != null) bgImage.color = new Color(0.18f, 0.12f, 0.03f, 0.95f);
            SetLevelText("★EVO", ResonantGold);
            SetNameText(hasIcon ? "" : fallbackName, ResonantGold);
            SetResonanceActive(true, ResonantGold);
        }
        else
        {
            if (borderImage != null) borderImage.color = WeaponBorder;
            if (bgImage != null) bgImage.color = WeaponBg;
            string lvlBadge = level >= 5 ? "MAX" : $"Lv.{level}";
            SetLevelText(lvlBadge, WeaponBorder);
            SetNameText(hasIcon ? "" : fallbackName, Color.white);
            SetResonanceActive(false);
        }
    }

    public void SetTech(Sprite icon, int level, string fallbackName)
    {
        bool hasIcon = icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(hasIcon);
        }

        if (borderImage != null) borderImage.color = TechBorder;
        if (bgImage != null) bgImage.color = TechBg;

        string lvlBadge = level >= 5 ? "MAX" : $"Lv.{level}";
        SetLevelText(lvlBadge, TechBorder);
        SetNameText(hasIcon ? "" : fallbackName, new Color(0.65f, 1f, 0.85f, 1f));
        SetResonanceActive(false);
    }

    public void SetBoon(Sprite icon, int level, bool isResonant, string fallbackName)
    {
        bool hasIcon = icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(hasIcon);
        }

        if (isResonant)
        {
            if (borderImage != null) borderImage.color = ResonantPurple;
            if (bgImage != null) bgImage.color = new Color(0.20f, 0.08f, 0.28f, 0.98f);
            SetLevelText("★RESO", ResonantGold);
            SetNameText(hasIcon ? "" : fallbackName, ResonantPurple);
            SetResonanceActive(true, ResonantPurple);
        }
        else
        {
            if (borderImage != null) borderImage.color = BoonBorder;
            if (bgImage != null) bgImage.color = BoonBg;
            string lvlBadge = level >= 2 ? "MAX" : $"Lv.{level}";
            SetLevelText(lvlBadge, BoonBorder);
            SetNameText(hasIcon ? "" : fallbackName, new Color(0.9f, 0.8f, 1f, 1f));
            SetResonanceActive(false);
        }
    }

    public void SetResonanceActive(bool active, Color? glowColor = null)
    {
        isResonantActive = active;
        if (resonanceIndicator != null)
        {
            resonanceIndicator.SetActive(active);
        }

        if (active && resonanceGlowImage != null && glowColor.HasValue)
        {
            Color c = glowColor.Value;
            c.a = 0.8f;
            resonanceGlowImage.color = c;
        }
    }

    private void SetLevelText(string text, Color? color = null)
    {
        if (levelTextTMP != null)
        {
            levelTextTMP.text = text;
            if (color.HasValue) levelTextTMP.color = color.Value;
            levelTextTMP.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        if (levelTextLegacy != null)
        {
            levelTextLegacy.text = text;
            if (color.HasValue) levelTextLegacy.color = color.Value;
            levelTextLegacy.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    private void SetNameText(string text, Color? color = null)
    {
        if (nameTextTMP != null)
        {
            nameTextTMP.text = text;
            if (color.HasValue) nameTextTMP.color = color.Value;
            nameTextTMP.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        if (nameTextLegacy != null)
        {
            nameTextLegacy.text = text;
            if (color.HasValue) nameTextLegacy.color = color.Value;
            nameTextLegacy.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
