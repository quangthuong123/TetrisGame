using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Tetris > Setup Skill Icons
// Puts the skill icons (Assets/Game Asset/SkillIcons) on Skill1-3 in the board prefab's SkillVessle,
// builds the SkillAlert pop-up that shows which skill the opponent used, and matches the SP bar colors.
public static class SkillIconSetup
{
    private const string IconFolder = "Assets/Game Asset/SkillIcons/";
    private static readonly string[] IconFiles = { "Skill1_BlockBreaker.png", "Skill2_ForcedZ.png", "Skill3_XBomb.png" };

    // Base colors of the three icons, reused for the SP bar tiers
    private static readonly Color[] TierColors =
    {
        new Color32(31, 153, 182, 255), // light blue
        new Color32(31, 77, 182, 255),  // blue
        new Color32(92, 31, 182, 255),  // purple
    };

    private static readonly Color AlertBackground = new Color(0.12f, 0.07f, 0.30f, 0.95f);
    private static readonly Color DetailColor = new Color(0.62f, 0.80f, 1f, 1f);

    [MenuItem("Tools/Tetris/Setup Skill Icons")]
    private static void Setup()
    {
        var icons = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + IconFiles[i]);
            if (icons[i] == null)
            {
                EditorUtility.DisplayDialog("Setup Skill Icons", $"Couldn't load {IconFolder}{IconFiles[i]} as a sprite.", "OK");
                return;
            }
        }

        RunOnBoard("Setup Skill Icons", ui =>
        {
            Apply(ui, icons);
            RestyleWarning(ui);
        },
            "• Skill1-3 show the Block Breaker / Forced Z / X-Bomb icons\n" +
            "• Skill pop-up and warning bubble over the board (fade after 2 s)\n" +
            "• SP bar colors match the icons");
    }

    [MenuItem("Tools/Tetris/Restyle Board Alerts")]
    private static void RestyleAlerts()
    {
        RunOnBoard("Restyle Board Alerts", ui =>
        {
            RestyleWarning(ui);
            ui.skillAlert = BuildAlert(ui);
            EditorUtility.SetDirty(ui);
        },
            "• Warning bubble: compact, readable, at the top of your board\n" +
            "• Skill pop-up: smaller, over the middle of your board\n" +
            "• Both pop in, stay 2 seconds, then fade out\n\n" +
            "They're invisible until they play. To preview one, set its CanvasGroup Alpha to 1 (and back to 0).");
    }

    // Runs the edit on the board prefab: in Prefab Mode if it's open there, otherwise on the asset directly
    private static void RunOnBoard(string title, System.Action<BoardUI> edit, string summary)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        BoardUI stageUI = stage != null ? stage.prefabContentsRoot.GetComponentInChildren<BoardUI>(true) : null;
        if (stageUI != null)
        {
            edit(stageUI);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            EditorUtility.DisplayDialog(title, $"Updated {stage.assetPath} (open in Prefab Mode: save it with Ctrl+S).\n\n{summary}", "OK");
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<BoardUI>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root.GetComponentInChildren<BoardUI>(true));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            EditorGUIUtility.PingObject(asset);
            EditorUtility.DisplayDialog(title, $"Updated {path}.\n\n{summary}\n\nSafe to run again.", "OK");
            return;
        }

        EditorUtility.DisplayDialog(title, "Couldn't find a prefab with a BoardUI component.", "OK");
    }

    // Turns the old stretched red Warning_Panel into a compact bubble at the top of the board
    private static void RestyleWarning(BoardUI ui)
    {
        GameObject panel = ui.warningPanel != null ? ui.warningPanel : FindChild(ui.transform, "Warning_Panel")?.gameObject;
        if (panel == null) return;
        Undo.RegisterFullObjectHierarchyUndo(panel, "Restyle Board Alerts");

        RectTransform canvasRect = (RectTransform)ui.transform;
        RectTransform rect = (RectTransform)panel.transform;
        rect.localScale = Vector3.one; // The old uneven scale is what squashed the text
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(8.6f, 1.6f);
        BoardFrame(ui, out Vector3 center, out float top);
        rect.position = new Vector3(center.x, top - 1.3f, canvasRect.position.z);
        panel.SetActive(true);

        Image background = panel.GetComponent<Image>();
        if (background == null) background = panel.AddComponent<Image>();
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = AlertBackground;
        background.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null) outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
        outline.effectDistance = new Vector2(0.06f, -0.06f);

        Canvas sorting = panel.GetComponent<Canvas>();
        if (sorting == null) sorting = panel.AddComponent<Canvas>();
        sorting.overrideSorting = true;
        sorting.sortingOrder = 100; // In front of the blocks

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        TMP_Text text = ui.warningText != null ? ui.warningText : panel.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            RectTransform textRect = text.rectTransform;
            textRect.localScale = Vector3.one;
            Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(0.25f, 0.1f), new Vector2(-0.25f, -0.1f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.35f;
            text.fontSizeMax = 0.8f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.85f, 0.3f, 1f); // Warm yellow reads well on the dark purple
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = "WARNING: FORCED Z INCOMING!";
        }

        WarningToast toast = panel.GetComponent<WarningToast>();
        if (toast == null) toast = panel.AddComponent<WarningToast>();
        toast.text = text;

        Undo.RecordObject(ui, "Restyle Board Alerts");
        ui.warningPanel = panel;
        ui.warningText = text as TextMeshProUGUI;
        ui.warningToast = toast;
        EditorUtility.SetDirty(ui);
    }

    // Center and top edge of the board, from the Vessel sprite
    private static void BoardFrame(BoardUI ui, out Vector3 center, out float top)
    {
        TetrisEngine engine = ui.engine != null ? ui.engine : ui.GetComponentInParent<TetrisEngine>(true);
        Transform board = engine != null ? engine.transform : ui.transform.root;
        Transform vessel = FindChild(board, "Vessel");
        SpriteRenderer sprite = vessel != null ? vessel.GetComponent<SpriteRenderer>() : null;

        if (sprite != null && sprite.sprite != null)
        {
            center = vessel.position;
            top = vessel.position.y + sprite.sprite.bounds.max.y * vessel.lossyScale.y;
        }
        else
        {
            center = board.position + new Vector3(4.5f, 9.5f, 0f);
            top = board.position.y + 19.5f;
        }
    }

    private static void Apply(BoardUI ui, Sprite[] icons)
    {
        Undo.RecordObject(ui, "Setup Skill Icons");

        Image[] slots = { ui.skill1Icon, ui.skill2Icon, ui.skill3Icon };
        for (int i = 0; i < 3; i++)
        {
            if (slots[i] == null) slots[i] = FindChild(ui.transform, "Skill" + (i + 1))?.GetComponent<Image>();
            if (slots[i] == null) continue;
            Undo.RecordObject(slots[i], "Setup Skill Icons");
            slots[i].sprite = icons[i];
            slots[i].type = Image.Type.Simple;
            slots[i].preserveAspect = true;
            slots[i].color = Color.white;
        }
        ui.skill1Icon = slots[0];
        ui.skill2Icon = slots[1];
        ui.skill3Icon = slots[2];
        ui.skillIcons = icons;

        ui.skillAlert = BuildAlert(ui);

        SkillPointBar bar = ui.spBar != null ? ui.spBar : ui.GetComponentInChildren<SkillPointBar>(true);
        if (bar != null)
        {
            Undo.RecordObject(bar, "Setup Skill Icons");
            bar.skill1ReadyColor = TierColors[0];
            bar.skill2ReadyColor = TierColors[1];
            bar.skill3ReadyColor = TierColors[2];
            EditorUtility.SetDirty(bar);
        }
        EditorUtility.SetDirty(ui);
    }

    private static SkillAlert BuildAlert(BoardUI ui)
    {
        Transform old = FindChild(ui.transform, "SkillAlert");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        RectTransform canvasRect = (RectTransform)ui.transform;
        RectTransform alert = NewUI("SkillAlert", canvasRect);
        alert.sizeDelta = new Vector2(8.2f, 2.4f);

        // Over the middle of your board, in front of the blocks (the warning bubble sits at the top)
        BoardFrame(ui, out Vector3 center, out _);
        alert.position = new Vector3(center.x, center.y + 3f, canvasRect.position.z);

        Canvas sorting = alert.gameObject.AddComponent<Canvas>();
        sorting.overrideSorting = true;
        sorting.sortingOrder = 100;
        CanvasGroup group = alert.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; // Hidden until an alert plays
        group.blocksRaycasts = false;

        Image background = alert.gameObject.AddComponent<Image>();
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = AlertBackground;
        background.raycastTarget = false;
        Outline outline = alert.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
        outline.effectDistance = new Vector2(0.07f, -0.07f);

        RectTransform iconRect = NewUI("Icon", alert);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(1.9f, 1.9f);
        iconRect.anchoredPosition = new Vector2(0.25f, 0f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text title = NewText("Title", alert, 0.7f, true, Color.white, TextAlignmentOptions.BottomLeft);
        Stretch(title.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(2.4f, 0f), new Vector2(-0.25f, -0.2f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 0.35f;
        title.fontSizeMax = 0.7f;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.text = "OPPONENT USED X-BOMB!";

        TMP_Text detail = NewText("Detail", alert, 0.45f, false, DetailColor, TextAlignmentOptions.TopLeft);
        Stretch(detail.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(2.4f, 0.2f), new Vector2(-0.25f, -0.05f));
        detail.enableAutoSizing = true;
        detail.fontSizeMin = 0.28f;
        detail.fontSizeMax = 0.45f;
        detail.text = "Your next piece is the X block";

        SkillAlert component = alert.gameObject.AddComponent<SkillAlert>();
        component.icon = icon;
        component.title = title;
        component.detail = detail;

        Undo.RegisterCreatedObjectUndo(alert.gameObject, "Setup Skill Icons");
        return component;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_Text NewText(string name, Transform parent, float size, bool bold, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI text = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
