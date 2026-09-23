using UnityEngine;
using UnityEngine.UI;

// A read-only Slider showing SP as a resource bar.
// Full = enough SP for skill 3; tick marks show where skills 1 and 2 unlock,
// and the fill color changes as each skill becomes affordable. BoardUI feeds it the SP.
[RequireComponent(typeof(Slider))]
public class SkillPointBar : MonoBehaviour
{
    [Header("References (auto-found)")]
    public Slider slider;
    public Image fillImage;

    [Header("Fill Colors")]
    public Color notEnoughColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color skill1ReadyColor = new Color(0.25f, 0.8f, 1f, 1f);
    public Color skill2ReadyColor = new Color(1f, 0.6f, 0.1f, 1f);
    public Color skill3ReadyColor = new Color(1f, 0.25f, 0.8f, 1f);

    [Header("Behaviour")]
    [Tooltip("How fast the bar animates, in SP per second")]
    public float fillSpeed = 2000f;
    [Tooltip("Pulse the bar when skill 3 is ready")]
    public bool pulseWhenFull = true;

    [Header("Tier Markers")]
    public bool showTierMarkers = true;
    public Color markerColor = new Color(1f, 1f, 1f, 0.8f);
    public float markerWidth = 3f;

    private int _maxSP;
    private float _targetSP;
    private float _shownSP;

    void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
        if (fillImage == null && slider.fillRect != null) fillImage = slider.fillRect.GetComponent<Image>();

        _maxSP = TetrisEngine.SkillCost(3);
        slider.interactable = false; // Display only
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = _maxSP;
        slider.wholeNumbers = false;
        slider.value = 0f;
        if (slider.handleRect != null) slider.handleRect.gameObject.SetActive(false);

        if (showTierMarkers) CreateTierMarkers();
    }

    public void SetSkillPoints(int skillPoints)
    {
        _targetSP = Mathf.Clamp(skillPoints, 0, _maxSP);
    }

    void Update()
    {
        _shownSP = Mathf.MoveTowards(_shownSP, _targetSP, fillSpeed * Time.deltaTime);
        slider.value = _shownSP;

        if (fillImage == null) return;
        Color color;
        if (_targetSP >= TetrisEngine.SkillCost(3)) color = skill3ReadyColor;
        else if (_targetSP >= TetrisEngine.SkillCost(2)) color = skill2ReadyColor;
        else if (_targetSP >= TetrisEngine.SkillCost(1)) color = skill1ReadyColor;
        else color = notEnoughColor;

        if (pulseWhenFull && _targetSP >= _maxSP)
        {
            color = Color.Lerp(color, Color.white, (Mathf.Sin(Time.time * 8f) + 1f) * 0.2f);
        }
        fillImage.color = color;
    }

    // Thin lines at the SP needed for skills 1 and 2 (skill 3 is the end of the bar)
    void CreateTierMarkers()
    {
        RectTransform area = slider.fillRect != null ? slider.fillRect.parent as RectTransform : (RectTransform)transform;
        if (area == null) return;

        for (int tier = 1; tier <= 2; tier++)
        {
            float t = (float)TetrisEngine.SkillCost(tier) / _maxSP;

            var marker = new GameObject($"Skill{tier}Marker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)marker.transform;
            rect.SetParent(area, false);
            rect.anchorMin = new Vector2(t, 0f);
            rect.anchorMax = new Vector2(t, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(markerWidth, 0f);
            rect.anchoredPosition = Vector2.zero;

            Image image = marker.GetComponent<Image>();
            image.color = markerColor;
            image.raycastTarget = false;
        }
    }
}
