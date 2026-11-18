using UnityEngine;

namespace ERSim
{
    /// <summary>On-screen clock, counters and controls (IMGUI, no assets needed).</summary>
    public class ReplayUI : MonoBehaviour
    {
        public ReplayController Replay;
        public ErLayout Layout;
        public Camera Cam;

        struct Scenario
        {
            public string Name; public int Rooms; public int Interarrival;
            public Scenario(string n, int r, int ia) { Name = n; Rooms = r; Interarrival = ia; }
            public string Label => $"{Name}: {Rooms} rooms, IA {Interarrival}";
        }

        // docs/assumptions.md, "Scenarios"
        static readonly Scenario[] Scenarios =
        {
            new Scenario("Base", 6, 3),
            new Scenario("A", 4, 6),
            new Scenario("B", 8, 6),
            new Scenario("C", 4, 3),
            new Scenario("D", 8, 3),
        };
        static readonly float[] Speeds = { 1f, 10f, 60f };
        static readonly string[] SpeedLabels = { "1x", "10x", "60x" };

        int scenario;
        int speed = 1;
        GUIStyle box, title, label, small, worldLabel;
        Texture2D swatch;
        const float PanelWidth = 300f, PanelHeight = 580f;
        Vector2 scroll;

        void Start()
        {
            LoadScenario(0);
            Replay.Playing = true;
        }

        /// <summary>Loads a scenario by index and rewinds (also used by Capture).</summary>
        public void SelectScenario(int i) => LoadScenario(Mathf.Clamp(i, 0, Scenarios.Length - 1));

        void LoadScenario(int i)
        {
            scenario = i;
            var s = Scenarios[i];
            Replay.Load(s.Rooms, s.Interarrival);
            Replay.Speed = Speeds[speed];
        }

        void Styles()
        {
            if (box != null) return;
            box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
            title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            worldLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.1f, 0.12f, 0.16f) },
            };
            swatch = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            Styles();
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            DrawWorldLabels(scale);

            var s = Replay.State;   // state columns of the last applied CSV row
            float height = Mathf.Min(PanelHeight, Screen.height / scale - 20f);
            var panel = new Rect(10, 10, PanelWidth, height);
            OrbitCamera.BlockedRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            GUILayout.BeginArea(panel, box);
            scroll = GUILayout.BeginScrollView(scroll);   // only scrolls when the window is short

            GUILayout.Label("ER triage replay", title);
            GUILayout.Label(Scenarios[scenario].Label, small);
            GUILayout.Space(6);

            int minutes = Mathf.FloorToInt(Replay.SimTime);
            GUILayout.Label($"Clock  {minutes / 60:00}:{minutes % 60:00}   (t = {Replay.SimTime:0.0} / {Replay.RunLength:0} min)", label);
            GUILayout.Label($"Triage queue   {s.TriageQueue}", label);
            GUILayout.Label($"Nurse          {(s.NurseBusy == 1 ? "busy" : "idle")}", label);
            GUILayout.Label($"Room queue     {s.RoomQueue}", label);
            GUILayout.Label($"Rooms busy     {s.RoomsBusy} / {Replay.Rooms}", label);
            if (Replay.Finished) GUILayout.Label("End of run", title);
            if (Replay.Error != null) GUILayout.Label(Replay.Error, small);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Replay.Playing ? "Pause" : "Play", GUILayout.Height(28)))
            {
                if (Replay.Finished) Replay.Restart();
                Replay.Playing = !Replay.Playing;
            }
            if (GUILayout.Button("Restart", GUILayout.Height(28)))
                Replay.Restart();
            GUILayout.EndHorizontal();

            GUILayout.Label("Speed (1x = 1 simulated min per second)", small);
            int newSpeed = GUILayout.Toolbar(speed, SpeedLabels, GUILayout.Height(26));
            if (newSpeed != speed) { speed = newSpeed; Replay.Speed = Speeds[speed]; }
            GUILayout.Space(6);

            GUILayout.Label("Scenario", small);
            for (int i = 0; i < Scenarios.Length; i++)
            {
                bool on = GUILayout.Toggle(i == scenario, Scenarios[i].Label, GUI.skin.button, GUILayout.Height(24));
                if (on && i != scenario)
                {
                    bool wasPlaying = Replay.Playing || Replay.Finished;
                    LoadScenario(i);
                    Replay.Playing = wasPlaying;
                }
            }
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            Legend(ReplayController.LowColor, "low");
            Legend(ReplayController.MediumColor, "medium");
            Legend(ReplayController.HighColor, "high");
            GUILayout.EndHorizontal();
            GUILayout.Label("Drag: rotate   Wheel: zoom", small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Legend(Color c, string text)
        {
            var r = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
            r.y += 3;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, swatch);
            GUI.color = old;
            GUILayout.Label(text, small, GUILayout.Width(62));
        }

        void DrawWorldLabels(float scale)
        {
            if (Cam == null) return;
            foreach (var kv in Layout.Labels)
            {
                var t = kv.Key;
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                var sp = Cam.WorldToScreenPoint(t.position);
                if (sp.z <= 0) continue;
                var gp = new Vector2(sp.x, Screen.height - sp.y) / scale;
                GUI.Label(new Rect(gp.x - 110, gp.y - 10, 220, 20), kv.Value, worldLabel);
            }
        }
    }
}
