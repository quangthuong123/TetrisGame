using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pop-up over your board when the opponent's skill hits you: the skill's icon, its name and what it did.
// Stays for holdSeconds, then fades. Created by Tools > Tetris > Setup Skill Icons; BoardUI calls Show().
[RequireComponent(typeof(CanvasGroup))]
public class SkillAlert : MonoBehaviour
{
    public Image icon;
    public TMP_Text title;
    public TMP_Text detail;

    [Header("Timing")]
    public float holdSeconds = 2f;
    public float fadeSeconds = 0.4f;
    public float popSeconds = 0.12f;
    [Tooltip("How big the alert starts before settling to normal size")]
    public float popScale = 1.15f;

    private CanvasGroup _group;
    private Vector3 _baseScale;
    private float _shownFor;
    private bool _visible;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _baseScale = transform.localScale;
        _group.alpha = 0f;
    }

    public void Show(Sprite skillIcon, string titleText, string detailText)
    {
        gameObject.SetActive(true);
        if (icon != null)
        {
            icon.sprite = skillIcon;
            icon.enabled = skillIcon != null;
        }
        if (title != null) title.text = titleText;
        if (detail != null) detail.text = detailText;
        _shownFor = 0f;
        _visible = true;
    }

    void Update()
    {
        if (!_visible) return;
        _shownFor += Time.deltaTime;

        float pop = popSeconds > 0f ? Mathf.Clamp01(_shownFor / popSeconds) : 1f;
        transform.localScale = _baseScale * Mathf.Lerp(popScale, 1f, pop);

        float fade = fadeSeconds > 0f ? (_shownFor - holdSeconds) / fadeSeconds : (_shownFor >= holdSeconds ? 1f : 0f);
        _group.alpha = 1f - Mathf.Clamp01(fade);
        if (_group.alpha <= 0f) _visible = false;
    }
}
