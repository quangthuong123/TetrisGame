using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Tetris > Apply Board Skin
// Swaps the plain white squares for the BoardSkin sprites (Assets/Game Asset/BoardSkin):
// beveled blocks, outline ghost, a framed grid board and a framed Next-piece panel.
// Also points TetrisEngine.boardBackground at the real board (it pointed at the TBackground prefab asset).
public static class BoardSkinApplier
{
    private const string SkinFolder = "Assets/Game Asset/BoardSkin/";
    private const float PanelBorderUnits = 0.35f; // Frame thickness of the Next panel on the world-space canvas

    [MenuItem("Tools/Tetris/Apply Board Skin")]
    private static void Apply()
    {
        Sprite block = Load("Block.png");
        Sprite ghost = Load("Ghost.png");
        Sprite board = Load("BoardFrame.png");
        Sprite panel = Load("Panel.png");
        if (block == null || ghost == null || board == null || panel == null)
        {
            EditorUtility.DisplayDialog("Apply Board Skin", "Couldn't load the sprites in " + SkinFolder, "OK");
            return;
        }

        string boardPath = FindBoardPrefab();
        if (boardPath == null)
        {
            EditorUtility.DisplayDialog("Apply Board Skin", "Couldn't find the board prefab (the one with TetrisEngine).", "OK");
            return;
        }

        TetrisEngine engineAsset = AssetDatabase.LoadAssetAtPath<GameObject>(boardPath).GetComponent<TetrisEngine>();
        string blockPath = engineAsset.blockPrefab != null ? AssetDatabase.GetAssetPath(engineAsset.blockPrefab) : null;
        string ghostPath = engineAsset.ghostPrefab != null ? AssetDatabase.GetAssetPath(engineAsset.ghostPrefab) : null;

        if (!string.IsNullOrEmpty(blockPath)) EditPrefab(blockPath, root => SetSprite(root, block));
        if (!string.IsNullOrEmpty(ghostPath)) EditPrefab(ghostPath, root => SetSprite(root, ghost));
        bool boardOpen = EditPrefab(boardPath, root => SkinBoard(root, board, panel));

        EditorUtility.DisplayDialog("Apply Board Skin",
            "Board skin applied:\n\n" +
            $"• Blocks: {blockPath}\n• Ghost: {ghostPath}\n• Board + Next panel: {boardPath}\n\n" +
            (boardOpen ? "The board prefab is open in Prefab Mode: save it with Ctrl+S.\n\n" : "") +
            "Safe to run again.", "OK");
    }

    private static void SetSprite(GameObject root, Sprite sprite)
    {
        SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null) return;
        Undo.RecordObject(renderer, "Apply Board Skin");
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.color = Color.white; // TetrisEngine tints each block at runtime
    }

    private static void SkinBoard(GameObject root, Sprite boardSprite, Sprite panelSprite)
    {
        TetrisEngine engine = root.GetComponent<TetrisEngine>();

        // The board ("Vessel"): the sprite is already 11x21 units (grid + frame), so no stretching
        Transform vessel = FindChild(root.transform, "Vessel");
        SpriteRenderer vesselRenderer = vessel != null ? vessel.GetComponent<SpriteRenderer>() : null;
        if (vesselRenderer != null)
        {
            Undo.RecordObject(vessel, "Apply Board Skin");
            Undo.RecordObject(vesselRenderer, "Apply Board Skin");
            vessel.localScale = Vector3.one;
            vessel.localPosition = new Vector3(4.5f, 9.5f, vessel.localPosition.z); // Centre of the 10x20 cells
            vesselRenderer.sprite = boardSprite;
            vesselRenderer.drawMode = SpriteDrawMode.Simple;
            vesselRenderer.color = Color.white;
        }

        if (engine != null)
        {
            Undo.RecordObject(engine, "Apply Board Skin");
            if (vesselRenderer != null) engine.boardBackground = vesselRenderer; // Was the TBackground prefab asset
            engine.myBoardColor = Color.white;
            engine.opponentBoardColor = new Color(0.72f, 0.72f, 0.82f, 1f); // Opponent's board slightly dimmed
            engine.garbageMeterOffsetX = -1.35f; // Just outside the new frame
            engine.ghostAlpha = 0.6f;
            EditorUtility.SetDirty(engine);
        }

        // Next-piece box
        Transform next = FindChild(root.transform, "NextPiece_Image");
        Image nextImage = next != null ? next.GetComponent<Image>() : null;
        if (nextImage != null)
        {
            Undo.RecordObject(nextImage, "Apply Board Skin");
            nextImage.sprite = panelSprite;
            nextImage.type = Image.Type.Sliced;
            nextImage.color = Color.white;
            nextImage.raycastTarget = false;
            // Canvas units here are world units, so shrink the 32 px frame down to a thin border
            nextImage.pixelsPerUnitMultiplier = panelSprite.border.x / PanelBorderUnits;
        }
    }

    // Edits a prefab in Prefab Mode if it's open there (returns true), otherwise saves the asset directly
    private static bool EditPrefab(string path, System.Action<GameObject> edit)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == path)
        {
            edit(stage.prefabContentsRoot);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            return true;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return false;
    }

    private static string FindBoardPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponent<TetrisEngine>() != null) return path;
        }
        return null;
    }

    private static Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SkinFolder + file);

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
