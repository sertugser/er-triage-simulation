using System.Collections.Generic;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// A patient: a rigged character that walks along waypoints, idles in queues
    /// and sits on the bed. Severity is shown by the disc under the feet and the
    /// marker above the head.
    /// </summary>
    public class Patient : MonoBehaviour
    {
        public static float MoveRate = 3.5f;   // metres per real second
        const float TurnRate = 720f;           // degrees per real second

        readonly Queue<Vector3> path = new Queue<Vector3>();
        Vector3 destination;
        float restYaw;
        bool sitAtRest;
        Animation anim;
        string playing;

        // Variants used for patients; the others are staff
        static readonly string[] Variants =
            { "female-a", "male-a", "female-b", "male-b", "female-c", "male-d" };

        public static Patient Create(int id, Color severity, Vector3 position, Transform parent)
        {
            var go = new GameObject("Patient_" + id);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var p = go.AddComponent<Patient>();
            p.destination = position;
            p.restYaw = 90f;

            var model = Models.Character(Variants[(id * 7 + id / 7) % Variants.Length], go.transform);
            p.anim = model.GetComponent<Animation>();

            Models.Primitive(PrimitiveType.Cylinder, "SeverityDisc", position + new Vector3(0f, 0.04f, 0f),
                new Vector3(0.8f, 0.012f, 0.8f), severity, go.transform);
            Models.Primitive(PrimitiveType.Sphere, "SeverityMarker", position + new Vector3(0f, 1.35f, 0f),
                new Vector3(0.22f, 0.22f, 0.22f), severity, go.transform);
            return p;
        }

        /// <summary>Walk straight to one point, then stand facing the given yaw.</summary>
        public void GoTo(Vector3 target, float yaw)
        {
            restYaw = yaw;
            sitAtRest = false;
            if (path.Count == 0 && target == destination) return;
            if (path.Count == 1 && path.Peek() == target) return;
            path.Clear();
            path.Enqueue(target);
            destination = target;
        }

        /// <summary>Walk through all points; optionally sit at the last one.</summary>
        public void Follow(List<Vector3> points, float yaw, bool sit)
        {
            path.Clear();
            foreach (var p in points) path.Enqueue(p);
            destination = points[points.Count - 1];
            restYaw = yaw;
            sitAtRest = sit;
        }

        public bool Arrived => path.Count == 0;

        /// <summary>Jump to the end of the current route (used when seeking).</summary>
        public void Snap()
        {
            path.Clear();
            transform.position = destination;
            transform.rotation = Quaternion.Euler(0f, restYaw, 0f);
        }

        void Update()
        {
            float step = MoveRate * Time.deltaTime;
            while (path.Count > 0 && step > 0f)
            {
                var next = path.Peek();
                var delta = next - transform.position;
                float dist = delta.magnitude;
                var flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > 1e-6f) Face(Quaternion.LookRotation(flat));
                if (dist <= step)
                {
                    transform.position = next;
                    step -= dist;
                    path.Dequeue();
                }
                else
                {
                    transform.position += delta / dist * step;
                    step = 0f;
                }
            }

            if (path.Count > 0)
            {
                Play("walk");
            }
            else
            {
                Face(Quaternion.Euler(0f, restYaw, 0f));
                Play(sitAtRest ? "sit" : "idle");
            }
        }

        void Face(Quaternion target) =>
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnRate * Time.deltaTime);

        void Play(string clip)
        {
            if (anim == null || playing == clip) return;
            playing = clip;
            if (anim[clip] != null) anim.CrossFade(clip, 0.15f);
        }
    }
}
