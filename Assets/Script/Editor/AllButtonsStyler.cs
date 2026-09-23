using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Style All Buttons
// Gives every button in the game's scenes (Build Settings) and your own prefabs the outlined
// Big Button look: purple for main actions, blue for back / return / quit / leave.
// Third-party folders (Photon, SlimUI, TextMesh Pro, Simple Buttons) are left alone.
public static class AllButtonsStyler
{
    private static readonly string[] SkipFolders =
    {
        "Assets/Photon", "Assets/SlimUI", "Assets/TextMesh Pro", "Assets/Simple Buttons"
    };

    [MenuItem("Tools/Tetris/Style All Buttons")]
    private static void StyleAll()
    {
        if (!EditorUtility.DisplayDialog("Style All Buttons",
                "This restyles every button in your game scenes and prefabs and SAVES them.\n\n" +
                "Undo isn't available across scenes, so commit first if you want an easy way back " +
                "(git can restore everything).", "Style them", "Cancel")) return;

        if (!ButtonSkin.SpritesAvailable)
        {
            EditorUtility.DisplayDialog("Style All Buttons", "Couldn't load the Big Buttons sprites from Assets/Simple Buttons/Big Buttons.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string startScene = SceneManager.GetActiveScene().path;
        var report = new StringBuilder();
        int styled = 0;

        // 1. Prefabs first, so scene instances pick up the change
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (SkipFolders.Any(path.StartsWith)) continue;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<Button>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = StyleButtons(root.GetComponentsInChildren<Button>(true), path, report, false);
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                styled += count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 2. Every enabled scene in Build Settings
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            Button[] buttons = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .ToArray();

            int count = StyleButtons(buttons, buildScene.path, report, true);
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            styled += count;
        }

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        Debug.Log("Style All Buttons:\n" + report);
        EditorUtility.DisplayDialog("Style All Buttons",
            $"Styled {styled} button(s).\n\n{report}\nThe full list is also in the Console.", "OK");
    }

    private static int StyleButtons(IEnumerable<Button> buttons, string where, StringBuilder report, bool skipPrefabInstances)
    {
        int count = 0;
        foreach (Button button in buttons)
        {
            // Buttons inside a prefab instance are styled through the prefab itself
            if (skipPrefabInstances && PrefabUtility.IsPartOfPrefabInstance(button)) continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(where) + " / " + button.name;
            switch (ButtonSkin.Apply(button, ButtonSkin.SpriteFor(button)))
            {
                case ButtonSkin.Result.Styled:
                    count++;
                    report.AppendLine("✓ " + name);
                    break;
                case ButtonSkin.Result.AlreadyIcon:
                    report.AppendLine("• " + name + " (already an outlined icon button, kept)");
                    break;
                case ButtonSkin.Result.CustomArt:
                    report.AppendLine("• " + name + " (custom artwork, kept)");
                    break;
            }
        }
        return count;
    }
}
