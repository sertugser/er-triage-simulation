using System.Collections.Generic;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Builds the emergency department from code. Patients flow left to right:
    /// entrance -> waiting hall (triage queue) -> triage desk -> second hall (room queue)
    /// -> treatment room -> exit.
    /// </summary>
    public class ErLayout : MonoBehaviour
    {
        public const int MaxRooms = 8;

        // Building footprint in metres. Wall segments are 2 m wide.
        const float West = -20f, East = 28f, South = -7f, North = 7f;
        const float Seg = 2f;
        const float SlotSpacing = 0.95f;

        // Treatment rooms: 4 x 4 m, two rows along a corridor
        const float RoomSize = 4f, RoomPitch = 4.4f, FirstRoomX = 9.2f, RoomRowZ = 4.6f;
        const float SeatHeight = 0.42f;   // top of the mattress

        public const float QueueFacing = 90f;    // queues face the flow direction (+x)
        public const float TriageFacing = 0f;    // towards the nurse (+z)
        public static float SeatFacing(int room) => room <= 4 ? 180f : 0f;   // sitting up in bed, legs towards its foot

        public Vector3 SpawnPoint { get; private set; }     // outside the entrance door
        public Vector3 TriageSpot { get; private set; }     // patient position during triage
        public readonly List<Transform> TriageQueueSlots = new List<Transform>();
        public readonly List<Transform> RoomQueueSlots = new List<Transform>();

        readonly List<GameObject> rooms = new List<GameObject>();
        readonly List<Vector3> roomCenters = new List<Vector3>();

        /// <summary>World-space labels drawn by the UI (anchor, text).</summary>
        public readonly List<KeyValuePair<Transform, string>> Labels = new List<KeyValuePair<Transform, string>>();

        public Vector3 Center => new Vector3(-2f, 0f, -1f);   // left of the middle: the UI panel covers the left edge
        public int RoomCount => rooms.Count;

        static readonly Color Floor = new Color(0.78f, 0.79f, 0.81f);
        static readonly Color WaitingFloor = new Color(0.62f, 0.73f, 0.86f);
        static readonly Color TriageFloor = new Color(0.88f, 0.79f, 0.58f);
        static readonly Color RoomFloor = new Color(0.62f, 0.82f, 0.74f);
        static readonly Color StaffMarker = new Color(0.20f, 0.45f, 0.90f);

        public void Build()
        {
            Floors();
            OuterWalls();
            WaitingHall();
            TriageStation();
            RoomHall();
            for (int i = 0; i < MaxRooms; i++) rooms.Add(Room(i));

            SpawnPoint = new Vector3(West - 2.5f, 0f, 0f);
            Label(new Vector3(West, 3.2f, 0f), "Entrance");
            Label(new Vector3(East, 3.2f, 0f), "Exit");
        }

        /// <summary>Shows rooms 1..count and hides the rest.</summary>
        public void SetVisibleRooms(int count)
        {
            for (int i = 0; i < rooms.Count; i++) rooms[i].SetActive(i < count);
        }

        // ------------------------------------------------------------ patient routes

        float Side(int room) => room <= 4 ? 1f : -1f;            // rooms 1-4 on the far (+z) side
        float DoorX(int room) => roomCenters[room - 1].x - 1f;   // door is in the left half of the front wall

        /// <summary>Where the patient sits during treatment (on the bed, near its head).</summary>
        Vector3 RoomSeat(int room)
        {
            var c = roomCenters[room - 1];
            return new Vector3(c.x + 1.1f, SeatHeight, c.z + Side(room) * 1.15f);
        }

        /// <summary>Corridor -> door -> bed.</summary>
        public List<Vector3> PathToRoom(int room)
        {
            float s = Side(room), x = DoorX(room);
            var seat = RoomSeat(room);
            return new List<Vector3>
            {
                new Vector3(6.6f, 0f, 0f),
                new Vector3(x, 0f, 0f),
                new Vector3(x, 0f, s * (RoomRowZ - 1.0f)),
                new Vector3(seat.x - 0.9f, 0f, seat.z),
                seat,
            };
        }

        /// <summary>Bed -> door -> corridor -> out of the exit door.</summary>
        public List<Vector3> PathFromRoom(int room)
        {
            float s = Side(room), x = DoorX(room);
            var seat = RoomSeat(room);
            return new List<Vector3>
            {
                new Vector3(seat.x - 0.9f, 0f, seat.z),
                new Vector3(x, 0f, s * (RoomRowZ - 1.0f)),
                new Vector3(x, 0f, 0f),
                new Vector3(East - 1f, 0f, 0f),
                new Vector3(East + 2.5f, 0f, 0f),
            };
        }

        // ------------------------------------------------------------ building

        void Floors()
        {
            Box("Floor", new Vector3((West + East) / 2f, 0f, 0f), new Vector3(East - West, 0.04f, North - South), Floor);
            Box("WaitingFloor", new Vector3(-13.9f, 0.005f, 0f), new Vector3(11.6f, 0.04f, 13.4f), WaitingFloor);
            Box("TriageFloor", new Vector3(-5.5f, 0.005f, 0f), new Vector3(4.4f, 0.04f, 13.4f), TriageFloor);
            Box("RoomHallFloor", new Vector3(1.6f, 0.005f, 0f), new Vector3(8.6f, 0.04f, 13.4f), WaitingFloor);
            // paths outside the doors
            Box("EntrancePath", new Vector3(West - 1.75f, -0.005f, 0f), new Vector3(3.5f, 0.04f, 3f), Floor);
            Box("ExitPath", new Vector3(East + 3.25f, -0.005f, 0f), new Vector3(6.5f, 0.04f, 3f), Floor);
        }

        void OuterWalls()
        {
            var parent = Group("OuterWalls");
            // north and south: a window on every other segment
            int n = Mathf.RoundToInt((East - West) / Seg);
            for (int i = 0; i < n; i++)
            {
                float x = West + Seg * (i + 0.5f);
                string piece = i % 2 == 1 ? "wallWindow" : "wall";
                Wall(piece, new Vector3(x, 0f, North), new Vector3(0f, 0f, -1f), parent);
                Wall(piece, new Vector3(x, 0f, South), new Vector3(0f, 0f, 1f), parent);
            }
            // west (entrance) and east (exit): door in the middle
            int m = Mathf.RoundToInt((North - South) / Seg);
            for (int i = 0; i < m; i++)
            {
                float z = South + Seg * (i + 0.5f);
                string piece = Mathf.Abs(z) < 0.1f ? "wallDoorwayWide" : "wall";
                Wall(piece, new Vector3(West, 0f, z), new Vector3(1f, 0f, 0f), parent);
                Wall(piece, new Vector3(East, 0f, z), new Vector3(-1f, 0f, 0f), parent);
            }
        }

        void WaitingHall()
        {
            var parent = Group("WaitingHall");
            Slots("TriageQueueSlots", new Vector3(-8.6f, 0f, 0f), 11, 13, TriageQueueSlots);
            Label(new Vector3(-13.5f, 3.2f, North), "Waiting hall: triage queue");

            // seating along the far wall, plants in the corners, wheelchairs by the door
            for (int i = 0; i < 5; i++)
                Models.Furniture("benchCushion", new Vector3(-18f + i * 2.2f, 0f, North - 0.45f), 180f, parent);
            Models.Furniture("pottedPlant", new Vector3(West + 0.7f, 0f, North - 0.7f), 0f, parent, 0.5f);
            Models.Furniture("pottedPlant", new Vector3(West + 0.7f, 0f, South + 0.7f), 0f, parent, 0.5f);
            Models.Furniture("trashcan", new Vector3(West + 0.6f, 0f, 2.2f), 0f, parent);
            Models.Prop("wheelchair", new Vector3(West + 1.0f, 0f, -2.6f), 90f, parent);
            Models.Prop("wheelchair", new Vector3(West + 1.0f, 0f, -3.6f), 90f, parent);
        }

        void TriageStation()
        {
            var parent = Group("TriageStation");
            TriageSpot = new Vector3(-5.5f, 0f, 0.6f);

            Models.Furniture("desk", new Vector3(-5.5f, 0f, 1.9f), 0f, parent);
            Models.Furniture("computerScreen", new Vector3(-5.0f, 0.77f, 2.0f), 180f, parent);
            Models.Furniture("computerKeyboard", new Vector3(-5.0f, 0.77f, 2.25f), 180f, parent);
            Models.Furniture("chairDesk", new Vector3(-4.3f, 0f, 3.0f), 200f, parent);
            Models.Furniture("bookcaseClosedDoors", new Vector3(-6.2f, 0f, North - 0.35f), 180f, parent);
            Models.Furniture("bookcaseClosedDoors", new Vector3(-5.3f, 0f, North - 0.35f), 180f, parent);
            Models.Furniture("kitchenCabinet", new Vector3(-4.2f, 0f, North - 0.5f), 180f, parent);

            // partitions on both sides of the station, open in the middle
            foreach (float x in new[] { -7.8f, -3.2f })
            {
                foreach (float z in new[] { 5.95f, -5.95f })
                {
                    // two segments back to back: interior finish on both faces
                    Models.Furniture("wall", new Vector3(x - 0.05f, 0f, z), 90f, parent);
                    Models.Furniture("wall", new Vector3(x + 0.05f, 0f, z), 270f, parent);
                }
            }

            Staff("female-d", StaffOutfit.Scrubs, new Vector3(-5.6f, 0f, 2.85f), 180f, parent, "Nurse");
            Label(new Vector3(-5.5f, 3.2f, North), "Triage");
        }

        void RoomHall()
        {
            var parent = Group("RoomHall");
            Slots("RoomQueueSlots", new Vector3(5.6f, 0f, 0f), 9, 13, RoomQueueSlots);
            Label(new Vector3(1.6f, 3.2f, North), "Room queue");

            Models.Furniture("loungeSofa", new Vector3(-0.5f, 0f, North - 0.55f), 180f, parent);
            Models.Furniture("loungeSofa", new Vector3(2.0f, 0f, North - 0.55f), 180f, parent);
            Models.Furniture("pottedPlant", new Vector3(4.2f, 0f, North - 0.7f), 0f, parent, 0.5f);
            Models.Furniture("pottedPlant", new Vector3(-2.4f, 0f, South + 0.7f), 0f, parent, 0.5f);
        }

        GameObject Room(int index)
        {
            int number = index + 1;
            bool far = index < 4;
            float s = far ? 1f : -1f;
            var c = new Vector3(FirstRoomX + (index % 4) * RoomPitch, 0f, s * RoomRowZ);
            roomCenters.Add(c);

            var root = new GameObject("Room_" + number);
            root.transform.SetParent(transform);
            root.transform.position = c;
            var t = root.transform;
            float h = RoomSize / 2f;

            Models.Primitive(PrimitiveType.Cube, "Floor", c + new Vector3(0f, 0.01f, 0f), new Vector3(RoomSize, 0.04f, RoomSize), RoomFloor, t);

            // back wall (window), side walls, front wall (door in the left half)
            Wall("wallWindow", c + new Vector3(-1f, 0f, s * h), new Vector3(0f, 0f, -s), t);
            Wall("wall", c + new Vector3(1f, 0f, s * h), new Vector3(0f, 0f, -s), t);
            foreach (float dz in new[] { -1f, 1f })
            {
                Wall("wall", c + new Vector3(-h, 0f, dz), new Vector3(1f, 0f, 0f), t);
                Wall("wall", c + new Vector3(h, 0f, dz), new Vector3(-1f, 0f, 0f), t);
            }
            Wall("wallDoorway", c + new Vector3(-1f, 0f, -s * h), new Vector3(0f, 0f, s), t);
            Wall("wall", c + new Vector3(1f, 0f, -s * h), new Vector3(0f, 0f, s), t);

            // bed with its head at the back wall, bedside cabinet with a monitor, visitor chair
            Models.Furniture("bedSingle", c + new Vector3(1.1f, 0f, s * 0.75f), far ? 0f : 180f, t);
            Models.Furniture("cabinetBedDrawer", c + new Vector3(0.1f, 0f, s * 1.65f), far ? 180f : 0f, t);
            Models.Furniture("computerScreen", c + new Vector3(0.1f, 0.53f, s * 1.65f), far ? 180f : 0f, t, 0.6f);
            Models.Furniture("chairCushion", c + new Vector3(1.55f, 0f, -s * 1.45f), far ? 0f : 180f, t);
            Models.Furniture("plantSmall1", c + new Vector3(-1.6f, 0f, s * 1.6f), 0f, t, 1.6f);

            // one physician per room, in a white coat (male-d recoloured; female-e already wears one)
            var beside = c + new Vector3(-0.1f, 0f, s * 0.5f);
            if (index % 2 == 0)
                Staff("male-d", StaffOutfit.WhiteCoat, beside, 90f, t, null);
            else
                Staff("female-e", null, beside, 90f, t, null);

            Label(c + new Vector3(0f, 3.2f, s * h), "Room " + number, t);
            return root;
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        /// A wall segment whose light (interior) face points along <paramref name="inward"/>;
        /// the kit's wall pieces have their interior face on local -z.
        /// </summary>
        void Wall(string piece, Vector3 center, Vector3 inward, Transform parent)
        {
            float yaw = Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg;
            var wall = Models.Furniture(piece, center, yaw, parent);
            wall.AddComponent<CutawayWall>().Inward = inward;
        }

        /// <summary>
        /// Queue positions as empty objects. Slot 0 is the head; lines of
        /// <paramref name="columns"/> run back from it (-x) and stack outwards from
        /// the centre line. Queues longer than the hall share the last slot.
        /// </summary>
        void Slots(string name, Vector3 head, int columns, int lines, List<Transform> list)
        {
            var parent = Group(name);
            for (int line = 0; line < lines; line++)
            {
                // z offsets 0, -1, +1, -2, +2 ...
                float z = (line % 2 == 1 ? -1f : 1f) * ((line + 1) / 2) * SlotSpacing;
                for (int col = 0; col < columns; col++)
                {
                    var slot = new GameObject("Slot_" + list.Count).transform;
                    slot.SetParent(parent);
                    slot.position = head + new Vector3(-col * SlotSpacing, 0f, z);
                    list.Add(slot);
                }
            }
        }

        void Staff(string variant, StaffOutfit.Rule[] outfit, Vector3 pos, float yaw, Transform parent, string label)
        {
            var root = new GameObject(label ?? "Physician");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var model = Models.Character(variant, root.transform);
            if (outfit != null) StaffOutfit.Apply(model, variant, outfit);
            var anim = model.GetComponent<Animation>();
            if (anim != null && anim["idle"] != null) anim.Play("idle");
            Models.Primitive(PrimitiveType.Cylinder, "Marker", pos + new Vector3(0f, 0.035f, 0f), new Vector3(0.8f, 0.01f, 0.8f), StaffMarker, root.transform);
            if (label != null) Label(pos + new Vector3(0f, 1.7f, 0f), label, root.transform);
        }

        void Label(Vector3 pos, string text, Transform parent = null)
        {
            var anchor = new GameObject("Label_" + text).transform;
            anchor.SetParent(parent != null ? parent : transform);
            anchor.position = pos;
            Labels.Add(new KeyValuePair<Transform, string>(anchor, text));
        }

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform);
            return t;
        }

        void Box(string name, Vector3 center, Vector3 size, Color color) =>
            Models.Primitive(PrimitiveType.Cube, name, center, size, color, transform);
    }
}
