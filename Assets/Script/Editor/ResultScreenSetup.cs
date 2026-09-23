using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Setup Match UI
// 1. Game over prefab: results panel (YOU WIN / score / high score / note) and a REMATCH button next to RETURN,
//    hooked up to GameOverManager in both game scenes.
// 2. Main menu: its own high score text (it was pointing at a text inside the lobby).
// 3. Mode select: room code box + PRIVATE ROOM button for playing with friends.
public static class ResultScreenSetup
{
    private const string GameOverPrefab = "Assets/Prefabs/SkillIcon/Game over.prefab";
    private const string PanelSprite = "Assets/Game Asset/BoardSkin/Panel.png";
    private const string MenuScene = "Assets/Scenes/StartMenu.unity";
    private static readonly string[] GameScenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/SinglePlayerScene.unity" };

    private const string ResultPanelName = "ResultPanel";
    private const string RematchButtonName = "RematchButton";
    private const string MenuHighScoreName = "MenuHighScore_Text";
    private const string RoomCodeInputName = "RoomCode_Input";
    private const string PrivateRoomButtonName = "Btn_PrivateRoom";
    private const string LeaderboardPanelName = "LeaderboardPanel";
    private const string SprintButtonName = "Btn_Sprint";
    private const string UltraButtonName = "Btn_Ultra";

    [MenuItem("Tools/Tetris/Setup Match UI")]
    private static void Setup()
    {
        if (!EditorUtility.DisplayDialog("Setup Match UI",
                "This updates and SAVES:\n\n" +
                "• Game over.prefab: results panel + REMATCH button\n" +
                "• GameScene + SinglePlayerScene: connects them to GameOverManager\n" +
                "• StartMenu: main menu high score, and a room code box + PRIVATE ROOM button\n\n" +
                "Commit first if you want an easy way back.", "Set up", "Cancel")) return;
        if (!ButtonSkin.SpritesAvailable)
        {
            EditorUtility.DisplayDialog("Setup Match UI", "Couldn't load the Big Buttons sprites.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;

        BuildGameOverPrefab();

        int hooked = 0;
        foreach (string path in GameScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (HookGameOverManager(scene)) hooked++;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Scene menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        bool highScoreDone = AddMenuHighScore(menu);
        bool privateDone = AddPrivateRoomControls(menu);
        bool modesDone = AddModeButtons(menu);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);

        if (!string.IsNullOrEmpty(startScene) && startScene != MenuScene) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Setup Match UI",
            "Results panel + REMATCH button added to Game over.prefab.\n" +
            $"GameOverManager connected in {hooked} of {GameScenes.Length} game scenes.\n" +
            (highScoreDone ? "Main menu high score added.\n" : "Main menu title not found: high score skipped.\n") +
            (privateDone ? "Room code box + PRIVATE ROOM button added under Multi Player.\n" : "Btn_MultiPlayer not found: private room controls skipped.\n") +
            (modesDone ? "SPRINT and ULTRA buttons added under Single Player.\n" : "Bth_SinglePlayer not found: mode buttons skipped.\n") +
            "\nSafe to run again.", "OK");
    }

    // ---------- Game over prefab ----------

    private static void BuildGameOverPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameOverPrefab);
        try
        {
            foreach (string name in new[] { ResultPanelName, RematchButtonName, LeaderboardPanelName })
            {
                Transform old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // Between the painted GAME OVER title and the buttons (panel is 1920x1080)
            RectTransform panel = NewUI(ResultPanelName, root.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(860f, 380f);
            panel.anchoredPosition = new Vector2(0f, 50f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
            background.type = Image.Type.Sliced;
            background.color = background.sprite != null ? Color.white : new Color(0.1f, 0.06f, 0.25f, 0.9f);
            background.raycastTarget = false;

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 28, 28);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            NewText("ResultText", panel, "YOU WIN!", 84, Color.white, 100, true);
            NewText("FinalScoreText", panel, "YOUR SCORE: 0", 50, Color.white, 64, true);
            NewText("HighScoreText", panel, "HIGH SCORE: 0", 38, new Color(0.62f, 0.80f, 1f, 1f), 50, true);
            NewText("NoteText", panel, "", 30, Color.white, 44, false);

            // World top 10 (Sprint / Ultra only; GameOverManager hides it in versus)
            RectTransform board = NewUI(LeaderboardPanelName, root.transform);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(440f, 520f);
            board.anchoredPosition = new Vector2(700f, 50f);
            Image boardBackground = board.gameObject.AddComponent<Image>();
            boardBackground.sprite = background.sprite;
            boardBackground.type = Image.Type.Sliced;
            boardBackground.color = background.color;
            boardBackground.raycastTarget = false;
            TextMeshProUGUI boardText = NewUI("LeaderboardText", board).gameObject.AddComponent<TextMeshProUGUI>();
            boardText.rectTransform.anchorMin = Vector2.zero;
            boardText.rectTransform.anchorMax = Vector2.one;
            boardText.rectTransform.offsetMin = new Vector2(30f, 26f);
            boardText.rectTransform.offsetMax = new Vector2(-30f, -26f);
            boardText.text = "<b>WORLD TOP 10</b>";
            boardText.fontSize = 36;
            boardText.alignment = TextAlignmentOptions.Top;
            boardText.color = Color.white;
            boardText.raycastTarget = false;

            // RETURN on the left, REMATCH (a copy of it) on the right
            Button returnButton = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name != RematchButtonName);
            if (returnButton != null)
            {
                RectTransform returnRect = (RectTransform)returnButton.transform;
                returnRect.anchoredPosition = new Vector2(-290f, -250f);
                returnRect.sizeDelta = new Vector2(480f, 130f);

                GameObject rematch = Object.Instantiate(returnButton.gameObject, returnButton.transform.parent);
                rematch.name = RematchButtonName;
                RectTransform rematchRect = (RectTransform)rematch.transform;
                rematchRect.anchoredPosition = new Vector2(290f, -250f);
                Button rematchButton = rematch.GetComponent<Button>();
                rematchButton.onClick = new Button.ButtonClickedEvent(); // GameOverManager hooks it up at runtime
                TMP_Text label = rematch.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "REMATCH";
                ButtonSkin.Apply(rematchButton, ButtonSkin.Purple);
                ButtonSkin.Apply(returnButton, ButtonSkin.Blue);

                returnButton.transform.SetAsLastSibling();
                rematch.transform.SetAsLastSibling();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GameOverPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool HookGameOverManager(Scene scene)
    {
        GameOverManager manager = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<GameOverManager>(true))
            .FirstOrDefault();
        if (manager == null || manager.gameOverPanel == null) return false;

        TextMeshProUGUI Text(string name) => manager.gameOverPanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == name);

        Undo.RecordObject(manager, "Setup Match UI");
        manager.resultText = Text("ResultText");
        manager.finalScoreText = Text("FinalScoreText");
        manager.highScoreText = Text("HighScoreText");
        manager.noteText = Text("NoteText");
        manager.rematchButton = manager.gameOverPanel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == RematchButtonName);
        EditorUtility.SetDirty(manager);
        return manager.resultText != null && manager.rematchButton != null;
    }

    // ---------- Main menu ----------

    private static bool AddMenuHighScore(Scene scene)
    {
        Transform[] all = AllTransforms(scene);
        FusionLauncher launcher = all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);
        RectTransform title = all.FirstOrDefault(t => t.name == "Title_text") as RectTransform;
        if (launcher == null || title == null) return false;

        Transform old = all.FirstOrDefault(t => t.name == MenuHighScoreName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        RectTransform rect = NewUI(MenuHighScoreName, title.parent);
        rect.anchorMin = title.anchorMin;
        rect.anchorMax = title.anchorMax;
        rect.pivot = title.pivot;
        rect.sizeDelta = new Vector2(600f, 60f);
        rect.anchoredPosition = title.anchoredPosition + new Vector2(0f, -90f);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "HIGH SCORE: 0";
        text.fontSize = 40;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.85f, 0.3f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        // It used to point at Stats_Text inside the lobby, which the lobby status overwrote
        Undo.RecordObject(launcher, "Setup Match UI");
        launcher.menuHighScoreText = text;
        EditorUtility.SetDirty(launcher);
        return true;
    }

    private static bool AddPrivateRoomControls(Scene scene)
    {
        Transform[] all = AllTransforms(scene);
        FusionLauncher launcher = all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);
        RectTransform multi = all.FirstOrDefault(t => t.name == "Btn_MultiPlayer") as RectTransform;
        if (launcher == null || multi == null) return false;

        foreach (string name in new[] { RoomCodeInputName, PrivateRoomButtonName })
        {
            Transform old = all.FirstOrDefault(t => t != null && t.name == name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        Transform parent = multi.parent;
        float x = multi.anchoredPosition.x;

        // Room code box under the Multi Player button
        var tmpResources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };
        GameObject inputGo = TMP_DefaultControls.CreateInputField(tmpResources);
        inputGo.name = RoomCodeInputName;
        inputGo.layer = parent.gameObject.layer;
        RectTransform inputRect = (RectTransform)inputGo.transform;
        inputRect.SetParent(parent, false);
        inputRect.anchorMin = inputRect.anchorMax = inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(448f, 70f);
        inputRect.anchoredPosition = new Vector2(x, -190f);
        TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
        input.characterLimit = 12;
        input.contentType = TMP_InputField.ContentType.Alphanumeric;
        input.textComponent.fontSize = 34;
        input.textComponent.alignment = TextAlignmentOptions.Center;
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "ROOM CODE (empty = new room)";
            placeholder.fontSize = 26;
            placeholder.alignment = TextAlignmentOptions.Center;
        }

        // PRIVATE ROOM button below it
        RectTransform buttonRect = NewUI(PrivateRoomButtonName, parent);
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(448f, 100f);
        buttonRect.anchoredPosition = new Vector2(x, -295f);
        buttonRect.gameObject.AddComponent<Image>();
        Button button = buttonRect.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = NewUI("Label", buttonRect).gameObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.text = "PRIVATE ROOM";
        label.fontSize = 34;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        ButtonSkin.Apply(button, ButtonSkin.Blue);
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(launcher.Button_PrivateRoom));

        Undo.RecordObject(launcher, "Setup Match UI");
        launcher.roomCodeInput = input;
        EditorUtility.SetDirty(launcher);
        return true;
    }

    // SPRINT 40L and ULTRA 2:00 under the Single Player button (mirrors the private room controls)
    private static bool AddModeButtons(Scene scene)
    {
        Transform[] all = AllTransforms(scene);
        FusionLauncher launcher = all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);
        RectTransform single = all.FirstOrDefault(t => t.name == "Bth_SinglePlayer" || t.name == "Btn_SinglePlayer") as RectTransform;
        if (launcher == null || single == null) return false;

        foreach (string name in new[] { SprintButtonName, UltraButtonName })
        {
            Transform old = all.FirstOrDefault(t => t != null && t.name == name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        MenuButton(SprintButtonName, single.parent, "SPRINT 40L", new Vector2(single.anchoredPosition.x, -190f), ButtonSkin.Purple, launcher.Button_Sprint);
        MenuButton(UltraButtonName, single.parent, "ULTRA 2:00", new Vector2(single.anchoredPosition.x, -300f), ButtonSkin.Purple, launcher.Button_Ultra);
        return true;
    }

    private static void MenuButton(string name, Transform parent, string text, Vector2 position, Sprite sprite, UnityAction onClick)
    {
        RectTransform rect = NewUI(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(448f, 100f);
        rect.anchoredPosition = position;
        rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = NewUI("Label", rect).gameObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.text = text;
        label.fontSize = 34;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        ButtonSkin.Apply(button, sprite);
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
    }

    // ---------- Helpers ----------

    private static Transform[] AllTransforms(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void NewText(string name, Transform parent, string text, float size, Color color, float height, bool bold)
    {
        TextMeshProUGUI tmp = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        LayoutElement element = tmp.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
    }
}
