using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Shared "outlined Big Button" look from the Simple Buttons pack, used by the styling tools.
public static class ButtonSkin
{
    public enum Result { Styled, AlreadyIcon, CustomArt, NoButton }

    private const string BigButtonsFolder = "Assets/Simple Buttons/Big Buttons/";
    private const string IconButtonsFolder = "Assets/Simple Buttons/Buttons Style";
    private const string UndoName = "Style Buttons";

    // 9-slice border (left, bottom, right, top) so the white outline keeps its shape when stretched
    private static readonly Vector4 BigButtonBorder = new Vector4(100, 80, 100, 80);

    // Buttons that go back or leave get the secondary (blue) skin
    private static readonly string[] SecondaryWords = { "back", "return", "quit", "exit", "cancel", "leave", "menu", "_p2" };

    public static Sprite Purple => Load("BigPurple.png");
    public static Sprite Blue => Load("BigBlue.png");
    public static Sprite LightBlue => Load("BigLigthBlue.png"); // (sic) the pack's file name

    public static bool SpritesAvailable => Purple != null && Blue != null && LightBlue != null;

    // Purple for main actions, blue for back/leave style buttons
    public static Sprite SpriteFor(Button button)
    {
        string label = button.GetComponentInChildren<TMP_Text>(true)?.text ?? "";
        string key = (button.name + " " + label).ToLowerInvariant();
        return SecondaryWords.Any(key.Contains) ? Blue : Purple;
    }

    public static Result Apply(Button button, Sprite sprite)
    {
        if (button == null) return Result.NoButton;
        GameObject go = button.gameObject;

        TMP_Text textOnButton = go.GetComponent<TMP_Text>();
        if (textOnButton != null) MoveTextToChild(go, textOnButton); // A text-only button can't also hold an Image

        Image image = go.GetComponent<Image>();
        if (image != null && !CanReplace(image.sprite))
        {
            return IsIcon(image.sprite) ? Result.AlreadyIcon : Result.CustomArt;
        }
        if (image == null) image = Undo.AddComponent<Image>(go);

        Undo.RecordObject(image, UndoName);
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = true;
        // Shrink the outline corners on short buttons so they don't overlap
        float height = ((RectTransform)go.transform).rect.height;
        float borders = BigButtonBorder.y + BigButtonBorder.w;
        image.pixelsPerUnitMultiplier = height > 0f ? Mathf.Max(1f, borders / (height * 0.8f)) : 1f;

        Undo.RecordObject(button, UndoName);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.92f, 1f, 1f);
        colors.pressedColor = new Color(0.70f, 0.76f, 1f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.7f, 0.7f);
        button.colors = colors;

        foreach (TMP_Text label in go.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(label, UndoName);
            label.color = Color.white;
            label.fontStyle |= FontStyles.Bold;
        }
        return Result.Styled;
    }

    // Unity's default button sprites, no sprite, or an earlier Big Button can be replaced.
    // Anything else is custom art (text may be baked in), so it's left alone.
    private static bool CanReplace(Sprite sprite)
    {
        if (sprite == null) return true;
        string path = AssetDatabase.GetAssetPath(sprite);
        return path.StartsWith("Resources/unity_builtin_extra") || path.StartsWith(BigButtonsFolder);
    }

    private static bool IsIcon(Sprite sprite)
    {
        return sprite != null && AssetDatabase.GetAssetPath(sprite).StartsWith(IconButtonsFolder);
    }

    private static void MoveTextToChild(GameObject button, TMP_Text text)
    {
        var label = new GameObject("Label", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(label, UndoName);
        label.layer = button.layer;
        RectTransform rect = (RectTransform)label.transform;
        rect.SetParent(button.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        UnityEditorInternal.ComponentUtility.CopyComponent(text);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(label);
        TMP_Text copy = label.GetComponent<TMP_Text>();
        copy.raycastTarget = false;

        Undo.DestroyObjectImmediate(text);
    }

    private static Sprite Load(string file)
    {
        string path = BigButtonsFolder + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.spriteBorder != BigButtonBorder)
        {
            importer.spriteBorder = BigButtonBorder;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
