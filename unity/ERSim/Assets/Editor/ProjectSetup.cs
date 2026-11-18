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
        public const string SkyPath = "Assets/Resources/Sky.mat";

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

            // Sky material as an asset, so the skybox shader is included in builds
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(SkyPath) == null)
            {
                var sky = new UnityEngine.Material(UnityEngine.Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetColor("_SkyTint", new UnityEngine.Color(0.45f, 0.55f, 0.70f));
                sky.SetColor("_GroundColor", new UnityEngine.Color(0.36f, 0.56f, 0.30f));
                sky.SetFloat("_Exposure", 1.2f);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("ERSim: project setup applied");
        }
    }

    /// <summary>Applies the setup when the project is opened and the scene or sky asset is missing.</summary>
    [InitializeOnLoad]
    static class ProjectSetupOnFirstOpen
    {
        static ProjectSetupOnFirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                bool hasScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePath) != null;
                bool hasSky = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(ProjectSetup.SkyPath) != null;
                if (hasScene && hasSky) return;
                ProjectSetup.Run();
                if (!hasScene) EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            };
        }
    }
}
