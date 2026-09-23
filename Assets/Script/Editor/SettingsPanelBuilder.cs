using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Build Settings Panel
// Rebuilds StartMenu's SettingPanel with Audio / Display / Gameplay / Controls settings in the
// purple / blue / white style, and wires everything to SettingsMenu. Safe to run again (it rebuilds).
public static class SettingsPanelBuilder
{
    private const string MenuScenePath = "Assets/Scenes/StartMenu.unity";
    private const string CardName = "SettingsCard";
    private const string UndoName = "Build Settings Panel";

    private static readonly Color PanelBackground = new Color(0.12f, 0.07f, 0.30f, 0.95f);
    private static readonly Color CardColor = new Color(0.36f, 0.20f, 0.72f, 0.95f);
    private static readonly Color SectionColor = new Color(0.62f, 0.80f, 1.00f, 1f); // light blue headings
    private static readonly Color TrackColor = new Color(0.12f, 0.07f, 0.30f, 1f);
    private static readonly Color FillColor = new Color(0.55f, 0.78f, 1.00f, 1f);
    private static readonly Color OutlineWhite = new Color(1f, 1f, 1f, 0.9f);

    private const float RowHeight = 52f;
    private const float LabelFontSize = 28f;
    private const float ControlWidth = 340f;
    private const float ValueWidth = 120f;

    private static DefaultControls.Resources _ui;
    private static TMP_DefaultControls.Resources _tmpUi;

    [MenuItem("Tools/Tetris/Build Settings Panel")]
    private static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Settings Panel",
                "This rebuilds the Settings panel in StartMenu:\n\n" +
                "• Audio: master, music and sound effects volume\n" +
                "• Display: fullscreen, resolution, VSync, frame rate limit\n" +
                "• Gameplay: DAS / ARR handling, ghost piece, AI difficulty\n" +
                "• Controls list, Reset Defaults and a working Back button\n\n" +
                "The old volume slider and the 'Btn_Back' text are replaced, and the menu canvas is set to " +
                "scale with screen size (1920x1080) so the menus fit any resolution.",
                "Build it", "Cancel")) return;

        if (SceneManager.GetActiveScene().path != MenuScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        }

        GameObject panel = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "SettingPanel")?.gameObject;
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Build Settings Panel", "Couldn't find SettingPanel in StartMenu.", "OK");
            return;
        }

        LoadResources();
        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        FusionLauncher launcher = Object.FindFirstObjectByType<FusionLauncher>(FindObjectsInactive.Include);
        ScaleCanvasWithScreen(panel);
        RemoveOldControls(panel, launcher);

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            Undo.RecordObject(panelImage, UndoName);
            panelImage.color = PanelBackground;
        }

        // Card
        RectTransform card = NewUI(CardName, panel.transform);
        card.anchorMin = new Vector2(0.08f, 0.05f);
        card.anchorMax = new Vector2(0.92f, 0.95f);
        card.offsetMin = card.offsetMax = Vector2.zero;
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = CardColor;
        AddOutline(card.gameObject, 5f);
        VerticalLayoutGroup cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(60, 60, 30, 30);
        cardLayout.spacing = 16;
        cardLayout.childControlWidth = cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        TMP_Text title = NewText("Title", card, "SETTINGS", 64, TextAlignmentOptions.Center, Color.white, true);
        Layout(title.gameObject, preferredHeight: 80);

        // Page tabs: GENERAL | CONTROLS
        RectTransform tabs = NewUI("Tabs", card);
        Layout(tabs.gameObject, preferredHeight: 70);
        HorizontalLayoutGroup tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 30;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = tabsLayout.childForceExpandHeight = false;
        Button generalTab = NewButton("Tab_General", tabs, "GENERAL", ButtonSkin.Purple, 300, 66, 30);
        Button controlsTab = NewButton("Tab_Controls", tabs, "CONTROLS", ButtonSkin.Purple, 300, 66, 30);

        // GENERAL page: two columns
        RectTransform columns = NewUI("Columns", card);
        Layout(columns.gameObject, flexibleHeight: 1);
        HorizontalLayoutGroup columnsLayout = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        columnsLayout.spacing = 80;
        columnsLayout.childControlWidth = columnsLayout.childControlHeight = true;
        columnsLayout.childForceExpandWidth = columnsLayout.childForceExpandHeight = true;

        RectTransform left = Column("LeftColumn", columns);
        RectTransform right = Column("RightColumn", columns);

        SettingsMenu menu = panel.GetComponent<SettingsMenu>();
        if (menu == null) menu = Undo.AddComponent<SettingsMenu>(panel);
        Undo.RecordObject(menu, UndoName);
        menu.launcher = launcher;

        Section("AUDIO", left);
        (menu.masterSlider, menu.masterValue) = SliderRow("Master Volume", left);
        (menu.musicSlider, menu.musicValue) = SliderRow("Music", left);
        (menu.sfxSlider, menu.sfxValue) = SliderRow("Sound Effects", left);

        Section("DISPLAY", left);
        menu.fullscreenToggle = ToggleRow("Fullscreen", left);
        menu.resolutionDropdown = DropdownRow("Resolution", left);
        menu.vsyncToggle = ToggleRow("VSync", left);
        menu.frameRateDropdown = DropdownRow("Frame Rate Limit", left);

        Section("GAMEPLAY", right);
        (menu.dasSlider, menu.dasValue) = SliderRow("Auto-Repeat Delay (DAS)", right);
        (menu.arrSlider, menu.arrValue) = SliderRow("Auto-Repeat Rate (ARR)", right);
        menu.ghostToggle = ToggleRow("Ghost Piece", right);
        menu.aiDifficultyDropdown = DropdownRow("AI Difficulty (Single Player)", right);

        // CONTROLS page: every action with a click-to-rebind key button
        RectTransform controlsPage = NewUI("ControlsPage", card);
        Layout(controlsPage.gameObject, flexibleHeight: 1);
        HorizontalLayoutGroup controlsLayout = controlsPage.gameObject.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 80;
        controlsLayout.childControlWidth = controlsLayout.childControlHeight = true;
        controlsLayout.childForceExpandWidth = controlsLayout.childForceExpandHeight = true;
        RectTransform keysLeft = Column("KeysLeft", controlsPage);
        RectTransform keysRight = Column("KeysRight", controlsPage);

        Section("MOVEMENT", keysLeft);
        var actions = (GameAction[])System.Enum.GetValues(typeof(GameAction));
        for (int i = 0; i < actions.Length; i++)
        {
            if (i == 6) Section("HOLD & SKILLS", keysRight);
            KeyRow(actions[i], i < 6 ? keysLeft : keysRight);
        }
        TMP_Text hint = NewText("Hint", keysRight, "Click a key, then press the new one.\nEsc cancels. Keys already in use swap places.",
            22, TextAlignmentOptions.TopLeft, SectionColor, false);
        Layout(hint.gameObject, preferredHeight: 90);

        menu.generalPage = columns.gameObject;
        menu.controlsPage = controlsPage.gameObject;
        menu.generalTab = generalTab;
        menu.controlsTab = controlsTab;
        controlsPage.gameObject.SetActive(false);

        // Footer buttons
        RectTransform footer = NewUI("Footer", card);
        Layout(footer.gameObject, preferredHeight: 100);
        HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 60;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;

        menu.resetButton = NewButton("Btn_ResetDefaults", footer, "RESET DEFAULTS", ButtonSkin.Purple);
        menu.backButton = NewButton("Btn_SettingsBack", footer, "BACK", ButtonSkin.Blue);

        Undo.RegisterCreatedObjectUndo(card.gameObject, UndoName);
        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        EditorSceneManager.SaveScene(panel.scene);
        Selection.activeGameObject = panel;

        EditorUtility.DisplayDialog("Build Settings Panel",
            "Settings panel built and StartMenu saved.\n\n" +
            "SettingPanel is hidden until Settings is clicked; tick it active in the Hierarchy to preview " +
            "(untick before playing). Ctrl+Z undoes the build.", "OK");
    }

    // ---------- Scene setup ----------

    private static void ScaleCanvasWithScreen(GameObject panel)
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;
        CanvasScaler scaler = canvas.rootCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;
        Undo.RecordObject(scaler, UndoName);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void RemoveOldControls(GameObject panel, FusionLauncher launcher)
    {
        foreach (string name in new[] { CardName, "Volume_Slider", "Btn_Back" })
        {
            Transform old = panel.transform.Cast<Transform>().FirstOrDefault(t => t.name == name);
            if (old == null) continue;
            if (launcher != null && launcher.volumeSlider != null && launcher.volumeSlider.transform == old)
            {
                Undo.RecordObject(launcher, UndoName);
                launcher.volumeSlider = null; // Volume lives in the new panel now
            }
            Undo.DestroyObjectImmediate(old.gameObject);
        }
    }

    private static void LoadResources()
    {
        Sprite standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        Sprite arrow = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        Sprite mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
        Sprite input = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");

        _ui = new DefaultControls.Resources
        {
            standard = standard, background = background, knob = knob, checkmark = checkmark,
            dropdown = arrow, mask = mask, inputField = input
        };
        _tmpUi = new TMP_DefaultControls.Resources
        {
            standard = standard, background = background, knob = knob, checkmark = checkmark,
            dropdown = arrow, mask = mask, inputField = input
        };
    }

    // ---------- Layout pieces ----------

    private static RectTransform Column(string name, Transform parent)
    {
        RectTransform column = NewUI(name, parent);
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }

    private static void Section(string text, Transform parent)
    {
        TMP_Text heading = NewText(text + "_Heading", parent, text, 34, TextAlignmentOptions.BottomLeft, SectionColor, true);
        Layout(heading.gameObject, preferredHeight: 56);
    }

    private static RectTransform Row(string label, Transform parent)
    {
        RectTransform row = NewUI(label + "_Row", parent);
        Layout(row.gameObject, preferredHeight: RowHeight);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        TMP_Text text = NewText("Label", row, label, LabelFontSize, TextAlignmentOptions.MidlineLeft, Color.white, false);
        Layout(text.gameObject, preferredHeight: RowHeight, flexibleWidth: 1, minWidth: 200);
        return row;
    }

    private static (Slider, TMP_Text) SliderRow(string label, Transform parent)
    {
        RectTransform row = Row(label, parent);

        GameObject go = DefaultControls.CreateSlider(_ui);
        go.name = "Slider";
        go.transform.SetParent(row, false);
        Layout(go, preferredWidth: ControlWidth, preferredHeight: 28);

        Slider slider = go.GetComponent<Slider>();
        go.transform.Find("Background").GetComponent<Image>().color = TrackColor;
        slider.fillRect.GetComponent<Image>().color = FillColor;
        slider.handleRect.GetComponent<Image>().color = Color.white;
        ((RectTransform)slider.handleRect).sizeDelta = new Vector2(28, 12);

        TMP_Text value = NewText("Value", row, "100%", 26, TextAlignmentOptions.MidlineRight, SectionColor, true);
        Layout(value.gameObject, preferredWidth: ValueWidth, preferredHeight: RowHeight);
        return (slider, value);
    }

    private static Toggle ToggleRow(string label, Transform parent)
    {
        RectTransform row = Row(label, parent);

        GameObject go = DefaultControls.CreateToggle(_ui);
        go.name = "Toggle";
        go.transform.SetParent(row, false);
        Layout(go, preferredWidth: 40, preferredHeight: 40);

        // Drop the built-in legacy label; the row already has one
        Transform builtInLabel = go.transform.Find("Label");
        if (builtInLabel != null) Object.DestroyImmediate(builtInLabel.gameObject);

        RectTransform box = (RectTransform)go.transform.Find("Background");
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.one;
        box.offsetMin = box.offsetMax = Vector2.zero;
        box.GetComponent<Image>().color = Color.white;
        RectTransform check = (RectTransform)box.Find("Checkmark");
        check.anchorMin = Vector2.zero;
        check.anchorMax = Vector2.one;
        check.offsetMin = new Vector2(4, 4);
        check.offsetMax = new Vector2(-4, -4);
        check.GetComponent<Image>().color = CardColor;

        // Keep the toggle lined up with the right edge like the other controls
        RectTransform spacer = NewUI("Spacer", row);
        Layout(spacer.gameObject, preferredWidth: ControlWidth + ValueWidth - 40, preferredHeight: 1);
        return go.GetComponent<Toggle>();
    }

    private static TMP_Dropdown DropdownRow(string label, Transform parent)
    {
        RectTransform row = Row(label, parent);

        GameObject go = TMP_DefaultControls.CreateDropdown(_tmpUi);
        go.name = "Dropdown";
        go.transform.SetParent(row, false);
        Layout(go, preferredWidth: ControlWidth + ValueWidth + 16, preferredHeight: 44);

        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.captionText.fontSize = 24;
        dropdown.captionText.color = TrackColor;
        dropdown.itemText.fontSize = 24;
        dropdown.itemText.color = TrackColor;
        RectTransform item = (RectTransform)dropdown.itemText.transform.parent;
        item.sizeDelta = new Vector2(item.sizeDelta.x, 40);
        dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 240);
        dropdown.ClearOptions();
        return dropdown;
    }

    // A row: action name on the left, its key button on the right
    private static void KeyRow(GameAction action, Transform parent)
    {
        RectTransform row = Row(KeyBindings.DisplayName(action), parent);
        Button button = NewButton("Key_" + action, row, KeyBindings.KeyName(KeyBindings.Get(action)), ButtonSkin.Blue, 220, 50, 26);
        KeyRebindButton rebind = button.gameObject.AddComponent<KeyRebindButton>();
        rebind.action = action;
        rebind.keyLabel = button.GetComponentInChildren<TMP_Text>(true);
    }

    private static Button NewButton(string name, Transform parent, string label, Sprite sprite,
        float width = 380, float height = 96, float fontSize = 34)
    {
        RectTransform rect = NewUI(name, parent);
        Layout(rect.gameObject, preferredWidth: width, preferredHeight: height);
        rect.sizeDelta = new Vector2(width, height);
        rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();

        TMP_Text text = NewText("Label", rect, label, fontSize, TextAlignmentOptions.Center, Color.white, true);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        text.raycastTarget = false;

        ButtonSkin.Apply(button, sprite);
        return button;
    }

    // ---------- Helpers ----------

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color, bool bold)
    {
        RectTransform rect = NewUI(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Layout(GameObject go, float preferredWidth = -1, float preferredHeight = -1,
        float flexibleWidth = -1, float flexibleHeight = -1, float minWidth = -1)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
        element.flexibleWidth = flexibleWidth;
        element.flexibleHeight = flexibleHeight;
        element.minWidth = minWidth;
    }

    private static void AddOutline(GameObject go, float thickness)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = OutlineWhite;
        outline.effectDistance = new Vector2(thickness, -thickness);
        outline.useGraphicAlpha = false;
    }
}
