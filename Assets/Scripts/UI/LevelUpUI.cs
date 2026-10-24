using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for Level Up UI.
/// Manages Weapon & Tech Passive selection cards when the player levels up.
/// Guarantees that card buttons are responsive and raycasts are not blocked.
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("Panel Reference")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private TMP_Text tmpTitleText;

    [Header("Card Buttons (3 Cards)")]
    [SerializeField] private Button[] cardButtons;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (levelUpPanel == null)
        {
            levelUpPanel = gameObject;
        }

        if (cardButtons == null || cardButtons.Length == 0)
        {
            List<Button> buttons = new List<Button>();
            foreach (Button b in GetComponentsInChildren<Button>(true))
            {
                string bName = b.name.ToLowerInvariant();
                if (!bName.Contains("reroll") && !bName.Contains("banish") && !bName.Contains("close") && !bName.Contains("exit"))
                {
                    buttons.Add(b);
                }
            }
            if (buttons.Count >= 3)
            {
                cardButtons = buttons.GetRange(0, 3).ToArray();
            }
        }
    }

    /// <summary>
    /// Binds card buttons and ensures listeners and raycasts are correctly configured.
    /// </summary>
    public void BindCardButton(int index, LevelUpUpgradeData data, System.Action onSelected)
    {
        if (cardButtons == null || index < 0 || index >= cardButtons.Length || cardButtons[index] == null) return;

        Button btn = cardButtons[index];
        btn.interactable = true;

        Image btnImg = btn.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.raycastTarget = true;
        }

        // Disable raycastTarget on all child graphics so they do not block mouse clicks
        foreach (Graphic g in btn.GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject != btn.gameObject)
            {
                g.raycastTarget = false;
            }
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OnCardSelected(data);
            onSelected?.Invoke();
        });
    }

    public void OnCardSelected(LevelUpUpgradeData upgradeData)
    {
        if (upgradeData == null) return;

        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.ApplyChosenUpgrade(upgradeData);
        }
    }

    public void OnCardSelected(int index)
    {
        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.SelectUpgrade(index);
        }
    }
}
