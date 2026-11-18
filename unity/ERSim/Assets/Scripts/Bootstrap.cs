using UnityEngine;

namespace ERSim
{
    /// <summary>Builds the whole scene from code when the scene loads.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<ErLayout>() != null) return;
            Application.runInBackground = true;   // keep playing when the window loses focus

            // sky: procedural skybox (Resources/Sky.mat, made by ProjectSetup) lit by the sun
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.53f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.46f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.36f, 0.31f);
            var sky = Resources.Load<Material>("Sky");
            if (sky != null) RenderSettings.skybox = sky;

            // The main scene is empty; reuse a camera or light only if one was placed anyway
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 0.5f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.color = new Color(1f, 0.97f, 0.90f);
                RenderSettings.sun = sun;

                // fill from the opposite side so walls facing away from the sun are not dark
                var fill = new GameObject("Fill").AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.25f;
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            }

            var layout = new GameObject("EmergencyRoom").AddComponent<ErLayout>();
            layout.Build();
            Surroundings.Build(layout.transform);

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.83f, 0.93f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 3000f;
            cam.gameObject.AddComponent<OrbitCamera>().Target = layout.Center;
            QualitySettings.shadowDistance = 140f;

            var replay = new GameObject("Replay").AddComponent<ReplayController>();
            replay.Layout = layout;

            var ui = replay.gameObject.AddComponent<ReplayUI>();
            ui.Replay = replay;
            ui.Layout = layout;
            ui.Cam = cam;

            replay.gameObject.AddComponent<Capture>().Init(replay, ui, cam.GetComponent<OrbitCamera>());
        }
    }
}
