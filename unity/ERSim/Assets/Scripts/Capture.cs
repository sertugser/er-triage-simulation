using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Screenshots from the command line, for checking the scene and for the report:
    ///   ERSim.exe -shots "out1.png,300,0,0,48,44,4,0|out2.png,300,0,40,30,12,13.6,4.6"
    /// Each shot is: file, sim time (min), scenario index, yaw, pitch, distance, target x, target z.
    /// The player quits after the last shot. Without -shots this component does nothing.
    /// </summary>
    public class Capture : MonoBehaviour
    {
        ReplayController replay;
        ReplayUI ui;
        OrbitCamera orbit;

        public void Init(ReplayController replay, ReplayUI ui, OrbitCamera orbit)
        {
            this.replay = replay;
            this.ui = ui;
            this.orbit = orbit;
        }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-shots");
            if (i < 0 || i + 1 >= args.Length) yield break;

            yield return null;   // let the UI load its first scenario
            foreach (var shot in args[i + 1].Split('|'))
            {
                var f = shot.Split(',');
                if (f.Length != 8) { Debug.LogError("ERSim: bad -shots entry: " + shot); continue; }
                float N(int k) => float.Parse(f[k], CultureInfo.InvariantCulture);

                ui.SelectScenario((int)N(2));
                replay.Seek(N(1));
                replay.Speed = 1f;
                replay.Playing = true;
                orbit.Yaw = N(3);
                orbit.Pitch = N(4);
                orbit.Distance = N(5);
                orbit.Target = new Vector3(N(6), 0f, N(7));

                yield return new WaitForSeconds(1.5f);   // animations settle, walkers get under way
                ScreenCapture.CaptureScreenshot(f[0]);
                yield return new WaitForSeconds(0.5f);
                Debug.Log("ERSim: screenshot " + f[0]);
            }
            Application.Quit();
        }
    }
}
