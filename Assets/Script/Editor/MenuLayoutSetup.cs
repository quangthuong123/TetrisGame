using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Rebuild Mode Menus
// • Main menu: real game title, proper PLAY / PROFILE / QUIT buttons (keeps your positions)
// • SELECT MODE: two big buttons, SINGLE PLAYER and MULTIPLAYER, plus BACK
// • New SINGLE PLAYER screen: CONTINUE / VS AI / SPRINT 40L / ULTRA 2:00 / BACK
// • New MULTIPLAYER screen: QUICK MATCH / room code / CREATE OR JOIN ROOM / BACK
public static class MenuLayoutSetup
{
    private const string MenuScene = "Assets/Scenes/StartMenu.unity";
    private const string PlaceholderProductName = "My project (6)";
    private const string FallbackTitle = "NEON BLOCKS ONLINE";

    private static readonly Color LightBlue = new Color(0.62f, 0.80f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);

    private const float WideButtonWidth = 620f;
    private const float WideButtonHeight = 118f;

    [MenuItem("Tools/Tetris/Rebuild Mode Menus")]
    private static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Rebuild Mode Menus",
                "This updates and SAVES StartMenu:\n\n" +
                "• Main menu: game title + PLAY / PROFILE / QUIT buttons\n" +
                "• SELECT MODE: SINGLE PLAYER and MULTIPLAYER buttons\n" +
                "• New SINGLE PLAYER screen (Continue, vs AI, Sprint, Ultra)\n" +
                "• New MULTIPLAYER screen (Quick Match, room code)\n\n" +
                "Commit first if you want an easy way back.", "Rebuild", "Cancel")) return;
        if (!ButtonSkin.SpritesAvailable)
        {
            EditorUtility.DisplayDialog("Rebuild Mode Menus", "Couldn't load the Big Buttons sprites.", "OK");
            return;
        }
        if (SceneManager.GetActiveScene().path != MenuScene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }

        Scene scene = SceneManager.GetActiveScene();
        Transform[] all = AllTransforms(scene);
        FusionLauncher launcher = all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);
        if (launcher == null || launcher.modeSelectPanel == null)
        {
            EditorUtility.DisplayDialog("Rebuild Mode Menus", "Couldn't find FusionLauncher and its mode select panel.", "OK");
            return;
        }

        string report = "";
        report += FixMainMenu(all) ? "✓ Main menu title and buttons\n" : "• Main menu not found\n";
        RectTransform modeSelect = (RectTransform)launcher.modeSelectPanel.transform;
        FixModeSelect(modeSelect, launcher);
        report += "✓ SELECT MODE screen\n";

        // Old per-mode controls on SELECT MODE move to the new screens
        foreach (string name in new[] { "Btn_Sprint", "Btn_Ultra", "Btn_Continue", "RoomCode_Input", "Btn_PrivateRoom", "SinglePlayerPanel", "MultiplayerPanel" })
        {
            foreach (Transform t in AllTransforms(scene).Where(t => t != null && t.name == name).ToArray())
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        BuildSinglePlayerPanel(modeSelect, launcher);
        BuildMultiplayerPanel(modeSelect, launcher);
        report += "✓ SINGLE PLAYER and MULTIPLAYER screens\n";

        EditorUtility.SetDirty(launcher);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Rebuild Mode Menus", report + "\nStartMenu saved. Safe to run again.\n\n" +
            "The screens are hidden until used; tick one active in the Hierarchy to preview it.", "OK");
    }

    // ---------- Main menu ----------

    private static bool FixMainMenu(Transform[] all)
    {
        RectTransform title = Find(all, "Title_text");
        if (title == null) return false;

        string gameName = PlayerSettings.productName == PlaceholderProductName ? FallbackTitle : PlayerSettings.productName.ToUpperInvariant();
        TMP_Text titleText = title.GetComponent<TMP_Text>();
        if (titleText != null)
        {
            titleText.text = gameName;
            titleText.fontSize = 96;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 40;
            titleText.fontSizeMax = 96;
        }
        title.sizeDelta = new Vector2(1400f, 140f);

        RectTransform highScore = Find(all, "MenuHighScore_Text");
        if (highScore != null) highScore.anchoredPosition = title.anchoredPosition + new Vector2(0f, -120f);

        FixButton(Find(all, "Btn_Play"), "PLAY", new Vector2(340f, 96f), ButtonSkin.Purple);
        FixButton(Find(all, "Btn_Profile"), "PROFILE", new Vector2(340f, 96f), ButtonSkin.Purple);
        FixButton(Find(all, "Btn_quit"), "QUIT", new Vector2(340f, 96f), ButtonSkin.Blue);
        return true;
    }

    // ---------- SELECT MODE ----------

    private static void FixModeSelect(RectTransform panel, FusionLauncher launcher)
    {
        // Heading ("Game Mode Select")
        TMP_Text heading = panel.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t => t.transform.parent == panel && t.GetComponentInParent<Button>() == null);
        if (heading != null) StyleHeading(heading, "SELECT MODE");

        RectTransform single = FindChild(panel, "Bth_SinglePlayer") ?? FindChild(panel, "Btn_SinglePlayer");
        RectTransform multi = FindChild(panel, "Btn_MultiPlayer");
        RectTransform back = FindChild(panel, "Btn_Back");

        if (single != null)
        {
            single.anchoredPosition = new Vector2(-420f, 10f);
            FixButton(single, "SINGLE PLAYER\n<size=45%>vs AI  ·  Sprint  ·  Ultra</size>", new Vector2(640f, 280f), ButtonSkin.Purple, 64f);
            Rewire(single.GetComponent<Button>(), launcher.Button_OpenSinglePlayerMenu);
        }
        if (multi != null)
        {
            multi.anchoredPosition = new Vector2(420f, 10f);
            FixButton(multi, "MULTIPLAYER\n<size=45%>Quick match  ·  Private room</size>", new Vector2(640f, 280f), ButtonSkin.Purple, 64f);
            Rewire(multi.GetComponent<Button>(), launcher.Button_OpenMultiplayerMenu);
        }
        if (back != null)
        {
            back.anchoredPosition = new Vector2(0f, -380f);
            FixButton(back, "BACK", new Vector2(360f, 100f), ButtonSkin.Blue);
        }
    }

    // ---------- SINGLE PLAYER ----------

    private static void BuildSinglePlayerPanel(RectTransform modeSelect, FusionLauncher launcher)
    {
        RectTransform panel = NewScreen("SinglePlayerPanel", modeSelect);
        StyleHeading(NewText("Heading", panel, "", 80, Color.white), "SINGLE PLAYER");
        RectTransform list = ButtonColumn(panel, 30f);

        launcher.continueButton = WideButton(list, "Btn_Continue", "CONTINUE", ButtonSkin.Purple, launcher.Button_Continue);
        WideButton(list, "Btn_VsAI", "VS AI\n<size=55%>Beat the computer (difficulty in Settings)</size>", ButtonSkin.Purple, launcher.Button_SinglePlayer);
        WideButton(list, "Btn_Sprint", "SPRINT 40L\n<size=55%>Clear 40 lines as fast as you can</size>", ButtonSkin.Purple, launcher.Button_Sprint);
        WideButton(list, "Btn_Ultra", "ULTRA 2:00\n<size=55%>Highest score in 2 minutes</size>", ButtonSkin.Purple, launcher.Button_Ultra);

        BackButton(panel, launcher);
        launcher.singlePlayerPanel = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // ---------- MULTIPLAYER ----------

    private static void BuildMultiplayerPanel(RectTransform modeSelect, FusionLauncher launcher)
    {
        RectTransform panel = NewScreen("MultiplayerPanel", modeSelect);
        StyleHeading(NewText("Heading", panel, "", 80, Color.white), "MULTIPLAYER");
        RectTransform list = ButtonColumn(panel, 18f);

        WideButton(list, "Btn_QuickMatch", "QUICK MATCH\n<size=55%>Play a random opponent online</size>", ButtonSkin.Purple, launcher.Button_Multiplayer);

        TextMeshProUGUI friends = NewText("FriendsHeading", list, "PLAY WITH A FRIEND", 38, Gold);
        friends.rectTransform.sizeDelta = new Vector2(WideButtonWidth, 70f);
        friends.alignment = TextAlignmentOptions.Bottom;

        // Room code box
        var resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };
        GameObject inputGo = TMP_DefaultControls.CreateInputField(resources);
        inputGo.name = "RoomCode_Input";
        inputGo.layer = list.gameObject.layer;
        RectTransform inputRect = (RectTransform)inputGo.transform;
        inputRect.SetParent(list, false);
        inputRect.sizeDelta = new Vector2(WideButtonWidth, 90f);
        TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
        input.characterLimit = 12;
        input.contentType = TMP_InputField.ContentType.Alphanumeric;
        input.textComponent.fontSize = 42;
        input.textComponent.alignment = TextAlignmentOptions.Center;
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "ROOM CODE";
            placeholder.fontSize = 34;
            placeholder.alignment = TextAlignmentOptions.Center;
        }
        launcher.roomCodeInput = input;

        WideButton(list, "Btn_PrivateRoom", "CREATE / JOIN ROOM", ButtonSkin.Blue, launcher.Button_PrivateRoom);
        TextMeshProUGUI hint = NewText("Hint", list, "Leave the code empty to create a room, then share its code.\nType a friend's code to join them.", 24, LightBlue);
        hint.fontStyle = FontStyles.Normal;
        hint.rectTransform.sizeDelta = new Vector2(WideButtonWidth + 200f, 70f);

        BackButton(panel, launcher);
        launcher.multiplayerPanel = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // ---------- Building blocks ----------

    // A full-screen page styled like SELECT MODE (same background image and color)
    private static RectTransform NewScreen(string name, RectTransform modeSelect)
    {
        RectTransform panel = NewUI(name, modeSelect.parent);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.SetSiblingIndex(modeSelect.GetSiblingIndex() + 1);

        Image source = modeSelect.GetComponent<Image>();
        Image background = panel.gameObject.AddComponent<Image>();
        if (source != null)
        {
            background.sprite = source.sprite;
            background.type = source.type;
            background.color = source.color;
        }
        else background.color = new Color(0.12f, 0.07f, 0.30f, 0.95f);
        return panel;
    }

    private static RectTransform ButtonColumn(RectTransform panel, float spacing)
    {
        RectTransform list = NewUI("Buttons", panel);
        list.anchorMin = list.anchorMax = list.pivot = new Vector2(0.5f, 0.5f);
        list.sizeDelta = new Vector2(WideButtonWidth + 200f, 620f);
        list.anchoredPosition = new Vector2(0f, 10f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return list;
    }

    private static Button WideButton(Transform parent, string name, string text, Sprite sprite, UnityAction onClick)
    {
        RectTransform rect = NewUI(name, parent);
        rect.sizeDelta = new Vector2(WideButtonWidth, WideButtonHeight);
        rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = NewText("Label", rect, text, 44, Color.white);
        StretchLabel(label.rectTransform);
        ButtonSkin.Apply(button, sprite);
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return button;
    }

    private static void BackButton(RectTransform panel, FusionLauncher launcher)
    {
        RectTransform rect = NewUI("Btn_Back", panel);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 100f);
        rect.anchoredPosition = new Vector2(0f, -410f);
        rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = NewText("Label", rect, "BACK", 40, Color.white);
        StretchLabel(label.rectTransform);
        ButtonSkin.Apply(button, ButtonSkin.Blue);
        UnityEventTools.AddPersistentListener(button.onClick, launcher.Button_BackToModeSelect);
    }

    private static void StyleHeading(TMP_Text heading, string text)
    {
        RectTransform rect = heading.rectTransform;
        rect.localScale = Vector3.one;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1200f, 120f);
        rect.anchoredPosition = new Vector2(0f, 400f);
        heading.text = text;
        heading.fontSize = 80;
        heading.fontStyle = FontStyles.Bold;
        heading.color = Color.white;
        heading.alignment = TextAlignmentOptions.Center;
        heading.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Resets a stretched old button to scale 1 and a real size, then re-skins it with a centred label
    private static void FixButton(RectTransform rect, string text, Vector2 size, Sprite sprite, float fontSize = 44f)
    {
        if (rect == null) return;
        Undo.RecordObject(rect, "Rebuild Mode Menus");
        rect.localScale = Vector3.one;
        rect.sizeDelta = size;

        TMP_Text label = rect.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.localScale = Vector3.one;
            StretchLabel(label.rectTransform);
        }
        Button button = rect.GetComponent<Button>();
        if (button != null) ButtonSkin.Apply(button, sprite);
    }

    // Replaces a button's saved click actions with one
    private static void Rewire(Button button, UnityAction action)
    {
        if (button == null) return;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private static void StretchLabel(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 6f);
        rect.offsetMax = new Vector2(-12f, -6f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static Transform[] AllTransforms(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

    private static RectTransform Find(Transform[] all, string name) =>
        all.FirstOrDefault(t => t != null && t.name == name) as RectTransform;

    private static RectTransform FindChild(Transform parent, string name) =>
        parent.Cast<Transform>().FirstOrDefault(t => t.name == name) as RectTransform;

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        TextMeshProUGUI tmp = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
