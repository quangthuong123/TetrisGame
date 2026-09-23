using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Rebuild Match Lobby
// Lays the online lobby out as  YOU (left)  |  status + countdown + LEAVE (centre)  |  OPPONENT (right):
// • YOU: "YOU · HOST/GUEST", your name + level, portrait, your status badge, and the READY button (wired!)
// • OPPONENT: "OPPONENT · GUEST/HOST", name + level, portrait, their status badge (not a button)
// • Every label at scale 1 and a readable size (some were at scale 0.1)
public static class LobbyLayoutSetup
{
    private const string MenuScene = "Assets/Scenes/StartMenu.unity";
    private const string PanelSprite = "Assets/Game Asset/BoardSkin/Panel.png";
    private static readonly Color LightBlue = new Color(0.62f, 0.80f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);

    [MenuItem("Tools/Tetris/Rebuild Match Lobby")]
    private static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Rebuild Match Lobby",
                "This rearranges MatchLobbyPanel in StartMenu and SAVES the scene:\n\n" +
                "• YOU | status | OPPONENT columns with HOST / GUEST headers\n" +
                "• READY button in your column, wired to Button_Ready\n" +
                "• Opponent's ready state as a badge (not a button)\n" +
                "• Readable text everywhere\n\n" +
                "Commit first if you want an easy way back.", "Rebuild", "Cancel")) return;
        if (!ButtonSkin.SpritesAvailable)
        {
            EditorUtility.DisplayDialog("Rebuild Match Lobby", "Couldn't load the Big Buttons sprites.", "OK");
            return;
        }
        if (SceneManager.GetActiveScene().path != MenuScene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }

        Scene scene = SceneManager.GetActiveScene();
        Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        FusionLauncher launcher = all.Select(t => t.GetComponent<FusionLauncher>()).FirstOrDefault(l => l != null);
        RectTransform lobby = launcher != null && launcher.matchLobbyPanel != null ? (RectTransform)launcher.matchLobbyPanel.transform : null;
        RectTransform left = Child(lobby, "Col_Player1"), center = Child(lobby, "Col_Center"), right = Child(lobby, "Col_Player2");
        if (lobby == null || left == null || center == null || right == null)
        {
            EditorUtility.DisplayDialog("Rebuild Match Lobby", "Couldn't find MatchLobbyPanel with Col_Player1 / Col_Center / Col_Player2.", "OK");
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(lobby.gameObject, "Rebuild Match Lobby");
        Undo.RecordObject(launcher, "Rebuild Match Lobby");

        PlaceColumn(left, -650f, 540f);
        PlaceColumn(center, 0f, 660f);
        PlaceColumn(right, 650f, 540f);

        // ----- YOU -----
        launcher.p1HeaderText = Header(left, "P1_Header", "YOU");
        launcher.p1NameText = NameText(Child(left, "P1_Name_text"));
        Portrait(Child(left, "P1_PortraitBox"));
        launcher.p1StatusText = Badge(left, "P1_Status", "<color=grey>CONNECTING...</color>");
        RectTransform ready = Child(left, "Btn_Readu_p1") ?? Child(left, "Btn_Ready");
        if (ready != null)
        {
            ready.name = "Btn_Ready";
            Fixed(ready, 130f);
            Button readyButton = ready.GetComponent<Button>();
            TMP_Text label = Label(ready, "READY", 60f);
            if (readyButton != null)
            {
                ButtonSkin.Apply(readyButton, ButtonSkin.Purple);
                for (int i = readyButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(readyButton.onClick, i);
                UnityEventTools.AddPersistentListener(readyButton.onClick, launcher.Button_Ready);
            }
            label.color = Color.white;
            launcher.readyButton = ready.gameObject;
        }
        Order(left, "P1_Header", "P1_Name_text", "P1_PortraitBox", "P1_Status", "Btn_Ready");

        // ----- OPPONENT -----
        launcher.p2HeaderText = Header(right, "P2_Header", "OPPONENT");
        launcher.p2NameText = NameText(Child(right, "P2_Name_text"));
        Portrait(Child(right, "P2_PortraitBox"));
        // Their old "Ready" button becomes a plain status badge: nothing to click on the opponent's side
        RectTransform oldP2Button = Child(right, "Btn_Readu_p2");
        if (oldP2Button != null) Undo.DestroyObjectImmediate(oldP2Button.gameObject);
        launcher.p2StatusText = Badge(right, "P2_Status", "<color=grey>WAITING...</color>");
        Spacer(right, "Spacer", 130f); // Lines the badges up with yours
        Order(right, "P2_Header", "P2_Name_text", "P2_PortraitBox", "P2_Status", "Spacer");

        // ----- CENTRE -----
        TMP_Text countdown = Child(center, "Countdown_Text")?.GetComponent<TMP_Text>();
        if (countdown != null)
        {
            Fixed(countdown.rectTransform, 230f);
            Style(countdown, 160f, Gold);
            countdown.text = "";
            launcher.countdownText = (TextMeshProUGUI)countdown;
        }
        TMP_Text status = Child(center, "Stats_Text")?.GetComponent<TMP_Text>();
        if (status != null)
        {
            Flexible(status.rectTransform);
            Style(status, 48f, Color.white);
            status.enableAutoSizing = true;
            status.fontSizeMin = 26f;
            status.fontSizeMax = 48f;
            status.textWrappingMode = TextWrappingModes.Normal;
            status.text = "WAITING FOR OPPONENT TO JOIN...";
            launcher.lobbyStatusText = (TextMeshProUGUI)status;
        }
        TextMeshProUGUI ping = Child(center, "Ping_Text")?.GetComponent<TextMeshProUGUI>() ?? NewText(center, "Ping_Text");
        Fixed(ping.rectTransform, 50f);
        Style(ping, 30f, LightBlue);
        ping.text = "Ping: -- ms";
        launcher.pingText = ping;
        RectTransform leave = Child(center, "Btn_LeaveLobby");
        if (leave != null)
        {
            Fixed(leave, 120f);
            Label(leave, "LEAVE", 52f);
            Button leaveButton = leave.GetComponent<Button>();
            if (leaveButton != null) ButtonSkin.Apply(leaveButton, ButtonSkin.Blue);
        }
        Order(center, "Countdown_Text", "Stats_Text", "Ping_Text", "Btn_LeaveLobby");

        EditorUtility.SetDirty(launcher);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Rebuild Match Lobby",
            "Lobby rebuilt and StartMenu saved.\n\n" +
            "READY now calls Button_Ready. It appears in YOUR column once an opponent joins; " +
            "when both players are ready, the host starts the 3-2-1 countdown.\n\n" +
            "MatchLobbyPanel is hidden until you search for a match; tick it active to preview (untick before playing).", "OK");
    }

    // ---------- Pieces ----------

    // Centred column with a vertical layout that sizes its children
    private static void PlaceColumn(RectTransform column, float x, float width)
    {
        column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(width, 920f);
        column.anchoredPosition = new Vector2(x, 0f);
        column.localScale = Vector3.one;

        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static TextMeshProUGUI Header(RectTransform column, string name, string text)
    {
        TextMeshProUGUI header = Child(column, name)?.GetComponent<TextMeshProUGUI>() ?? NewText(column, name);
        Fixed(header.rectTransform, 60f);
        Style(header, 40f, Gold);
        header.text = text;
        return header;
    }

    private static TextMeshProUGUI NameText(RectTransform rect)
    {
        if (rect == null) return null;
        TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
        Fixed(rect, 140f);
        if (text == null) return null;
        Style(text, 60f, Color.white);
        text.enableAutoSizing = true;
        text.fontSizeMin = 30f;
        text.fontSizeMax = 60f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = "Player";
        return text;
    }

    private static void Portrait(RectTransform rect)
    {
        if (rect != null) Fixed(rect, 280f);
    }

    // Dark rounded badge with a big centred status line ("NOT READY" / "READY ✓")
    private static TextMeshProUGUI Badge(RectTransform column, string name, string text)
    {
        RectTransform badge = Child(column, name);
        if (badge == null)
        {
            badge = NewUI(name, column);
            Image image = badge.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
            image.type = Image.Type.Sliced;
            image.color = image.sprite != null ? Color.white : new Color(0.12f, 0.07f, 0.30f, 0.95f);
            image.raycastTarget = false;
        }
        Fixed(badge, 100f);
        return (TextMeshProUGUI)Label(badge, text, 46f);
    }

    private static void Spacer(RectTransform column, string name, float height)
    {
        RectTransform spacer = Child(column, name) ?? NewUI(name, column);
        Fixed(spacer, height);
    }

    // The label inside a button/badge: scale 1, fills its parent, readable size
    private static TMP_Text Label(RectTransform parent, string text, float size)
    {
        TMP_Text label = parent.GetComponentInChildren<TMP_Text>(true);
        if (label == null) label = NewText(parent, "Label");
        RectTransform rect = label.rectTransform;
        rect.localScale = Vector3.one; // Some labels were at 0.1, which made them unreadable
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);
        Style(label, size, Color.white);
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.5f;
        label.fontSizeMax = size;
        label.text = text;
        return label;
    }

    private static void Style(TMP_Text text, float size, Color color)
    {
        text.rectTransform.localScale = Vector3.one;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
    }

    private static void Fixed(RectTransform rect, float height)
    {
        rect.localScale = Vector3.one;
        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        element.flexibleHeight = 0f;
    }

    private static void Flexible(RectTransform rect)
    {
        rect.localScale = Vector3.one;
        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 120f;
        element.flexibleHeight = 1f;
    }

    private static void Order(RectTransform column, params string[] names)
    {
        int index = 0;
        foreach (string name in names)
        {
            RectTransform child = Child(column, name);
            if (child != null) child.SetSiblingIndex(index++);
        }
    }

    private static RectTransform Child(Transform parent, string name) =>
        parent == null ? null : parent.Cast<Transform>().FirstOrDefault(t => t.name == name) as RectTransform;

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Rebuild Match Lobby");
        return rect;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name) =>
        NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
}
