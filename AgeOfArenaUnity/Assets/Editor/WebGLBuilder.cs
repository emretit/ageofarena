#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// WebGL build pipeline. The project has no authored .unity scene — the whole world is
/// built at runtime by <see cref="GameBootstrap"/> (RuntimeInitializeOnLoadMethod). A build
/// still needs at least one scene in the player, so we generate an empty Boot.unity on demand
/// and ship only that; GameBootstrap takes over on the first frame.
///
/// Menu:  Build > WebGL Build
/// CLI :  Unity -quit -batchmode -projectPath . -executeMethod WebGLBuilder.BuildFromCommandLine \
///              -buildOutput Builds/WebGL -logFile -
/// </summary>
public static class WebGLBuilder
{
    const string BootScenePath = "Assets/Scenes/Boot.unity";
    const string DefaultOutput = "Builds/WebGL";

    [MenuItem("Build/WebGL Build")]
    public static void BuildMenu()
    {
        Build(AbsoluteOutput(DefaultOutput));
        EditorUtility.RevealInFinder(AbsoluteOutput(DefaultOutput));
    }

    /// <summary>Entry point for headless/CI builds. Reads optional <c>-buildOutput &lt;path&gt;</c>.</summary>
    public static void BuildFromCommandLine()
    {
        string output = DefaultOutput;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildOutput") output = args[i + 1];

        Build(AbsoluteOutput(output));
    }

    static string AbsoluteOutput(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path);

    static void Build(string outputPath)
    {
        EnsureBootScene();

        // WebGL player settings — tuned for a playable first build.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        // Disabled compression = no server Content-Encoding config needed for local `python -m http.server`.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        // FullWithStacktrace so a runtime C# exception prints a readable trace instead of
        // silently halting the player. Switch back to None for size once the build is stable.
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
        PlayerSettings.WebGL.dataCaching = true;
        // Let the heap grow instead of pre-reserving a huge fixed block.
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { BootScenePath },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        Debug.Log($"[WebGLBuilder] Building WebGL → {outputPath}");
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"[WebGLBuilder] Result={s.result} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime} output={outputPath}");

        if (Application.isBatchMode)
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    static void EnsureBootScene()
    {
        if (File.Exists(BootScenePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(BootScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, BootScenePath);
        AssetDatabase.Refresh();
    }
}
#endif
