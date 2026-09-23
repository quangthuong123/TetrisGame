using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Style Match Lobby
// Recolors the StartMenu's MatchLobbyPanel to the purple / blue / white scheme, gives its buttons
// the outlined "Big Buttons" from the Simple Buttons pack, and adds the missing Leave button.
// Everything is undoable (Ctrl+Z) and the scene is saved at the end.
public static class LobbyStyler
{
    private const string MenuScenePath = "Assets/Scenes/StartMenu.unity";
    private const string LeaveButtonName = "Btn_LeaveLobby";

    // Palette
    private static readonly Color PanelBackground = new Color(0.12f, 0.07f, 0.30f, 0.95f); // deep purple
    private static readonly Color PlayerColumn = new Color(0.36f, 0.20f, 0.72f, 0.90f);    // purple
    private static readonly Color CenterColumn = new Color(0.16f, 0.30f, 0.78f, 0.90f);    // blue
    private static readonly Color PortraitBox = new Color(0.62f, 0.80f, 1.00f, 1.00f);     // light blue
    private static readonly Color OutlineWhite = new Color(1f, 1f, 1f, 0.90f);
    private static readonly Color SoftWhite = new Color(0.85f, 0.92f, 1f, 1f);

    [MenuItem("Tools/Tetris/Style Match Lobby")]
    private static void StyleLobby()
    {
        if (!OpenMenuScene()) return;

        GameObject panel = FindInScene("MatchLobbyPanel");
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Style Match Lobby", "Couldn't find MatchLobbyPanel in StartMenu.", "OK");
            return;
        }

        if (!ButtonSkin.SpritesAvailable)
        {
            EditorUtility.DisplayDialog("Style Match Lobby", "Couldn't load the Big Buttons sprites from Assets/Simple Buttons/Big Buttons.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Style Match Lobby");
        int undoGroup = Undo.GetCurrentGroup();

        // Background and columns
        SetImageColor(panel, PanelBackground);
        SetImageColor(Child(panel, "Col_Player1"), PlayerColumn, true);
        SetImageColor(Child(panel, "Col_Player2"), PlayerColumn, true);
        SetImageColor(Child(panel, "Col_Center"), CenterColumn, true);
        SetImageColor(Child(panel, "P1_PortraitBox"), PortraitBox, true);
        SetImageColor(Child(panel, "P2_PortraitBox"), PortraitBox, true);

        // All lobby text white (status texts keep their inline <color> tags)
        foreach (TextMeshProUGUI text in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Undo.RecordObject(text, "Style Match Lobby");
            text.color = Color.white;
        }
        TextMeshProUGUI stats = Child(panel, "Stats_Text")?.GetComponent<TextMeshProUGUI>();
        if (stats != null) stats.color = SoftWhite;

        // Buttons
        StyleButton(Child(panel, "Btn_Readu_p1"), ButtonSkin.Purple);
        StyleButton(Child(panel, "Btn_Readu_p2"), ButtonSkin.Blue);
        StyleButton(EnsureLeaveButton(panel), ButtonSkin.Blue);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        EditorSceneManager.SaveScene(panel.scene);
        Selection.activeGameObject = panel;

        EditorUtility.DisplayDialog("Style Match Lobby",
            "Match lobby restyled and StartMenu saved.\n\n" +
            "• Purple / blue / white colors with white outlines\n" +
            "• Ready buttons use BigPurple / BigBlue, LEAVE uses BigBlue\n" +
            "• Added a LEAVE button (calls Button_CancelMatchmaking)\n\n" +
            "Ctrl+Z undoes it. MatchLobbyPanel is hidden while not in a lobby; " +
            "tick it active in the Hierarchy to preview.", "OK");
    }

    private static bool OpenMenuScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == MenuScenePath) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        return true;
    }

    private static GameObject FindInScene(string name)
    {
        return SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name)?.gameObject;
    }

    private static GameObject Child(GameObject parent, string name)
    {
        if (parent == null) return null;
        return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)?.gameObject;
    }

    private static void SetImageColor(GameObject go, Color color, bool outline = false)
    {
        if (go == null) return;
        Image image = go.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, "Style Match Lobby");
            image.color = color;
        }
        if (outline) AddOutline(go, 4f);
    }

    private static void AddOutline(GameObject go, float thickness)
    {
        Outline outline = go.GetComponent<Outline>();
        if (outline == null) outline = Undo.AddComponent<Outline>(go);
        else Undo.RecordObject(outline, "Style Match Lobby");
        outline.effectColor = OutlineWhite;
        outline.effectDistance = new Vector2(thickness, -thickness);
        outline.useGraphicAlpha = false;
    }

    private static void StyleButton(GameObject go, Sprite sprite)
    {
        if (go != null) ButtonSkin.Apply(go.GetComponent<Button>(), sprite);
    }

    // The lobby had no way out; FusionLauncher.Button_CancelMatchmaking existed but nothing called it
    private static GameObject EnsureLeaveButton(GameObject panel)
    {
        GameObject existing = Child(panel, LeaveButtonName);
        if (existing != null) return existing;

        GameObject parent = Child(panel, "Col_Center") ?? panel;
        GameObject template = Child(panel, "Btn_Readu_p1");

        GameObject go = template != null
            ? Object.Instantiate(template, parent.transform, false)
            : DefaultControls.CreateButton(new DefaultControls.Resources());
        go.name = LeaveButtonName;
        if (template == null) go.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(go, "Style Match Lobby");
        go.SetActive(true);

        // Bottom-center of the column
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(320f, 110f);
        rect.anchoredPosition = new Vector2(0f, 40f);

        TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "LEAVE";

        // Replace the copied Ready click with Leave
        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        FusionLauncher launcher = Object.FindFirstObjectByType<FusionLauncher>(FindObjectsInactive.Include);
        if (launcher != null)
        {
            UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(launcher.Button_CancelMatchmaking));
        }
        else
        {
            Debug.LogWarning("Style Match Lobby: no FusionLauncher in StartMenu; hook LEAVE's On Click to Button_CancelMatchmaking yourself.");
        }
        return go;
    }
}
