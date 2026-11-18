using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Everything outside the building: lawn to the horizon, pavement, a car park with
    /// ambulances at the entrance, a road and trees. Decoration only; it has no effect
    /// on the replay.
    /// </summary>
    public static class Surroundings
    {
        static readonly Color Grass = new Color(0.36f, 0.56f, 0.30f);
        static readonly Color Asphalt = new Color(0.27f, 0.28f, 0.30f);
        static readonly Color Pavement = new Color(0.70f, 0.70f, 0.69f);
        static readonly Color Marking = new Color(0.92f, 0.92f, 0.88f);

        const float VehicleScale = 1.6f;

        // Building footprint plus margin, car park and road: nothing is planted here
        static readonly Rect KeepOut = Rect.MinMaxRect(-52f, -42f, 40f, 12f);

        public static void Build(Transform parent)
        {
            var root = new GameObject("Surroundings").transform;
            root.SetParent(parent);

            Box(root, "Lawn", new Vector3(0f, -0.11f, 0f), new Vector3(4000f, 0.1f, 4000f), Grass);
            Box(root, "Pavement", new Vector3(4f, -0.045f, 0f), new Vector3(54f, 0.04f, 20f), Pavement);

            // road along the south side with a dashed centre line
            Box(root, "Road", new Vector3(0f, -0.04f, -36f), new Vector3(1200f, 0.04f, 9f), Asphalt);
            for (int i = -40; i <= 40; i++)
                Box(root, "RoadLine", new Vector3(i * 6f, -0.015f, -36f), new Vector3(2.5f, 0.01f, 0.2f), Marking);

            // car park west of the entrance, joined to the road
            Box(root, "CarPark", new Vector3(-36f, -0.04f, -10.5f), new Vector3(27f, 0.04f, 42f), Asphalt);
            string[] cars = { "sedan", "suv", null, "van", "taxi", "hatchback-sports", null, "sedan" };
            for (int i = 0; i <= cars.Length; i++)
                Box(root, "BayLine", new Vector3(-45.5f, -0.015f, 6.5f - i * 3.2f), new Vector3(5.5f, 0.01f, 0.15f), Marking);
            for (int i = 0; i < cars.Length; i++)
                if (cars[i] != null)
                    Models.Place("Vehicles/" + cars[i], new Vector3(-45.5f, 0f, 4.9f - i * 3.2f), 90f, root, VehicleScale);

            // ambulances parked beside the entrance
            Models.Place("Vehicles/ambulance", new Vector3(-24.5f, 0f, 5.5f), 180f, root, VehicleScale);
            Models.Place("Vehicles/ambulance", new Vector3(-24.5f, 0f, -5.5f), 0f, root, VehicleScale);

            // path from the exit down to the road
            Box(root, "ExitPath", new Vector3(33f, -0.03f, -14f), new Vector3(3f, 0.04f, 35f), Pavement);

            // hedge and a row of trees behind the building
            string[] trees = { "tree_oak", "tree_default", "tree_detailed", "tree_fat", "tree_pineRoundA" };
            for (int i = 0; i < 14; i++)
                Models.Place("Nature/" + trees[i % 3], new Vector3(-32f + i * 5.6f, 0f, 14.5f + (i % 2) * 1.5f), i * 67f, root, 4.2f + (i % 3) * 0.5f);
            for (int i = 0; i < 24; i++)
                Models.Place("Nature/plant_bushLarge", new Vector3(-19f + i * 2f, 0f, 9.2f), i * 40f, root, 3f);

            // scattered trees and bushes, same every run
            var random = new System.Random(7);
            int placed = 0;
            while (placed < 170)
            {
                float x = -160f + (float)random.NextDouble() * 330f;
                float z = -130f + (float)random.NextDouble() * 250f;
                float yaw = (float)random.NextDouble() * 360f;
                float size = 0.8f + (float)random.NextDouble() * 0.5f;
                int kind = random.Next(trees.Length + 1);
                if (KeepOut.Contains(new Vector2(x, z)) || Mathf.Abs(z + 36f) < 7f) continue;
                if (kind < trees.Length)
                    Models.Place("Nature/" + trees[kind], new Vector3(x, 0f, z), yaw, root, 4.5f * size);
                else
                    Models.Place("Nature/plant_bush", new Vector3(x, 0f, z), yaw, root, 3.5f * size);
                placed++;
            }
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Color color) =>
            Models.Primitive(PrimitiveType.Cube, name, center, size, color, parent);
    }
}
