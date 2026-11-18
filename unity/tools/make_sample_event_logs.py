"""Write SAMPLE event logs for the Unity replay.

These files are placeholders so the 3D replay can be built and tested before
the real model (issue #1) and its `write_event_log` (issue #2) exist. They
follow docs/event_log_format.md exactly and use the distributions from
docs/assumptions.md, but they are NOT results of the project's model.
Overwrite them with the real event logs once `write_event_log` is available.

Usage (from the repository root):

    python unity/tools/make_sample_event_logs.py            # write missing files
    python unity/tools/make_sample_event_logs.py --force    # overwrite all
"""

import argparse
import heapq
import random
from pathlib import Path

RUN_LENGTH = 1440.0
SEED = 1
SCENARIOS = [(6, 3), (4, 6), (8, 6), (4, 3), (8, 3)]  # (rooms, mean interarrival)
SEVERITY_P = [("low", 0.5), ("medium", 0.3), ("high", 0.2)]
TREATMENT = {"low": (15, 3), "medium": (25, 5), "high": (45, 8)}
HEADER = "time,patient_id,event,severity,resource,triage_queue,room_queue,nurse_busy,rooms_busy"


def simulate(n_rooms, mean_ia, rng):
    """Single-nurse triage + n_rooms rooms, FCFS everywhere. Returns event rows."""
    rows = []
    triage_q = []  # patient ids waiting for the nurse
    room_q = []  # patient ids waiting for a room
    nurse = None  # patient id in triage
    rooms = [None] * n_rooms  # patient id per room
    severity = {}
    room_of = {}
    fel = []  # future event list: (time, seq, kind, patient_id)
    seq = 0

    def schedule(t, kind, pid):
        nonlocal seq
        if t <= RUN_LENGTH:  # events after the run length are not written
            heapq.heappush(fel, (t, seq, kind, pid))
        seq += 1

    def log(t, pid, event, resource=""):
        rows.append((t, pid, event, severity.get(pid, ""), resource,
                     len(triage_q), len(room_q), int(nurse is not None),
                     sum(r is not None for r in rooms)))

    def pick_severity():
        u, acc = rng.random(), 0.0
        for name, p in SEVERITY_P:
            acc += p
            if u < acc:
                return name
        return SEVERITY_P[-1][0]

    def treatment_time(sev):
        mu, sd = TREATMENT[sev]
        x = rng.gauss(mu, sd)
        while x <= 0:  # assumption 3: redraw non-positive samples
            x = rng.gauss(mu, sd)
        return x

    def start_triage(t):
        nonlocal nurse
        if nurse is None and triage_q:
            nurse = triage_q.pop(0)
            log(t, nurse, "triage_start", "nurse")
            schedule(t + rng.triangular(low=2, high=6, mode=4), "triage_end", nurse)

    def start_room(t):
        while room_q and None in rooms:
            i = rooms.index(None)
            pid = room_q.pop(0)
            rooms[i] = pid
            room_of[pid] = f"room_{i + 1}"
            log(t, pid, "room_start", room_of[pid])
            schedule(t + treatment_time(severity[pid]), "room_end", pid)

    next_id = 1
    schedule(rng.expovariate(1.0 / mean_ia), "arrive", next_id)

    while fel:
        t, _, kind, pid = heapq.heappop(fel)
        if kind == "arrive":
            severity[pid] = pick_severity()
            triage_q.append(pid)
            log(t, pid, "arrive")
            next_id += 1
            schedule(t + rng.expovariate(1.0 / mean_ia), "arrive", next_id)
            start_triage(t)
        elif kind == "triage_end":
            nurse = None
            room_q.append(pid)
            log(t, pid, "triage_end", "nurse")
            start_room(t)
            start_triage(t)
        elif kind == "room_end":
            rooms[int(room_of[pid].split("_")[1]) - 1] = None
            log(t, pid, "room_end", room_of[pid])
            start_room(t)

    rows.append((RUN_LENGTH, "", "end", "", "", len(triage_q), len(room_q),
                 int(nurse is not None), sum(r is not None for r in rooms)))
    return rows


def write(path, rows):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(HEADER + "\n")
        for t, pid, event, sev, res, tq, rq, nb, rb in rows:
            f.write(f"{t:.2f},{pid},{event},{sev},{res},{tq},{rq},{nb},{rb}\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--force", action="store_true", help="overwrite existing files")
    parser.add_argument("--out", default="data", help="output directory (default: data)")
    args = parser.parse_args()

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for n_rooms, mean_ia in SCENARIOS:
        path = out / f"events_rooms{n_rooms}_ia{mean_ia}.csv"
        if path.exists() and not args.force:
            print(f"skip   {path} (exists)")
            continue
        rows = simulate(n_rooms, float(mean_ia), random.Random(SEED))
        write(path, rows)
        last = rows[-1]
        print(f"wrote  {path}: {len(rows)} rows, end state tq={last[5]} rq={last[6]} "
              f"nurse={last[7]} rooms={last[8]}")


if __name__ == "__main__":
    main()
