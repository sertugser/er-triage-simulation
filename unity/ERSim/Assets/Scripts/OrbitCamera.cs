using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Orbit (left drag), pan (right or middle drag) and zoom (mouse wheel) around a target point.
    /// Uses IMGUI events so it works with either input backend.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 Target;
        public float Distance = 47f;
        public float Yaw = 0f;
        public float Pitch = 44f;
        public float MinDistance = 4f, MaxDistance = 120f;
        public float MinPitch = 10f, MaxPitch = 89f;

        /// <summary>Screen area (GUI coordinates) where drags belong to the UI, not the camera.</summary>
        public static Rect BlockedRect;

        bool dragging;

        void OnGUI()
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    dragging = !BlockedRect.Contains(e.mousePosition) && GUIUtility.hotControl == 0;
                    break;
                case EventType.MouseUp:
                    dragging = false;
                    break;
                case EventType.MouseDrag:
                    if (!dragging) break;
                    if (e.button == 0)
                    {
                        Yaw += e.delta.x * 0.3f;
                        Pitch = Mathf.Clamp(Pitch + e.delta.y * 0.2f, MinPitch, MaxPitch);
                    }
                    else
                    {
                        // move the target over the ground; the scene follows the cursor
                        var turn = Quaternion.Euler(0f, Yaw, 0f);
                        float k = Distance * 0.0016f;
                        Target += turn * new Vector3(-e.delta.x * k, 0f, e.delta.y * k);
                        Target = new Vector3(Mathf.Clamp(Target.x, -60f, 60f), 0f, Mathf.Clamp(Target.z, -45f, 30f));
                    }
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    if (BlockedRect.Contains(e.mousePosition)) break;
                    Distance = Mathf.Clamp(Distance * (1f + e.delta.y * 0.05f), MinDistance, MaxDistance);
                    e.Use();
                    break;
            }
        }

        void LateUpdate()
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = Target + rot * new Vector3(0f, 0f, -Distance);
            transform.rotation = rot;
        }
    }
}
