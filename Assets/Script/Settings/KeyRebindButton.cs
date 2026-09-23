using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row's key button in Settings > Controls: click it, press a key, done. Esc cancels.
[RequireComponent(typeof(Button))]
public class KeyRebindButton : MonoBehaviour
{
    public GameAction action;
    public TMP_Text keyLabel;

    // True while any button is waiting for a key (gameplay input and Esc-to-close are paused)
    public static bool IsListening => _listening != null;
    private static KeyRebindButton _listening;
    public static int EscapeHandledFrame = -1;

    // Keyboard keys only (no mouse buttons or joystick buttons)
    private static readonly KeyCode[] KeyboardKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
        .Where(k => k != KeyCode.None && k < KeyCode.Mouse0)
        .Distinct()
        .ToArray();

    void Awake()
    {
        if (keyLabel == null) keyLabel = GetComponentInChildren<TMP_Text>(true);
        GetComponent<Button>().onClick.AddListener(StartListening);
    }

    void OnEnable()
    {
        KeyBindings.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        KeyBindings.Changed -= Refresh;
        if (_listening == this) _listening = null;
    }

    void StartListening()
    {
        if (_listening != null && _listening != this) _listening.Refresh();
        _listening = this;
        if (keyLabel != null) keyLabel.text = "<color=yellow>Press a key...</color>";
    }

    void Update()
    {
        if (_listening != this || !Input.anyKeyDown) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _listening = null;
            EscapeHandledFrame = Time.frameCount; // So Settings doesn't also close on this Esc
            Refresh();
            return;
        }

        foreach (KeyCode key in KeyboardKeys)
        {
            if (!Input.GetKeyDown(key)) continue;
            _listening = null;
            KeyBindings.Set(action, key); // Fires Changed, which refreshes every button (swaps included)
            return;
        }
    }

    public void Refresh()
    {
        if (keyLabel != null && _listening != this) keyLabel.text = KeyBindings.KeyName(KeyBindings.Get(action));
    }
}
