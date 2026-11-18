"""ER triage simulation model (M1).

Patients arrive at an emergency room, wait for the single triage nurse (FCFS),
are triaged, and then wait for one of ``n_rooms`` treatment rooms (FCFS, one
physician per room). The modelling assumptions are in ``docs/assumptions.md``;
the output contract is in ``docs/model_output.md``; the event log format is in
``docs/event_log_format.md``.

Usage::

    from er_model import run_simulation, write_event_log

    result = run_simulation(n_rooms=6, mean_interarrival=3.0,
                            run_length=1440.0, seed=1)
    result.patients    # one row per arrived patient
    result.state_log   # one row per state change (Q(t), B(t))
    write_event_log(result, "data/events_rooms6_ia3.csv")
"""

from __future__ import annotations

import csv
import math
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import pandas as pd
import simpy

# ---------------------------------------------------------------------------
# Input model (docs/assumptions.md, "System")
# ---------------------------------------------------------------------------

SEVERITIES = ("low", "medium", "high")
SEVERITY_PROBS = (0.5, 0.3, 0.2)

# Triangular triage time in minutes. Passed BY NAME to NumPy
# (left, mode, right) -- see the note in docs/assumptions.md.
TRIAGE_LEFT, TRIAGE_MODE, TRIAGE_RIGHT = 2.0, 4.0, 6.0

# Normal treatment time (mean, standard deviation) in minutes per severity.
TREATMENT_PARAMS = {
    "low": (15.0, 3.0),
    "medium": (25.0, 5.0),
    "high": (45.0, 8.0),
}

# Scenarios of docs/assumptions.md: name -> (n_rooms, mean_interarrival)
SCENARIOS = {
    "Base": (6, 3.0),
    "A": (4, 6.0),
    "B": (8, 6.0),
    "C": (4, 3.0),
    "D": (8, 3.0),
}

RUN_LENGTH = 1440.0  # one day, decision 1

PATIENT_COLUMNS = [
    "patient_id", "severity", "arrival", "triage_start", "triage_end",
    "room_start", "departure", "room_id",
]
STATE_COLUMNS = ["triage_queue", "room_queue", "nurse_busy", "rooms_busy"]

EVENT_LOG_COLUMNS = [
    "time", "patient_id", "event", "severity", "resource",
    "triage_queue", "room_queue", "nurse_busy", "rooms_busy",
]


# ---------------------------------------------------------------------------
# Random number streams
# ---------------------------------------------------------------------------

class RandomStreams:
    """Independent random number streams, all derived from one seed.

    Each source of randomness gets its own stream, and every patient's
    attributes are drawn at arrival. Patient k therefore gets the same
    severity, triage time and treatment time in every scenario run with the
    same seed (common random numbers, decision 10), whatever the number of
    rooms.
    """

    def __init__(self, seed: int):
        children = np.random.SeedSequence(seed).spawn(4)
        self._arrival, self._severity, self._triage, self._treatment = (
            np.random.default_rng(c) for c in children
        )

    def interarrival(self, mean: float) -> float:
        return float(self._arrival.exponential(scale=mean))

    def severity(self) -> str:
        return SEVERITIES[int(self._severity.choice(len(SEVERITIES), p=SEVERITY_PROBS))]

    def triage_time(self) -> float:
        return float(self._triage.triangular(left=TRIAGE_LEFT, mode=TRIAGE_MODE,
                                             right=TRIAGE_RIGHT))

    def treatment_time(self, severity: str) -> float:
        mean, sd = TREATMENT_PARAMS[severity]
        while True:  # decision 3: redraw samples <= 0
            x = float(self._treatment.normal(loc=mean, scale=sd))
            if x > 0.0:
                return x


# ---------------------------------------------------------------------------
# Result object (docs/model_output.md)
# ---------------------------------------------------------------------------

@dataclass
class SimulationResult:
    params: dict
    patients: pd.DataFrame
    state_log: pd.DataFrame


# ---------------------------------------------------------------------------
# Model
# ---------------------------------------------------------------------------

class _EmergencyRoom:
    """System state, resources and processes of one replication."""

    def __init__(self, env: simpy.Environment, n_rooms: int, streams: RandomStreams):
        self.env = env
        self.streams = streams
        self.nurse = simpy.Resource(env, capacity=1)  # FIFO request queue = FCFS
        # Free rooms are kept as numbers 1..R. Getters are served FIFO (FCFS);
        # a patient gets the lowest-numbered free room.
        self.free_rooms = simpy.PriorityStore(env, capacity=n_rooms)
        for k in range(1, n_rooms + 1):
            self.free_rooms.put(k)

        # State variables
        self.triage_queue = 0
        self.room_queue = 0
        self.nurse_busy = 0
        self.rooms_busy = 0

        self.patients: list[dict] = []
        self.state_rows: list[tuple] = [(0.0, 0, 0, 0, 0)]

    def _log_state(self) -> None:
        self.state_rows.append((self.env.now, self.triage_queue, self.room_queue,
                                self.nurse_busy, self.rooms_busy))

    def arrivals(self, mean_interarrival: float):
        """Arrival process: exponential interarrival times."""
        patient_id = 0
        while True:
            yield self.env.timeout(self.streams.interarrival(mean_interarrival))
            patient_id += 1
            severity = self.streams.severity()
            triage_time = self.streams.triage_time()
            treatment_time = self.streams.treatment_time(severity)
            self.env.process(self.patient(patient_id, severity, triage_time, treatment_time))

    def patient(self, patient_id: int, severity: str, triage_time: float,
                treatment_time: float):
        env = self.env
        rec = {
            "patient_id": patient_id, "severity": severity, "arrival": env.now,
            "triage_start": math.nan, "triage_end": math.nan,
            "room_start": math.nan, "departure": math.nan, "room_id": "",
        }
        self.patients.append(rec)

        # Arrival: join the triage queue
        self.triage_queue += 1
        self._log_state()

        with self.nurse.request() as request:
            yield request
            # Triage start
            self.triage_queue -= 1
            self.nurse_busy = 1
            rec["triage_start"] = env.now
            self._log_state()

            yield env.timeout(triage_time)
            # Triage end: nurse is released, patient joins the room queue
            self.nurse_busy = 0
            self.room_queue += 1
            rec["triage_end"] = env.now
            self._log_state()

        room = yield self.free_rooms.get()
        # Room start
        self.room_queue -= 1
        self.rooms_busy += 1
        rec["room_start"] = env.now
        rec["room_id"] = f"room_{room}"
        self._log_state()

        yield env.timeout(treatment_time)
        # Room end: patient leaves, room becomes free
        self.rooms_busy -= 1
        rec["departure"] = env.now
        self._log_state()
        self.free_rooms.put(room)


def run_simulation(n_rooms: int = 6, mean_interarrival: float = 3.0,
                   run_length: float = RUN_LENGTH, seed: int = 1) -> SimulationResult:
    """Run one replication; see docs/model_output.md for the returned object."""
    if int(n_rooms) != n_rooms or n_rooms < 1:
        raise ValueError(f"n_rooms must be a positive integer, got {n_rooms!r}")
    if not mean_interarrival > 0:
        raise ValueError(f"mean_interarrival must be > 0, got {mean_interarrival!r}")
    if not run_length > 0:
        raise ValueError(f"run_length must be > 0, got {run_length!r}")
    n_rooms = int(n_rooms)
    mean_interarrival = float(mean_interarrival)
    run_length = float(run_length)

    env = simpy.Environment()
    er = _EmergencyRoom(env, n_rooms, RandomStreams(seed))
    env.process(er.arrivals(mean_interarrival))
    env.run(until=run_length)

    # Final row at run_length with the final state
    er.state_rows.append((run_length, er.triage_queue, er.room_queue,
                          er.nurse_busy, er.rooms_busy))

    patients = pd.DataFrame(er.patients, columns=PATIENT_COLUMNS)
    patients = patients.astype({
        "patient_id": "int64", "arrival": "float64", "triage_start": "float64",
        "triage_end": "float64", "room_start": "float64", "departure": "float64",
    })
    state_log = pd.DataFrame(er.state_rows, columns=["time"] + STATE_COLUMNS)
    state_log = state_log.astype({"time": "float64", **{c: "int64" for c in STATE_COLUMNS}})

    params = {"n_rooms": n_rooms, "mean_interarrival": mean_interarrival,
              "run_length": run_length, "seed": seed}
    return SimulationResult(params=params, patients=patients, state_log=state_log)


# ---------------------------------------------------------------------------
# Event log (docs/event_log_format.md)
# ---------------------------------------------------------------------------

# Every event changes the state (triage_queue, room_queue, nurse_busy,
# rooms_busy) by a different vector, so the event behind each row of the
# state log can be recognised from the change alone.
_EVENT_BY_DELTA = {
    (+1, 0, 0, 0): "arrive",
    (-1, 0, +1, 0): "triage_start",
    (0, +1, -1, 0): "triage_end",
    (0, -1, 0, +1): "room_start",
    (0, 0, 0, -1): "room_end",
}
_TIME_COLUMN = {
    "arrive": "arrival",
    "triage_start": "triage_start",
    "triage_end": "triage_end",
    "room_start": "room_start",
    "room_end": "departure",
}


def build_event_log(result: SimulationResult) -> pd.DataFrame:
    """Event log rows from ``result.patients`` and ``result.state_log``.

    Rows 1..N-2 of the state log are the events in the order they happened
    (row 0 is the initial state, the last row the state at run_length). For
    each row the event type is recognised from the state change, and the
    patient is the next one, in order of that event's time, who has that
    event. The patient's own recorded time must equal the row's time; any
    mismatch raises an error, so the log is always consistent with both tables.
    """
    patients = result.patients
    state = result.state_log
    run_length = float(result.params["run_length"])

    by_id = patients.set_index("patient_id")
    pending = {}
    for event, col in _TIME_COLUMN.items():
        done = patients[patients[col].notna()]
        pending[event] = iter(done.sort_values([col, "patient_id"], kind="stable")
                              ["patient_id"].tolist())

    values = state[STATE_COLUMNS].to_numpy()
    times = state["time"].to_numpy()
    rows = []
    for i in range(1, len(state) - 1):
        delta = tuple(int(x) for x in values[i] - values[i - 1])
        event = _EVENT_BY_DELTA.get(delta)
        if event is None:
            raise ValueError(f"state_log row {i}: change {delta} is not a single event")
        try:
            pid = next(pending[event])
        except StopIteration:
            raise ValueError(f"state_log row {i}: no patient left for event {event!r}") from None
        p = by_id.loc[pid]
        if p[_TIME_COLUMN[event]] != times[i]:
            raise ValueError(f"state_log row {i}: patient {pid} {event} time "
                             f"{p[_TIME_COLUMN[event]]} != {times[i]}")
        if event == "arrive":
            resource = ""
        elif event.startswith("triage"):
            resource = "nurse"
        else:
            resource = p["room_id"]
        rows.append([times[i], pid, event, p["severity"], resource, *values[i]])

    for event, it in pending.items():
        if next(it, None) is not None:
            raise ValueError(f"patients has {event!r} times without a state_log row")

    rows.append([run_length, None, "end", "", "", *values[-1]])
    log = pd.DataFrame(rows, columns=EVENT_LOG_COLUMNS)
    log["patient_id"] = log["patient_id"].astype("Int64")
    for c in STATE_COLUMNS:
        log[c] = log[c].astype("int64")
    return log


def event_log_path(n_rooms: int, mean_interarrival: float, data_dir="data") -> Path:
    """data/events_rooms{R}_ia{IA}.csv, e.g. data/events_rooms6_ia3.csv"""
    return Path(data_dir) / f"events_rooms{int(n_rooms)}_ia{float(mean_interarrival):g}.csv"


def write_event_log(result: SimulationResult, path) -> pd.DataFrame:
    """Write the event log CSV (docs/event_log_format.md); returns the rows."""
    log = build_event_log(result)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(EVENT_LOG_COLUMNS)
        for r in log.itertuples(index=False):
            w.writerow([
                f"{r.time:.2f}",
                "" if pd.isna(r.patient_id) else int(r.patient_id),
                r.event, r.severity, r.resource,
                int(r.triage_queue), int(r.room_queue),
                int(r.nurse_busy), int(r.rooms_busy),
            ])
    return log
