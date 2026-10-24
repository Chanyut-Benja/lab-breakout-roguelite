using UnityEngine;
using UnityEngine.UI;

public class ExpBarUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private Text levelText;
    [SerializeField] private Text expValueText;

    private void Start()
    {
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnExpChanged += UpdateExpDisplay;
            UpdateExpDisplay(PlayerLevel.Instance.CurrentExp, PlayerLevel.Instance.ExpToNextLevel, PlayerLevel.Instance.CurrentLevel);
        }
        else
        {
            // พยายามหา Player ในฉาก
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                PlayerLevel level = player.GetComponent<PlayerLevel>();
                if (level != null)
                {
                    level.OnExpChanged += UpdateExpDisplay;
                    UpdateExpDisplay(level.CurrentExp, level.ExpToNextLevel, level.CurrentLevel);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnExpChanged -= UpdateExpDisplay;
        }
    }

    private void UpdateExpDisplay(int currentExp, int maxExp, int currentLevel)
    {
        if (expSlider != null)
        {
            expSlider.maxValue = maxExp;
            expSlider.value = currentExp;
        }

        if (levelText != null)
        {
            levelText.text = $"Lv. {currentLevel}";
        }

        if (expValueText != null)
        {
            expValueText.text = $"{currentExp} / {maxExp}";
        }
    }
}
