"""Write one event log per scenario for the Unity replay (task #2).

Run from the repository root:

    python src/export_event_logs.py

Produces data/events_rooms{R}_ia{IA}.csv for every scenario of
docs/assumptions.md, replication seed = 1, run length 1440 min
(docs/event_log_format.md).
"""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))

from er_model import RUN_LENGTH, SCENARIOS, event_log_path, run_simulation, write_event_log  # noqa: E402

DATA_DIR = Path(__file__).resolve().parent.parent / "data"


def main() -> None:
    for name, (n_rooms, mean_ia) in SCENARIOS.items():
        result = run_simulation(n_rooms=n_rooms, mean_interarrival=mean_ia,
                                run_length=RUN_LENGTH, seed=1)
        path = event_log_path(n_rooms, mean_ia, DATA_DIR)
        log = write_event_log(result, path)
        last = log.iloc[-1]
        print(f"{name:<5} rooms={n_rooms} ia={mean_ia:g}: {path.relative_to(DATA_DIR.parent)} "
              f"({len(log)} rows, {len(result.patients)} patients; at end: "
              f"triage_queue={last.triage_queue}, room_queue={last.room_queue})")


if __name__ == "__main__":
    main()
