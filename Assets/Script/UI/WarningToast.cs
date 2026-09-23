using TMPro;
using UnityEngine;

// Small notification bubble: pops in when its message changes, stays for holdSeconds, then fades out.
// BoardUI sends it the current warning (countdown, incoming skill, garbage).
[RequireComponent(typeof(CanvasGroup))]
public class WarningToast : MonoBehaviour
{
    public TMP_Text text;

    [Header("Timing")]
    public float holdSeconds = 2f;
    public float fadeSeconds = 0.4f;
    public float popSeconds = 0.12f;
    [Tooltip("How big the bubble starts before settling to normal size")]
    public float popScale = 1.15f;

    private CanvasGroup _group;
    private Vector3 _baseScale;
    private float _shownFor;
    private bool _visible;
    private string _message;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _group.alpha = 0f;
        _baseScale = transform.localScale;
    }

    // Call every frame with the current warning (or null). Only a new message restarts the bubble.
    public void SetMessage(string message)
    {
        if (message == _message) return;
        _message = message;

        if (string.IsNullOrEmpty(message))
        {
            // Warning is over: skip the rest of the hold and fade out now
            if (_visible) _shownFor = Mathf.Max(_shownFor, holdSeconds);
            return;
        }

        if (text != null) text.text = message;
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
