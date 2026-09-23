using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Tetris > Resize Pause Menu
// The game scenes' canvas used "Constant Pixel Size", so UI kept its full pixel size and overflowed any
// window smaller than 1080p (the pause card, the full-screen Game Over panel...). This switches both game
// scenes to "Scale With Screen Size" 1920x1080 (same as the menu) and makes the pause card more compact.
public static class PauseMenuResize
{
    private static readonly string[] GameScenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/SinglePlayerScene.unity" };

    // Pause card layout (in 1920x1080 reference pixels)
    private static readonly Vector2 CardSize = new Vector2(560f, 520f);
    private static readonly Vector2 ButtonSize = new Vector2(400f, 84f);
    private const float TitleSize = 60f;
    private const float ButtonFontSize = 32f;
    private const float HintSize = 22f;

    [MenuItem("Tools/Tetris/Resize Pause Menu")]
    private static void Resize()
    {
        if (!EditorUtility.DisplayDialog("Resize Pause Menu",
                "For GameScene and SinglePlayerScene (both are SAVED):\n\n" +
                "• Canvas scales with the window (1920x1080 reference), like the menu\n" +
                "• Smaller pause card and buttons\n\n" +
                "Safe to run again.", "Resize", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;

        string report = "";
        foreach (string path in GameScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

            int scalers = 0;
            foreach (Canvas canvas in all.Select(t => t.GetComponent<Canvas>()).Where(c => c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace))
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                EditorUtility.SetDirty(scaler);
                scalers++;
            }

            bool pauseFound = ResizeCard(all.FirstOrDefault(t => t.name == "PauseCard") as RectTransform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report += $"{System.IO.Path.GetFileNameWithoutExtension(path)}: {scalers} canvas scaled" +
                      (pauseFound ? ", pause card resized\n" : ", no pause menu (run Setup Progress UI once)\n");
        }

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Resize Pause Menu", report + "\nBoth scenes saved.", "OK");
    }

    private static bool ResizeCard(RectTransform card)
    {
        if (card == null) return false;

        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = CardSize;

        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(40, 40, 30, 30);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        foreach (Transform child in card)
        {
            var rect = (RectTransform)child;
            Button button = child.GetComponent<Button>();
            if (button != null)
            {
                rect.sizeDelta = ButtonSize;
                TMP_Text label = child.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 18f;
                    label.fontSizeMax = ButtonFontSize;
                }
                ButtonSkin.Apply(button, button.name == "Btn_Resume" ? ButtonSkin.Purple : ButtonSkin.Blue); // Re-fit the outline to the new height
                continue;
            }

            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text == null) continue;
            bool isTitle = child.name == "Title";
            rect.sizeDelta = new Vector2(CardSize.x - 80f, isTitle ? 76f : 36f);
            text.enableAutoSizing = true;
            text.fontSizeMin = isTitle ? 30f : 14f;
            text.fontSizeMax = isTitle ? TitleSize : HintSize;
        }
        return true;
    }
}
