using System.Collections.Generic;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Loads the third-party models (Kenney, CC0) from Resources/Models and places them
    /// from code. Furniture is positioned by the bottom centre of its bounds, so the
    /// different pivots of the source files do not matter.
    /// </summary>
    public static class Models
    {
        public const float FurnitureScale = 0.2f;   // the kit is modelled at 5 units per metre
        public const float CharacterScale = 1.5f;

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        static GameObject Load(string path)
        {
            if (!cache.TryGetValue(path, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + path);
                if (prefab == null) Debug.LogError("ERSim: model not found: Resources/Models/" + path);
                cache[path] = prefab;
            }
            return prefab;
        }

        /// <param name="pos">Bottom centre of the placed object.</param>
        /// <param name="yaw">Rotation around the vertical axis, degrees.</param>
        public static GameObject Furniture(string name, Vector3 pos, float yaw, Transform parent, float scale = 1f)
        {
            return Place("Furniture/" + name, name, pos, yaw, parent, FurnitureScale * scale);
        }

        public static GameObject Prop(string name, Vector3 pos, float yaw, Transform parent, float scale = 1f)
        {
            return Place("Props/" + name, name, pos, yaw, parent, CharacterScale * scale);
        }

        static GameObject Place(string path, string name, Vector3 pos, float yaw, Transform parent, float scale)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;

            var prefab = Load(path);
            if (prefab == null) return root;

            var model = Object.Instantiate(prefab, root.transform);
            model.transform.localScale = prefab.transform.localScale * scale;
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                model.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            }
            return root;
        }

        /// <summary>A rigged character standing on the parent origin, facing the parent +z.</summary>
        public static GameObject Character(string variant, Transform parent)
        {
            var prefab = Load("Characters/character-" + variant);
            if (prefab == null)
            {
                // visible fallback so a missing model is obvious but not fatal
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(parent);
                capsule.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
                capsule.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                return capsule;
            }
            var model = Object.Instantiate(prefab, parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = prefab.transform.rotation;
            model.transform.localScale = prefab.transform.localScale * CharacterScale;
            return model;
        }

        /// <summary>Coloured primitive (floors, markers) without a material per object.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Vector3 center, Vector3 size, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = size;
            Object.Destroy(go.GetComponent<Collider>());
            Paint(go, color);
            return go;
        }

        static MaterialPropertyBlock block;

        public static void Paint(GameObject go, Color color)
        {
            if (block == null) block = new MaterialPropertyBlock();
            var r = go.GetComponent<Renderer>();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color);       // Built-in Standard shader
            block.SetColor("_BaseColor", color);   // URP Lit, if the project is ever switched
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Hides a wall while the camera is on its outer side, so the inside of the
    /// building stays visible from every orbit angle.
    /// </summary>
    public class CutawayWall : MonoBehaviour
    {
        public Vector3 Inward;   // horizontal direction pointing into the room
        Renderer[] renderers;
        bool shown = true;

        void Start() => renderers = GetComponentsInChildren<Renderer>();

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            bool show = Vector3.Dot(Inward, cam.transform.position - transform.position) > 0f;
            if (show == shown) return;
            shown = show;
            foreach (var r in renderers) r.enabled = show;
        }
    }
}
