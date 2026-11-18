using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ERSim
{
    public enum EventKind { Arrive, TriageStart, TriageEnd, RoomStart, RoomEnd, End }

    public enum Severity { None, Low, Medium, High }

    /// <summary>One row of the event log (docs/event_log_format.md).</summary>
    public struct LogEvent
    {
        public float Time;
        public int PatientId;      // 0 on the end row
        public EventKind Kind;
        public Severity Severity;
        public int Room;           // 1..R for room events, 0 otherwise
        public int TriageQueue;    // state columns, taken as-is from the file
        public int RoomQueue;
        public int NurseBusy;
        public int RoomsBusy;
    }

    /// <summary>Reads an event log CSV. Parsing only, no calculations.</summary>
    public static class EventLog
    {
        static readonly string[] Header =
            { "time", "patient_id", "event", "severity", "resource",
              "triage_queue", "room_queue", "nurse_busy", "rooms_busy" };

        public static List<LogEvent> Load(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) throw new FormatException("empty file");

            var head = lines[0].Trim().TrimStart((char)0xFEFF).Split(',');
            if (head.Length != Header.Length)
                throw new FormatException("unexpected header: " + lines[0]);
            for (int i = 0; i < Header.Length; i++)
                if (head[i].Trim() != Header[i])
                    throw new FormatException("unexpected header: " + lines[0]);

            var events = new List<LogEvent>(lines.Length);
            for (int n = 1; n < lines.Length; n++)
            {
                var line = lines[n].Trim();
                if (line.Length == 0) continue;
                var c = line.Split(',');
                if (c.Length != Header.Length)
                    throw new FormatException($"line {n + 1}: expected {Header.Length} columns");
                try
                {
                    events.Add(new LogEvent
                    {
                        Time = float.Parse(c[0], NumberStyles.Float, CultureInfo.InvariantCulture),
                        PatientId = c[1].Length == 0 ? 0 : int.Parse(c[1], CultureInfo.InvariantCulture),
                        Kind = ParseKind(c[2]),
                        Severity = ParseSeverity(c[3]),
                        Room = ParseRoom(c[4]),
                        TriageQueue = int.Parse(c[5], CultureInfo.InvariantCulture),
                        RoomQueue = int.Parse(c[6], CultureInfo.InvariantCulture),
                        NurseBusy = int.Parse(c[7], CultureInfo.InvariantCulture),
                        RoomsBusy = int.Parse(c[8], CultureInfo.InvariantCulture),
                    });
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    throw new FormatException($"line {n + 1}: {e.Message}");
                }
            }
            return events;
        }

        static EventKind ParseKind(string s)
        {
            switch (s)
            {
                case "arrive": return EventKind.Arrive;
                case "triage_start": return EventKind.TriageStart;
                case "triage_end": return EventKind.TriageEnd;
                case "room_start": return EventKind.RoomStart;
                case "room_end": return EventKind.RoomEnd;
                case "end": return EventKind.End;
                default: throw new FormatException("unknown event '" + s + "'");
            }
        }

        static Severity ParseSeverity(string s)
        {
            switch (s)
            {
                case "low": return Severity.Low;
                case "medium": return Severity.Medium;
                case "high": return Severity.High;
                case "": return Severity.None;
                default: throw new FormatException("unknown severity '" + s + "'");
            }
        }

        static int ParseRoom(string s)
        {
            if (s.StartsWith("room_"))
                return int.Parse(s.Substring(5), CultureInfo.InvariantCulture);
            if (s == "nurse" || s == "") return 0;
            throw new FormatException("unknown resource '" + s + "'");
        }
    }
}
