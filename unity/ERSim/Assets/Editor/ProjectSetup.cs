using UnityEditor;
using UnityEditor.SceneManagement;

namespace ERSim.EditorTools
{
    /// <summary>
    /// Version-control settings and the (empty) main scene. Everything visible in the
    /// scene is created at runtime by ERSim.Bootstrap.
    /// Batch: Unity -batchmode -quit -projectPath unity/ERSim -executeMethod ERSim.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("ERSim/Apply project setup")]
        public static void Run()
        {
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSettings.serializationMode = SerializationMode.ForceText;

            PlayerSettings.companyName = "ER Triage Simulation";
            PlayerSettings.productName = "ERSim";
            PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                    AssetDatabase.CreateFolder("Assets", "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("ERSim: project setup applied");
        }
    }

    /// <summary>Applies the setup the first time the project is opened (no scene yet).</summary>
    [InitializeOnLoad]
    static class ProjectSetupOnFirstOpen
    {
        static ProjectSetupOnFirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePath) != null) return;
                ProjectSetup.Run();
                EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            };
        }
    }
}
