using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Dark Sci-Fi UI: Scales up card on mouse hover (1.05x) and returns to 1.0x on exit.
/// Uses Time.unscaledDeltaTime so animations remain silky smooth even when Time.timeScale == 0 (Level-up pause).
/// </summary>
public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Vector3 originalScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private const float HoverScale = 1.05f;
    private const float LerpSpeed = 16f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
            targetScale = originalScale;
        }
    }

    private void OnEnable()
    {
        targetScale = originalScale;
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * HoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    private void Update()
    {
        if (rectTransform == null) return;

        // Smoothly interpolate scale ignoring time scale
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * LerpSpeed);
    }
}
