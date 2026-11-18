using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ERSim.EditorTools
{
    /// <summary>
    /// Windows build into unity/ERSim/Builds/Windows (ignored by git).
    /// Batch: Unity -batchmode -quit -projectPath unity/ERSim -executeMethod ERSim.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        const string Output = "Builds/Windows/ERSim.exe";

        [MenuItem("ERSim/Build Windows")]
        public static void BuildWindows()
        {
            ProjectSetup.Run();   // event logs are synced by DataSyncBeforeBuild

            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"ERSim: build {s.result}, {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB -> {Path.GetFullPath(Output)}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
