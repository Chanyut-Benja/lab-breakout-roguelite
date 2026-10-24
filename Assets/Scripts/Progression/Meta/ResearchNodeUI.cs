using UnityEngine;
using UnityEngine.UI;

public class ResearchNodeUI : MonoBehaviour
{
    [Header("Node Settings")]
    [SerializeField] private MetaUpgradeType upgradeType = MetaUpgradeType.MaxHealth;
    [SerializeField] private string nodeTitle = "Genetic Fortification";

    [Header("UI Elements")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text costText;
    [SerializeField] private Button upgradeButton;

    private void Start()
    {
        if (upgradeButton != null && upgradeButton.onClick.GetPersistentEventCount() == 0)
        {
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        if (ResearchTreeManager.Instance != null)
        {
            ResearchTreeManager.Instance.OnTreeUpdated -= RefreshUI;
            ResearchTreeManager.Instance.OnTreeUpdated += RefreshUI;
        }

        RefreshUI();
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (ResearchTreeManager.Instance != null)
        {
            ResearchTreeManager.Instance.OnTreeUpdated -= RefreshUI;
        }
    }

    public void RefreshUI()
    {
        if (ResearchTreeManager.Instance == null) return;

        int currentLevel = ResearchTreeManager.Instance.GetUpgradeLevel(upgradeType);
        int maxLevel = ResearchTreeManager.Instance.MaxLevel;
        int cost = ResearchTreeManager.Instance.GetUpgradeCost(upgradeType);
        bool canAfford = ResearchTreeManager.Instance.CanUpgrade(upgradeType);
        string dynamicTitle = ResearchTreeManager.Instance.GetBranchTitle(upgradeType);

        if (titleText != null)
        {
            titleText.text = !string.IsNullOrEmpty(dynamicTitle) ? dynamicTitle : nodeTitle;
        }

        if (levelText != null)
        {
            string nodeKind = currentLevel < 3 ? "Minor" : "Major";
            levelText.text = currentLevel >= maxLevel
                ? $"MAXED ({maxLevel}/{maxLevel})"
                : $"Step {currentLevel} / {maxLevel} ({nodeKind})";
        }

        if (costText != null)
        {
            costText.text = currentLevel >= maxLevel ? "Completed" :
                !ResearchTreeManager.Instance.MeetsPrerequisites(upgradeType) ?
                ResearchTreeManager.Instance.GetPrerequisiteText(upgradeType) : $"{cost} RP";
        }

        if (upgradeButton != null)
        {
            upgradeButton.interactable = canAfford && currentLevel < maxLevel;
        }
    }

    public void OnUpgradeClicked()
    {
        if (ResearchTreeManager.Instance != null)
        {
            ResearchTreeManager.Instance.TryUpgrade(upgradeType);
        }
    }
}
