using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Build Main Menu Showcase
// Fills the middle of the main menu: neon logo + tagline, a player card (name box, level, XP bar,
// Best Score / Sprint / Ultra tiles), rotating tips, and falling neon tetrominoes behind it all.
public static class MainMenuShowcaseSetup
{
    private const string MenuScene = "Assets/Scenes/StartMenu.unity";
    private const string PanelSprite = "Assets/Game Asset/BoardSkin/Panel.png";
    private const string BlockSprite = "Assets/Game Asset/BoardSkin/Block.png";
    private const string PlaceholderProductName = "My project (6)";

    // Screen-space centre of the free area (left of the PLAY / PROFILE / QUIT column)
    private const float CenterX = -160f;

    private static readonly Color LightBlue = new Color(0.62f, 0.80f, 1f, 1f);
    private static readonly Color TileColor = new Color(0.10f, 0.06f, 0.26f, 0.9f);

    private static readonly string[] BuiltNames = { "Showcase", "FallArea", "Title_Shadow", "Title_Tagline", "PlayerCard", "TipText" };

    [MenuItem("Tools/Tetris/Build Main Menu Showcase")]
    private static void Build()
    {
        if (!EditorUtility.DisplayDialog("Build Main Menu Showcase",
                "This fills the middle of the main menu and SAVES StartMenu:\n\n" +
                "• Neon game logo + tagline (replaces the plain title)\n" +
                "• Player card: name box, level + XP bar, Best Score / Sprint / Ultra\n" +
                "  (replaces the plain HIGH SCORE line)\n" +
                "• Rotating tips and falling neon blocks in the background\n\n" +
                "Safe to run again.", "Build", "Cancel")) return;

        if (SceneManager.GetActiveScene().path != MenuScene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }

        Scene scene = SceneManager.GetActiveScene();
        Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        FusionLauncher launcher = all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);
        RectTransform menu = launcher != null && launcher.mainMenuPanel != null ? (RectTransform)launcher.mainMenuPanel.transform : null;
        RectTransform titleRect = all.FirstOrDefault(t => t.name == "Title_text") as RectTransform;
        if (menu == null || titleRect == null)
        {
            EditorUtility.DisplayDialog("Build Main Menu Showcase", "Couldn't find the main menu panel and Title_text.", "OK");
            return;
        }

        // Rebuild from scratch (and drop the old generic HIGH SCORE line)
        foreach (Transform t in all.Where(t => t != null && (BuiltNames.Contains(t.name) || t.name == "MenuHighScore_Text")).ToArray())
        {
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        Undo.RecordObject(launcher, "Build Main Menu Showcase");
        launcher.menuHighScoreText = null;

        MainMenuShowcase showcase = NewUI("Showcase", menu).gameObject.AddComponent<MainMenuShowcase>();

        // ----- Falling blocks (behind everything else on the menu) -----
        RectTransform fall = NewUI("FallArea", menu);
        fall.sizeDelta = new Vector2(1400f, 1080f);
        fall.anchoredPosition = Local(menu, CenterX, 0f);
        fall.SetAsFirstSibling();
        showcase.fallArea = fall;
        showcase.blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BlockSprite);
        showcase.blockColors = GameBlockColors();

        // ----- Logo -----
        string gameName = PlayerSettings.productName == PlaceholderProductName ? "NEON BLOCKS ONLINE" : PlayerSettings.productName.ToUpperInvariant();
        string logo = gameName.EndsWith(" ONLINE") ? gameName.Substring(0, gameName.Length - " ONLINE".Length) : gameName;

        TMP_Text title = titleRect.GetComponent<TMP_Text>();
        titleRect.sizeDelta = new Vector2(1300f, 180f);
        titleRect.anchoredPosition = Local(menu, CenterX, 345f);
        titleRect.localScale = Vector3.one;
        if (title != null)
        {
            StyleText(title, logo, 150f, Color.white, true);
            title.enableVertexGradient = true;
            title.characterSpacing = 6f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 60f;
            title.fontSizeMax = 150f;
            showcase.title = title;
        }

        // Dark copy behind the logo as a drop shadow
        TextMeshProUGUI shadow = NewText("Title_Shadow", menu, logo, 150f, new Color(0.08f, 0.02f, 0.22f, 0.85f));
        shadow.rectTransform.sizeDelta = titleRect.sizeDelta;
        shadow.rectTransform.anchoredPosition = titleRect.anchoredPosition + new Vector2(8f, -10f);
        shadow.characterSpacing = 6f;
        shadow.enableAutoSizing = true;
        shadow.fontSizeMin = 60f;
        shadow.fontSizeMax = 150f;
        shadow.transform.SetSiblingIndex(titleRect.GetSiblingIndex());

        TextMeshProUGUI tagline = NewText("Title_Tagline", menu, "ONLINE  ·  VERSUS  ·  SPRINT  ·  ULTRA", 36f, new Color(0.45f, 0.95f, 1f));
        tagline.rectTransform.sizeDelta = new Vector2(1300f, 60f);
        tagline.rectTransform.anchoredPosition = Local(menu, CenterX, 238f);
        tagline.characterSpacing = 14f;
        showcase.tagline = tagline;

        // ----- Player card -----
        RectTransform card = NewUI("PlayerCard", menu);
        card.sizeDelta = new Vector2(980f, 420f);
        card.anchoredPosition = Local(menu, CenterX, -70f);
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
        cardImage.type = Image.Type.Sliced;
        cardImage.color = cardImage.sprite != null ? Color.white : TileColor;
        cardImage.raycastTarget = false;
        VerticalLayoutGroup cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(44, 44, 30, 30);
        cardLayout.spacing = 12f;
        cardLayout.childControlWidth = cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        showcase.greetingText = Sized(NewText("Greeting", card, "WELCOME BACK, PLAYER", 46f, Color.white), 60f);

        // Name row
        RectTransform nameRow = NewUI("NameRow", card);
        Height(nameRow, 64f);
        HorizontalLayoutGroup nameLayout = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        nameLayout.spacing = 16f;
        nameLayout.childAlignment = TextAnchor.MiddleCenter;
        nameLayout.childControlWidth = nameLayout.childControlHeight = true;
        nameLayout.childForceExpandWidth = nameLayout.childForceExpandHeight = false;
        TextMeshProUGUI nameLabel = NewText("NameLabel", nameRow, "YOUR NAME", 28f, LightBlue);
        nameLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;
        showcase.nameInput = NameInput(nameRow);
        launcher.nameInputField = showcase.nameInput; // Saved when PLAY is pressed as well

        showcase.levelText = Sized(NewText("Level", card, "LEVEL 1  ·  ROOKIE", 34f, Color.white), 48f);

        // XP bar
        RectTransform bar = NewUI("XpBar", card);
        Height(bar, 22f);
        bar.gameObject.AddComponent<Image>().color = TileColor;
        RectTransform fill = NewUI("Fill", bar);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0.35f;
        fillImage.color = new Color(0.45f, 0.95f, 1f);
        showcase.xpFill = fillImage;

        // Record tiles
        RectTransform tiles = NewUI("Records", card);
        Height(tiles, 118f);
        HorizontalLayoutGroup tilesLayout = tiles.gameObject.AddComponent<HorizontalLayoutGroup>();
        tilesLayout.spacing = 18f;
        tilesLayout.childControlWidth = tilesLayout.childControlHeight = true;
        tilesLayout.childForceExpandWidth = tilesLayout.childForceExpandHeight = true;
        showcase.bestScoreText = Tile(tiles, "BestScore");
        showcase.bestSprintText = Tile(tiles, "BestSprint");
        showcase.bestUltraText = Tile(tiles, "BestUltra");

        // ----- Tips -----
        TextMeshProUGUI tip = NewText("TipText", menu, "TIP  Press C to hold a piece.", 28f, LightBlue);
        tip.fontStyle = FontStyles.Normal;
        tip.rectTransform.sizeDelta = new Vector2(1300f, 60f);
        tip.rectTransform.anchoredPosition = Local(menu, CenterX, -335f);
        showcase.tipText = tip;

        EditorUtility.SetDirty(showcase);
        EditorUtility.SetDirty(launcher);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Build Main Menu Showcase",
            "Main menu filled and StartMenu saved.\n\n" +
            "Everything sits under MainMenu (FallArea, Title_text/Shadow/Tagline, PlayerCard, TipText), " +
            "so you can move or resize it freely. Colours, speeds and tips are on the Showcase object.", "OK");
    }

    // ---------- Helpers ----------

    // MainMenu is a small rect at the bottom-left corner, so convert a screen-centred position into its space
    private static Vector2 Local(RectTransform menu, float screenX, float screenY) =>
        new Vector2(screenX, screenY) - menu.anchoredPosition;

    private static Color[] GameBlockColors()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            TetrisEngine engine = prefab != null ? prefab.GetComponent<TetrisEngine>() : null;
            if (engine != null && engine.blockColors != null && engine.blockColors.Length >= 8)
            {
                return engine.blockColors.Skip(1).Take(7).ToArray(); // The seven piece colours (not empty / garbage)
            }
        }
        return new[] { Color.cyan, Color.magenta, Color.yellow, new Color(0.5f, 0.3f, 1f), Color.green, Color.red, new Color(1f, 0.5f, 0f) };
    }

    private static TMP_InputField NameInput(Transform parent)
    {
        var resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };
        GameObject go = TMP_DefaultControls.CreateInputField(resources);
        go.name = "NameInput";
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.preferredWidth = 460f;
        element.preferredHeight = 60f;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.characterLimit = 16;
        input.textComponent.fontSize = 32;
        input.textComponent.alignment = TextAlignmentOptions.Center;
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Type your name...";
            placeholder.fontSize = 28;
            placeholder.alignment = TextAlignmentOptions.Center;
        }
        return input;
    }

    private static TextMeshProUGUI Tile(Transform parent, string name)
    {
        RectTransform tile = NewUI(name, parent);
        Image image = tile.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = TileColor;
        image.raycastTarget = false;
        TextMeshProUGUI text = NewText("Value", tile, "<size=55%>BEST</size>\n—", 44f, Color.white);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.lineSpacing = -10f;
        return text;
    }

    private static TextMeshProUGUI Sized(TextMeshProUGUI text, float height)
    {
        Height(text.rectTransform, height);
        return text;
    }

    private static void Height(RectTransform rect, float height)
    {
        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
    }

    private static void StyleText(TMP_Text text, string content, float size, Color color, bool bold)
    {
        text.text = content;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        TextMeshProUGUI tmp = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(tmp, text, size, color, true);
        return tmp;
    }
}
