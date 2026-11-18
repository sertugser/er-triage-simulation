using System.Collections.Generic;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Gives staff their uniforms. The characters are coloured through a shared palette
    /// texture: each triangle of the body points at one palette cell. An outfit moves the
    /// triangles of some cells to other cells (for example the dark suit jacket to white),
    /// optionally only above a height, so trousers can stay as they are.
    /// </summary>
    public static class StaffOutfit
    {
        const int Columns = 16, Rows = 4;   // palette layout of colormap.png

        public struct Rule
        {
            public int FromCol, FromRow, ToCol, ToRow;
            public float MinHeight;   // model units above the feet; 0 = whole body

            public Rule(int fromCol, int fromRow, int toCol, int toRow, float minHeight = 0f)
            {
                FromCol = fromCol; FromRow = fromRow; ToCol = toCol; ToRow = toRow; MinHeight = minHeight;
            }
        }

        // Palette cells: (3,3) slate, (7,3) dark, (15,3) white, (3,1) light blue
        /// <summary>Nurse (from female-d): light blue scrubs, white shoes.</summary>
        public static readonly Rule[] Scrubs =
        {
            new Rule(3, 3, 3, 1),
            new Rule(7, 3, 15, 3),
        };

        /// <summary>Physician (from male-d): white coat over a light blue shirt, dark trousers.</summary>
        public static readonly Rule[] WhiteCoat =
        {
            new Rule(15, 3, 3, 1),
            new Rule(7, 3, 15, 3, 0.13f),
        };

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static void Apply(GameObject character, string key, Rule[] rules)
        {
            foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.name.StartsWith("body")) continue;   // hair and face stay as they are
                string id = key + "/" + r.sharedMesh.name + "/" + r.sharedMesh.GetInstanceID();
                if (!cache.TryGetValue(id, out var mesh))
                    cache[id] = mesh = Recolour(r, character.transform, rules);
                if (mesh != null) r.sharedMesh = mesh;
            }
        }

        static Mesh Recolour(SkinnedMeshRenderer renderer, Transform root, Rule[] rules)
        {
            var source = renderer.sharedMesh;
            if (!source.isReadable)
            {
                Debug.LogWarning("ERSim: " + source.name + " is not readable; staff keep their default clothes");
                return null;
            }

            var vertices = new List<Vector3>(source.vertices);
            var normals = new List<Vector3>(source.normals);
            var uvs = new List<Vector2>(source.uv);
            var weights = new List<BoneWeight>(source.boneWeights);
            var triangles = source.triangles;

            // height of each vertex above the feet, in model units (bind pose)
            float scale = root.lossyScale.y;
            var height = new float[vertices.Count];
            for (int i = 0; i < height.Length; i++)
                height[i] = (renderer.transform.TransformPoint(vertices[i]).y - root.position.y) / scale;

            // A vertex shared by a recoloured and an untouched triangle is split,
            // otherwise the colour would smear across the palette between them.
            var moved = new Dictionary<long, int>();   // (vertex, rule) -> new vertex
            var usedUnchanged = new bool[vertices.Count];
            var pending = new List<int[]>();           // triangle start index, rule index

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int rule = Match(triangles, t, uvs, height, rules);
                if (rule < 0)
                    for (int k = 0; k < 3; k++) usedUnchanged[triangles[t + k]] = true;
                else
                    pending.Add(new[] { t, rule });
            }

            foreach (var p in pending)
            {
                var rule = rules[p[1]];
                var shift = new Vector2((rule.ToCol - rule.FromCol) / (float)Columns, -(rule.ToRow - rule.FromRow) / (float)Rows);
                for (int k = 0; k < 3; k++)
                {
                    int v = triangles[p[0] + k];
                    long keyOf = (long)v * rules.Length + p[1];
                    if (!moved.TryGetValue(keyOf, out int target))
                    {
                        if (usedUnchanged[v] || HasOtherRule(moved, v, rules.Length, p[1]))
                        {
                            target = vertices.Count;
                            vertices.Add(vertices[v]);
                            if (normals.Count > 0) normals.Add(normals[v]);
                            uvs.Add(uvs[v] + shift);
                            if (weights.Count > 0) weights.Add(weights[v]);
                        }
                        else
                        {
                            target = v;
                            uvs[v] += shift;
                        }
                        moved[keyOf] = target;
                    }
                    triangles[p[0] + k] = target;
                }
            }

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "-outfit";
            mesh.SetVertices(vertices);
            if (normals.Count > 0) mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (weights.Count > 0) mesh.boneWeights = weights.ToArray();
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        static bool HasOtherRule(Dictionary<long, int> moved, int vertex, int ruleCount, int rule)
        {
            for (int r = 0; r < ruleCount; r++)
                if (r != rule && moved.TryGetValue((long)vertex * ruleCount + r, out int target) && target == vertex)
                    return true;
            return false;
        }

        /// <returns>Index of the rule that applies to the whole triangle, or -1.</returns>
        static int Match(int[] triangles, int t, List<Vector2> uvs, float[] height, Rule[] rules)
        {
            float centre = (height[triangles[t]] + height[triangles[t + 1]] + height[triangles[t + 2]]) / 3f;
            for (int r = 0; r < rules.Length; r++)
            {
                if (centre < rules[r].MinHeight) continue;
                bool all = true;
                for (int k = 0; k < 3 && all; k++)
                {
                    var uv = uvs[triangles[t + k]];
                    int col = Mathf.Clamp(Mathf.FloorToInt(uv.x * Columns), 0, Columns - 1);
                    int row = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * Rows), 0, Rows - 1);
                    all = col == rules[r].FromCol && row == rules[r].FromRow;
                }
                if (all) return r;
            }
            return -1;
        }
    }
}
