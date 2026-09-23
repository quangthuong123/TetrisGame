using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Setup Progress UI
// • Game over prefab: XP line in the results panel
// • StartMenu: CONTINUE button (mode select), PROFILE button + Profile screen (main menu)
// • GameScene + SinglePlayerScene: Esc pause menu (RESUME / SAVE & QUIT / QUIT)
public static class ProgressUISetup
{
    private const string GameOverPrefab = "Assets/Prefabs/SkillIcon/Game over.prefab";
    private const string PanelSprite = "Assets/Game Asset/BoardSkin/Panel.png";
    private const string MenuScene = "Assets/Scenes/StartMenu.unity";
    private static readonly string[] GameScenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/SinglePlayerScene.unity" };

    private static readonly Color Backdrop = new Color(0.12f, 0.07f, 0.30f, 0.95f);
    private static readonly Color CardColor = new Color(0.36f, 0.20f, 0.72f, 0.95f);
    private static readonly Color LightBlue = new Color(0.62f, 0.80f, 1f, 1f);
    private static readonly Color BarBack = new Color(0.12f, 0.07f, 0.30f, 1f);

    [MenuItem("Tools/Tetris/Setup Progress UI")]
    private static void Setup()
    {
        if (!EditorUtility.DisplayDialog("Setup Progress UI",
                "This updates and SAVES:\n\n" +
                "• Game over.prefab: XP line in the results panel\n" +
                "• StartMenu: CONTINUE button, PROFILE button and Profile screen\n" +
                "• GameScene + SinglePlayerScene: Esc pause menu\n\n" +
                "Run Tools > Tetris > Setup Match UI first (it builds the results panel).\n" +
                "Commit first if you want an easy way back.", "Set up", "Cancel")) return;
        if (!ButtonSkin.SpritesAvailable)
        {
            EditorUtility.DisplayDialog("Setup Progress UI", "Couldn't load the Big Buttons sprites.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;

        bool xpDone = AddXpLine();

        Scene menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        bool continueDone = AddContinueButton(menu);
        bool profileDone = AddProfile(menu);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);

        int pauseDone = 0;
        foreach (string path in GameScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (AddPauseMenu(scene)) pauseDone++;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Setup Progress UI",
            (xpDone ? "✓ XP line added to the results panel\n" : "• Results panel not found: run Setup Match UI, then this again\n") +
            (continueDone ? "✓ CONTINUE button on mode select\n" : "• Single Player button not found: CONTINUE skipped\n") +
            (profileDone ? "✓ PROFILE button + Profile screen on the main menu\n" : "• Main menu not found: Profile skipped\n") +
            $"✓ Pause menu in {pauseDone} of {GameScenes.Length} game scenes\n\nSafe to run again.", "OK");
    }

    // ---------- Result screen ----------

    private static bool AddXpLine()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameOverPrefab);
        try
        {
            Transform panel = root.transform.Find("ResultPanel");
            if (panel == null) return false;

            Transform old = panel.Find("XpText");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            TextMeshProUGUI xp = NewText("XpText", panel, "+0 XP  ·  LEVEL 1 ROOKIE (0%)", 30, LightBlue, TextAlignmentOptions.Center);
            xp.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            Transform note = panel.Find("NoteText");
            if (note != null) xp.transform.SetSiblingIndex(note.GetSiblingIndex()); // Just above the note line

            RectTransform panelRect = (RectTransform)panel;
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, 430f);

            PrefabUtility.SaveAsPrefabAsset(root, GameOverPrefab);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- Main menu ----------

    private static bool AddContinueButton(Scene scene)
    {
        Transform[] all = AllTransforms(scene);
        FusionLauncher launcher = Launcher(all);
        RectTransform single = all.FirstOrDefault(t => t.name == "Bth_SinglePlayer" || t.name == "Btn_SinglePlayer") as RectTransform;
        if (launcher == null || single == null) return false;

        Remove(all, "Btn_Continue");
        Button button = MenuButton("Btn_Continue", single.parent, "CONTINUE", new Vector2(single.anchoredPosition.x, 190f),
            new Vector2(448f, 110f), ButtonSkin.Purple, launcher.Button_Continue);
        button.gameObject.SetActive(false); // Only shown when there's a saved game

        Undo.RecordObject(launcher, "Setup Progress UI");
        launcher.continueButton = button;
        EditorUtility.SetDirty(launcher);
        return true;
    }

    private static bool AddProfile(Scene scene)
    {
        Transform[] all = AllTransforms(scene);
        FusionLauncher launcher = Launcher(all);
        RectTransform quit = all.FirstOrDefault(t => t.name == "Btn_quit") as RectTransform;
        Transform settingsPanel = all.FirstOrDefault(t => t.name == "SettingPanel");
        if (launcher == null || quit == null || settingsPanel == null) return false;

        Remove(all, "Btn_Profile");
        Remove(all, "ProfilePanel");

        // PROFILE button just under Quit
        MenuButton("Btn_Profile", quit.parent, "PROFILE", quit.anchoredPosition + new Vector2(0f, -150f),
            quit.sizeDelta, ButtonSkin.Purple, launcher.Button_OpenProfile);

        // Full-screen Profile page next to the Settings page
        RectTransform page = NewUI("ProfilePanel", settingsPanel.parent);
        Stretch(page, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        page.gameObject.AddComponent<Image>().color = Backdrop;

        RectTransform card = NewUI("ProfileCard", page);
        Stretch(card, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.92f), Vector2.zero, Vector2.zero);
        card.gameObject.AddComponent<Image>().color = CardColor;
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
        outline.effectDistance = new Vector2(5f, -5f);
        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(70, 70, 30, 30);
        layout.spacing = 10;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ProfileMenu profile = page.gameObject.AddComponent<ProfileMenu>();
        profile.launcher = launcher;
        Sized(NewText("Title", card, "PROFILE", 64, Color.white, TextAlignmentOptions.Center), 80);
        profile.nameText = Sized(NewText("Name", card, "Player", 44, Color.white, TextAlignmentOptions.Center), 56);
        profile.levelText = Sized(NewText("Level", card, "LEVEL 1  ·  ROOKIE", 34, Color.white, TextAlignmentOptions.Center), 46);

        // XP bar
        RectTransform bar = NewUI("XpBar", card);
        bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
        bar.gameObject.AddComponent<Image>().color = BarBack;
        RectTransform fill = NewUI("Fill", bar);
        Stretch(fill, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fillImage.color = LightBlue;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0.35f;
        profile.xpFill = fillImage;
        profile.xpText = Sized(NewText("XpText", card, "0 / 100 XP", 26, LightBlue, TextAlignmentOptions.Center), 38);

        // Two columns of stats
        RectTransform columns = NewUI("Stats", card);
        columns.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        HorizontalLayoutGroup columnsLayout = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        columnsLayout.spacing = 60;
        columnsLayout.childControlWidth = columnsLayout.childControlHeight = true;
        columnsLayout.childForceExpandWidth = columnsLayout.childForceExpandHeight = true;
        profile.matchesText = NewText("Matches", columns, "MATCHES", 30, Color.white, TextAlignmentOptions.TopLeft);
        profile.recordsText = NewText("Records", columns, "RECORDS", 30, Color.white, TextAlignmentOptions.TopLeft);
        profile.matchesText.lineSpacing = profile.recordsText.lineSpacing = 18;
        profile.matchesText.textWrappingMode = profile.recordsText.textWrappingMode = TextWrappingModes.Normal;

        // BACK
        RectTransform footer = NewUI("Footer", card);
        footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
        HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = footerLayout.childControlHeight = false;
        footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
        profile.backButton = MenuButton("Btn_ProfileBack", footer, "BACK", Vector2.zero, new Vector2(380f, 96f), ButtonSkin.Blue, null);

        page.gameObject.SetActive(false);
        Undo.RecordObject(launcher, "Setup Progress UI");
        launcher.profilePanel = page.gameObject;
        EditorUtility.SetDirty(launcher);
        return true;
    }

    // ---------- Pause menu ----------

    private static bool AddPauseMenu(Scene scene)
    {
        Transform[] all = AllTransforms(scene);
        Canvas canvas = all.Select(t => t.GetComponent<Canvas>())
            .FirstOrDefault(c => c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);
        if (canvas == null) return false;
        Remove(all, "PauseMenu");

        RectTransform root = NewUI("PauseMenu", canvas.transform);
        Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.SetAsLastSibling(); // Drawn over everything
        PauseMenu pause = root.gameObject.AddComponent<PauseMenu>();

        RectTransform dim = NewUI("PausePanel", root);
        Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0.05f, 0.65f); // Also blocks clicks behind it
        pause.panel = dim.gameObject;

        RectTransform card = NewUI("PauseCard", dim);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(640f, 600f);
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
        cardImage.type = Image.Type.Sliced;
        cardImage.color = cardImage.sprite != null ? Color.white : CardColor;
        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 40, 40);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        TextMeshProUGUI title = NewText("Title", card, "PAUSED", 64, Color.white, TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(520f, 80f);
        pause.resumeButton = MenuButton("Btn_Resume", card, "RESUME", Vector2.zero, new Vector2(440f, 100f), ButtonSkin.Purple, null);
        pause.saveQuitButton = MenuButton("Btn_SaveQuit", card, "SAVE & QUIT", Vector2.zero, new Vector2(440f, 100f), ButtonSkin.Blue, null);
        pause.quitButton = MenuButton("Btn_Quit", card, "QUIT (NO SAVE)", Vector2.zero, new Vector2(440f, 100f), ButtonSkin.Blue, null);
        pause.quitLabel = pause.quitButton.GetComponentInChildren<TMP_Text>(true);
        TextMeshProUGUI hint = NewText("Hint", card, "Game paused  ·  Esc to resume", 24, LightBlue, TextAlignmentOptions.Center);
        hint.rectTransform.sizeDelta = new Vector2(520f, 40f);
        pause.hintText = hint;

        dim.gameObject.SetActive(false);
        EditorUtility.SetDirty(pause);
        return true;
    }

    // ---------- Helpers ----------

    private static Transform[] AllTransforms(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

    private static FusionLauncher Launcher(Transform[] all) =>
        all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);

    private static void Remove(Transform[] all, string name)
    {
        foreach (Transform t in all.Where(t => t != null && t.name == name).ToArray())
        {
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }

    private static Button MenuButton(string name, Transform parent, string text, Vector2 position, Vector2 size, Sprite sprite, UnityAction onClick)
    {
        RectTransform rect = NewUI(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = NewText("Label", rect, text, Mathf.Clamp(size.y * 0.36f, 18f, 36f), Color.white, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ButtonSkin.Apply(button, sprite);
        if (onClick != null) UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return button;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TextMeshProUGUI Sized(TextMeshProUGUI text, float height)
    {
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
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
