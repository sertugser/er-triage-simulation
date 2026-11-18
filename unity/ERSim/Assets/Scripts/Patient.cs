using System.Collections.Generic;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// A patient: a rigged character that walks along waypoints, idles in queues
    /// and sits on the bed. Severity is shown by the disc under the feet and the
    /// marker above the head.
    ///
    /// Movement is a list of legs. A route leg (through a door) is always walked to
    /// its end, so nobody cuts through a wall when the log moves them on early; a
    /// free leg (a place in an open hall) can be retargeted at any time.
    /// </summary>
    public class Patient : MonoBehaviour
    {
        /// <summary>Walking speed in metres per simulated second.</summary>
        public const float WalkingPace = 1.8f;

        /// <summary>Simulated seconds per real second, set by the replay each frame.</summary>
        public static float TimeScale = 1f;

        const float TurnRate = 540f;   // degrees per simulated second

        class Leg
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            public int Next;        // index of the point being walked to
            public float Yaw;       // facing when the leg is finished
            public bool Sit;
            public bool Route;      // must be completed
        }

        readonly List<Leg> legs = new List<Leg>();
        float restYaw = 90f;
        bool sitAtRest;
        Animation anim;
        string playing;

        // Variants used for patients; the others are staff
        static readonly string[] Variants =
            { "female-a", "male-a", "female-b", "male-b", "female-c", "male-e" };

        public static Patient Create(int id, Color severity, Vector3 position, Transform parent)
        {
            var go = new GameObject("Patient_" + id);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var p = go.AddComponent<Patient>();

            var model = Models.Character(Variants[(id * 7 + id / 7) % Variants.Length], go.transform);
            p.anim = model.GetComponent<Animation>();

            Models.Primitive(PrimitiveType.Cylinder, "SeverityDisc", position + new Vector3(0f, 0.04f, 0f),
                new Vector3(0.8f, 0.012f, 0.8f), severity, go.transform);
            Models.Primitive(PrimitiveType.Sphere, "SeverityMarker", position + new Vector3(0f, 1.35f, 0f),
                new Vector3(0.22f, 0.22f, 0.22f), severity, go.transform);
            return p;
        }

        /// <summary>Walk straight to a place in an open hall, then stand facing the given yaw.</summary>
        public void GoTo(Vector3 target, float yaw)
        {
            Leg leg = legs.Count > 0 && !legs[legs.Count - 1].Route ? legs[legs.Count - 1] : null;
            if (leg == null)
            {
                if (legs.Count == 0 && !sitAtRest && (transform.position - target).sqrMagnitude < 1e-6f)
                {
                    restYaw = yaw;
                    return;
                }
                leg = new Leg();
                legs.Add(leg);
            }
            leg.Points.Clear();
            leg.Points.Add(target);
            leg.Next = 0;
            leg.Yaw = yaw;
        }

        /// <summary>Walk through all points (a route through a door); optionally sit at the last one.</summary>
        public void Follow(List<Vector3> points, float yaw, bool sit)
        {
            // a place in a hall that was not reached yet is given up; routes are kept
            if (legs.Count > 0 && !legs[legs.Count - 1].Route) legs.RemoveAt(legs.Count - 1);
            var leg = new Leg { Yaw = yaw, Sit = sit, Route = true };
            leg.Points.AddRange(points);
            legs.Add(leg);
        }

        public bool Arrived => legs.Count == 0;

        /// <summary>Jump to the end of everything planned (used when seeking).</summary>
        public void Snap()
        {
            if (legs.Count > 0)
            {
                var last = legs[legs.Count - 1];
                transform.position = last.Points[last.Points.Count - 1];
                restYaw = last.Yaw;
                sitAtRest = last.Sit;
                legs.Clear();
            }
            transform.rotation = Quaternion.Euler(0f, restYaw, 0f);
        }

        void Update()
        {
            float step = WalkingPace * TimeScale * Time.deltaTime;
            while (legs.Count > 0 && step > 0f)
            {
                var leg = legs[0];
                var next = leg.Points[leg.Next];
                var delta = next - transform.position;
                float dist = delta.magnitude;
                var flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > 1e-6f) Face(Quaternion.LookRotation(flat));
                if (dist <= step)
                {
                    transform.position = next;
                    step -= dist;
                    if (++leg.Next >= leg.Points.Count)
                    {
                        restYaw = leg.Yaw;
                        sitAtRest = leg.Sit;
                        legs.RemoveAt(0);
                    }
                }
                else
                {
                    transform.position += delta / dist * step;
                    step = 0f;
                }
            }

            if (legs.Count > 0)
            {
                Play("walk");
                // fast playback is a fast-forward of the same walk
                if (anim != null && anim["walk"] != null)
                    anim["walk"].speed = Mathf.Clamp(TimeScale, 1f, 6f);
            }
            else
            {
                Face(Quaternion.Euler(0f, restYaw, 0f));
                Play(sitAtRest ? "sit" : "idle");
            }
        }

        void Face(Quaternion target) =>
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target,
                TurnRate * Mathf.Max(1f, TimeScale) * Time.deltaTime);

        void Play(string clip)
        {
            if (anim == null || playing == clip) return;
            playing = clip;
            if (anim[clip] != null) anim.CrossFade(clip, 0.15f);
        }
    }
}
