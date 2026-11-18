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

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.47f, 0.49f);

            // The main scene is empty; reuse a camera or light only if one was placed anyway
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 0.5f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

                // fill from the opposite side so walls facing away from the sun are not dark
                var fill = new GameObject("Fill").AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.25f;
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            }

            var layout = new GameObject("EmergencyRoom").AddComponent<ErLayout>();
            layout.Build();

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.83f, 0.93f);
            cam.fieldOfView = 45f;
            cam.farClipPlane = 300f;
            var view = Quaternion.Euler(48f, 0f, 0f);
            cam.transform.rotation = view;
            cam.transform.position = layout.Center + view * new Vector3(0f, 0f, -50f);
            QualitySettings.shadowDistance = 120f;

            var replay = new GameObject("Replay").AddComponent<ReplayController>();
            replay.Layout = layout;

            replay.Load(6, 3);   // base scenario until the UI can choose
            replay.Playing = true;
        }
    }
}
