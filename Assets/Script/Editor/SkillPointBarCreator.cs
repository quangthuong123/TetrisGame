using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Tetris > Create SP Bar
// Builds a display-only Slider with a SkillPointBar under the board's SP_text and hooks it into BoardUI.
public static class SkillPointBarCreator
{
    private const float BarHeight = 24f;
    private const float GapBelowText = 8f;

    [MenuItem("Tools/Tetris/Create SP Bar")]
    private static void CreateBar()
    {
        // Board prefab already open in Prefab Mode: build it there (undoable)
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            BoardUI stageUI = stage.prefabContentsRoot.GetComponentInChildren<BoardUI>(true);
            if (stageUI != null)
            {
                if (!ConfirmReplace(stageUI)) return;
                Build(stageUI, true);
                EditorSceneManager.MarkSceneDirty(stage.scene);
                return;
            }
        }

        // Otherwise edit the prefab asset that holds BoardUI (the Game Manager prefab)
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<BoardUI>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BoardUI ui = root.GetComponentInChildren<BoardUI>(true);
                if (!ConfirmReplace(ui)) return;
                Build(ui, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorGUIUtility.PingObject(asset);
            EditorUtility.DisplayDialog("SP Bar created",
                $"Added SP_Bar under SP_text in:\n{path}\n\nOpen the prefab to move, resize or recolor it.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("SP Bar", "Couldn't find a prefab with a BoardUI component.", "OK");
    }

    private static bool ConfirmReplace(BoardUI ui)
    {
        return ui.spBar == null || EditorUtility.DisplayDialog("SP Bar",
            "This board already has an SP bar. Replace it?", "Replace", "Cancel");
    }

    private static void Build(BoardUI ui, bool undoable)
    {
        if (ui.spBar != null)
        {
            if (undoable) Undo.DestroyObjectImmediate(ui.spBar.gameObject);
            else Object.DestroyImmediate(ui.spBar.gameObject);
        }

        Transform parent = ui.spText != null ? ui.spText.transform.parent : ui.transform;

        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        GameObject go = DefaultControls.CreateSlider(resources);
        go.name = "SP_Bar";
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;

        Slider slider = go.GetComponent<Slider>();

        // Display only: no handle, and the fill spans the full bar
        if (slider.handleRect != null) Object.DestroyImmediate(slider.handleRect.parent.gameObject);
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        RectTransform fillArea = (RectTransform)slider.fillRect.parent;
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;
        slider.fillRect.sizeDelta = Vector2.zero;

        Image background = go.transform.Find("Background")?.GetComponent<Image>();
        if (background != null) background.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        // Sit just below the SP text, matching its width
        if (ui.spText != null)
        {
            RectTransform sp = ui.spText.rectTransform;
            rect.SetSiblingIndex(sp.GetSiblingIndex() + 1);
            if (sp.anchorMin == sp.anchorMax)
            {
                rect.anchorMin = sp.anchorMin;
                rect.anchorMax = sp.anchorMax;
                rect.pivot = new Vector2(sp.pivot.x, 0.5f);
                rect.sizeDelta = new Vector2(Mathf.Max(sp.rect.width, 200f), BarHeight);
                float textBottom = sp.anchoredPosition.y - sp.rect.height * sp.pivot.y;
                rect.anchoredPosition = new Vector2(sp.anchoredPosition.x, textBottom - GapBelowText - BarHeight * 0.5f);
            }
        }
        else
        {
            rect.sizeDelta = new Vector2(200f, BarHeight);
        }

        SkillPointBar bar = go.AddComponent<SkillPointBar>();
        bar.slider = slider;
        bar.fillImage = slider.fillRect.GetComponent<Image>();

        if (undoable)
        {
            Undo.RegisterCreatedObjectUndo(go, "Create SP Bar");
            Undo.RecordObject(ui, "Create SP Bar");
        }
        ui.spBar = bar;
        EditorUtility.SetDirty(ui);
    }
}
