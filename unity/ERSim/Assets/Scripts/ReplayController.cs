using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ERSim
{
    /// <summary>
    /// Plays an event log. Positions follow the events; the counters are the state
    /// columns of the last applied row, copied without any calculation.
    /// </summary>
    public class ReplayController : MonoBehaviour
    {
        public static readonly Color LowColor = new Color(0.20f, 0.75f, 0.30f);
        public static readonly Color MediumColor = new Color(0.95f, 0.80f, 0.15f);
        public static readonly Color HighColor = new Color(0.90f, 0.20f, 0.20f);

        public ErLayout Layout;

        // Playback state
        public float SimTime { get; private set; }
        public float RunLength { get; private set; }
        public bool Playing { get; set; }
        public float Speed { get; set; } = 10f;   // simulated minutes per real second
        public bool Finished { get; private set; }

        // Last applied row (state columns straight from the CSV)
        public LogEvent State { get; private set; }

        public string LoadedFile { get; private set; }
        public int Rooms { get; private set; }
        public string Error { get; private set; }

        List<LogEvent> events = new List<LogEvent>();
        int next;   // index of the next row to apply
        readonly Dictionary<int, Patient> patients = new Dictionary<int, Patient>();
        readonly List<Patient> triageQueue = new List<Patient>();
        readonly List<Patient> roomQueue = new List<Patient>();
        readonly List<Patient> leaving = new List<Patient>();

        /// <summary>Loads events_rooms{R}_ia{IA}.csv from StreamingAssets and rewinds.</summary>
        public bool Load(int rooms, int interarrival)
        {
            Clear();
            Rooms = rooms;
            Layout.SetVisibleRooms(rooms);
            LoadedFile = $"events_rooms{rooms}_ia{interarrival}.csv";
            var path = Path.Combine(Application.streamingAssetsPath, LoadedFile);
            try
            {
                events = EventLog.Load(path);
            }
            catch (Exception e)
            {
                events = new List<LogEvent>();
                Error = $"Could not read {LoadedFile}: {e.Message}";
                Debug.LogError(Error);
                return false;
            }
            Error = null;
            RunLength = events.Count > 0 ? events[events.Count - 1].Time : 0f;
            Debug.Log($"Loaded {events.Count} events from {path}");
            return true;
        }

        public void Restart()
        {
            Clear();
        }

        void Clear()
        {
            foreach (var p in patients.Values) Destroy(p.gameObject);
            foreach (var p in leaving) Destroy(p.gameObject);
            patients.Clear();
            triageQueue.Clear();
            roomQueue.Clear();
            leaving.Clear();
            next = 0;
            SimTime = 0f;
            State = default;   // all zeros, as at time 0
            Finished = false;
        }

        void Update()
        {
            if (Playing && !Finished && events.Count > 0)
                Advance(SimTime + Time.deltaTime * Speed);

            // Patients that walked out of the exit are removed
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                if (leaving[i].Arrived)
                {
                    Destroy(leaving[i].gameObject);
                    leaving.RemoveAt(i);
                }
            }

            // Walking speeds up with the playback speed so patients keep up with the log
            Patient.MoveRate = Mathf.Max(3.5f, Speed * 0.5f);
        }

        /// <summary>Applies every row up to <paramref name="time"/>.</summary>
        void Advance(float time)
        {
            SimTime = time;
            bool changed = false;
            while (next < events.Count && events[next].Time <= SimTime)
            {
                Apply(events[next++]);
                changed = true;
            }
            if (changed) UpdateQueueTargets();
            if (next >= events.Count)
            {
                SimTime = RunLength;
                Finished = true;
                Playing = false;
            }
        }

        /// <summary>Jumps to a time: replays the rows from the start and places everyone directly.</summary>
        public void Seek(float time)
        {
            bool wasPlaying = Playing;
            Clear();
            Advance(Mathf.Clamp(time, 0f, RunLength));
            foreach (var p in patients.Values) p.Snap();
            foreach (var p in leaving) Destroy(p.gameObject);
            leaving.Clear();
            Playing = wasPlaying && !Finished;
        }

        void Apply(LogEvent e)
        {
            State = e;
            Patient p;
            switch (e.Kind)
            {
                case EventKind.Arrive:
                    p = Spawn(e);
                    triageQueue.Add(p);
                    break;
                case EventKind.TriageStart:
                    p = Get(e);
                    triageQueue.Remove(p);
                    p.GoTo(Layout.TriageSpot, ErLayout.TriageFacing);
                    break;
                case EventKind.TriageEnd:
                    p = Get(e);
                    roomQueue.Add(p);
                    break;
                case EventKind.RoomStart:
                    p = Get(e);
                    roomQueue.Remove(p);
                    if (e.Room >= 1 && e.Room <= Layout.RoomCount)
                        p.Follow(Layout.PathToRoom(e.Room), ErLayout.SeatFacing(e.Room), true);
                    else
                        Debug.LogWarning($"t={e.Time}: room_{e.Room} is outside 1..{Layout.RoomCount}");
                    break;
                case EventKind.RoomEnd:
                    p = Get(e);
                    patients.Remove(e.PatientId);
                    if (e.Room >= 1 && e.Room <= Layout.RoomCount)
                        p.Follow(Layout.PathFromRoom(e.Room), ErLayout.QueueFacing, false);
                    leaving.Add(p);
                    break;
                case EventKind.End:
                    break;
            }
        }

        Patient Spawn(LogEvent e)
        {
            var p = Patient.Create(e.PatientId, ColorOf(e.Severity), Layout.SpawnPoint, transform);
            patients[e.PatientId] = p;
            return p;
        }

        Patient Get(LogEvent e)
        {
            if (patients.TryGetValue(e.PatientId, out var p)) return p;
            Debug.LogWarning($"t={e.Time}: patient {e.PatientId} has no arrive row");
            return Spawn(e);
        }

        void UpdateQueueTargets()
        {
            for (int i = 0; i < triageQueue.Count; i++)
                triageQueue[i].GoTo(Slot(Layout.TriageQueueSlots, i), ErLayout.QueueFacing);
            for (int i = 0; i < roomQueue.Count; i++)
                roomQueue[i].GoTo(Slot(Layout.RoomQueueSlots, i), ErLayout.QueueFacing);
        }

        static Vector3 Slot(List<Transform> slots, int i) => slots[Mathf.Min(i, slots.Count - 1)].position;

        public static Color ColorOf(Severity s)
        {
            switch (s)
            {
                case Severity.Low: return LowColor;
                case Severity.Medium: return MediumColor;
                case Severity.High: return HighColor;
                default: return Color.gray;
            }
        }
    }
}
