using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Tools > Tetris > Prepare Build Settings   (game name, icon, window options)
// Tools > Tetris > Build Windows (x64)       (builds every enabled scene to Builds/Windows)
public static class BuildTools
{
    private const string PlaceholderName = "My project (6)";
    // "Tetris" is a trademark of The Tetris Company, so the default name avoids it. Change it any time in
    // Project Settings > Player > Product Name (this tool only replaces the untouched placeholder).
    private const string DefaultGameName = "Neon Blocks Online";
    private const string IconPath = "Assets/Game Asset/Icon/AppIcon.png";
    private const string OutputFolder = "Builds/Windows";

    [MenuItem("Tools/Tetris/Prepare Build Settings")]
    private static void PrepareMenu()
    {
        string summary = Prepare();
        EditorUtility.DisplayDialog("Prepare Build Settings", summary, "OK");
    }

    private static string Prepare()
    {
        if (PlayerSettings.productName == PlaceholderName) PlayerSettings.productName = DefaultGameName;

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        PlayerSettings.runInBackground = true;   // Online matches keep running when the window loses focus
        PlayerSettings.resizableWindow = true;   // Windowed players can resize (Settings > Fullscreen off)
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        AssetDatabase.SaveAssets();

        return $"Game name: {PlayerSettings.productName}\n" +
               $"Company: {PlayerSettings.companyName}\n" +
               $"Version: {PlayerSettings.bundleVersion}\n" +
               (icon != null ? "Icon: set\n" : $"Icon: not found at {IconPath}\n") +
               "Runs in background, resizable window, starts fullscreen 1920x1080.\n\n" +
               "Change the name / company / version in Project Settings > Player.";
    }

    [MenuItem("Tools/Tetris/Build Windows (x64)")]
    private static void BuildWindows()
    {
        Prepare();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build Windows", "No scenes are enabled in File > Build Profiles.", "OK");
            return;
        }

        string exePath = Path.Combine(OutputFolder, PlayerSettings.productName + ".exe");
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Build Windows",
                $"Build succeeded in {summary.totalTime.TotalSeconds:0}s ({summary.totalSize / (1024f * 1024f):0.0} MB).\n\n" +
                $"{Path.GetFullPath(exePath)}\n\n" +
                "Share the whole Builds/Windows folder (zip it), not just the .exe.", "OK");
            EditorUtility.RevealInFinder(exePath);
        }
        else
        {
            EditorUtility.DisplayDialog("Build Windows",
                $"Build {summary.result} with {summary.totalErrors} error(s). See the Console for details.", "OK");
        }
    }
}
