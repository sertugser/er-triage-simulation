using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ERSim.EditorTools
{
    /// <summary>
    /// Copies the event logs from the repository's data/ folder into StreamingAssets.
    /// data/ is the single source; the copies are not committed (see .gitignore).
    /// Runs when the editor loads and before every build.
    /// </summary>
    [InitializeOnLoad]
    public static class DataSync
    {
        static DataSync()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += () => Sync(false);
        }

        public static string DataDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "data"));

        public static string TargetDir => Path.Combine(Application.dataPath, "StreamingAssets");

        [MenuItem("ERSim/Sync event logs from data")]
        static void SyncMenu() => Sync(true);

        /// <returns>Number of event logs available in StreamingAssets.</returns>
        public static int Sync(bool log)
        {
            if (!Directory.Exists(DataDir))
            {
                Debug.LogWarning("ERSim: data folder not found at " + DataDir);
                return 0;
            }
            Directory.CreateDirectory(TargetDir);

            int copied = 0, total = 0;
            foreach (var src in Directory.GetFiles(DataDir, "events_*.csv"))
            {
                total++;
                var dst = Path.Combine(TargetDir, Path.GetFileName(src));
                if (File.Exists(dst) && Same(src, dst)) continue;
                File.Copy(src, dst, true);
                copied++;
            }
            if (copied > 0) AssetDatabase.Refresh();
            if (log || copied > 0)
                Debug.Log($"ERSim: {total} event logs in data/, {copied} copied to StreamingAssets");
            return total;
        }

        static bool Same(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return File.ReadAllText(a) == File.ReadAllText(b);
        }
    }

    /// <summary>Syncs the event logs before any build, including File > Build Profiles.</summary>
    class DataSyncBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (DataSync.Sync(true) == 0)
                Debug.LogWarning("ERSim: no event logs in data/; the build will show a load error");
        }
    }
}
