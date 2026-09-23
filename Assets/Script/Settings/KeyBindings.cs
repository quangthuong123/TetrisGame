using System;
using UnityEngine;

public enum GameAction
{
    MoveLeft, MoveRight, SoftDrop, HardDrop,
    RotateClockwise, RotateCounterClockwise, Rotate180, Hold,
    Skill1, Skill2, Skill3,
}

// Rebindable keyboard controls, saved in PlayerPrefs. FusionLauncher reads these every frame.
public static class KeyBindings
{
    public static event Action Changed;

    private static readonly KeyCode[] Defaults =
    {
        KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.Space,
        KeyCode.UpArrow, KeyCode.Z, KeyCode.A, KeyCode.C,
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
    };

    private static readonly KeyCode[] Current = (KeyCode[])Defaults.Clone();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
        {
            Current[(int)action] = (KeyCode)PlayerPrefs.GetInt(PrefKey(action), (int)Defaults[(int)action]);
        }
    }

    public static KeyCode Get(GameAction action) => Current[(int)action];

    public static bool Held(GameAction action) => Input.GetKey(Current[(int)action]);
    public static bool Pressed(GameAction action) => Input.GetKeyDown(Current[(int)action]);

    // Assigns a key; if another action already uses it, the two swap so nothing is left unbound
    public static void Set(GameAction action, KeyCode key)
    {
        int index = (int)action;
        int clash = Array.IndexOf(Current, key);
        if (clash >= 0 && clash != index)
        {
            Current[clash] = Current[index];
            PlayerPrefs.SetInt(PrefKey((GameAction)clash), (int)Current[clash]);
        }
        Current[index] = key;
        PlayerPrefs.SetInt(PrefKey(action), (int)key);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ResetToDefaults()
    {
        foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
        {
            Current[(int)action] = Defaults[(int)action];
            PlayerPrefs.DeleteKey(PrefKey(action));
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static string DisplayName(GameAction action)
    {
        switch (action)
        {
            case GameAction.MoveLeft: return "Move Left";
            case GameAction.MoveRight: return "Move Right";
            case GameAction.SoftDrop: return "Soft Drop";
            case GameAction.HardDrop: return "Hard Drop";
            case GameAction.RotateClockwise: return "Rotate Right";
            case GameAction.RotateCounterClockwise: return "Rotate Left";
            case GameAction.Rotate180: return "Rotate 180";
            case GameAction.Hold: return "Hold";
            case GameAction.Skill1: return "Skill 1 (Block Breaker)";
            case GameAction.Skill2: return "Skill 2 (Forced Z)";
            case GameAction.Skill3: return "Skill 3 (X-Bomb)";
            default: return action.ToString();
        }
    }

    public static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftArrow: return "Left";
            case KeyCode.RightArrow: return "Right";
            case KeyCode.UpArrow: return "Up";
            case KeyCode.DownArrow: return "Down";
            case KeyCode.Space: return "Space";
            case KeyCode.LeftShift: return "L-Shift";
            case KeyCode.RightShift: return "R-Shift";
            case KeyCode.LeftControl: return "L-Ctrl";
            case KeyCode.RightControl: return "R-Ctrl";
            case KeyCode.LeftAlt: return "L-Alt";
            case KeyCode.RightAlt: return "R-Alt";
        }
        string name = key.ToString();
        if (name.StartsWith("Alpha")) return name.Substring(5);
        if (name.StartsWith("Keypad")) return "Num " + name.Substring(6);
        return name;
    }

    private static string PrefKey(GameAction action) => "Key_" + action;
}
