"""Performance measures for one ER simulation replication.

The calculations consume the ``result`` object defined in
``docs/model_output.md``. Time-average measures use the state log as a
piecewise-constant time series, so irregularly spaced events are weighted by
their duration.
"""

from dataclasses import asdict, dataclass
from math import isfinite
from typing import Any

import pandas as pd


@dataclass(frozen=True, slots=True)
class PerformanceMetrics:
    """Performance measures for a single run.

    Utilisation and share fields are proportions in the range [0, 1]. Average
    delays and time in system are in minutes. A mean or share is ``None`` when
    its eligible patient group is empty.
    """

    average_triage_delay: float | None
    average_room_delay: float | None
    average_time_in_system: float | None
    average_triage_queue_length: float
    average_room_queue_length: float
    average_number_in_system: float
    nurse_utilisation: float
    rooms_utilisation: float
    share_waiting_more_than_30_minutes: float | None
    share_triage_wait_more_than_30_minutes: float | None
    share_room_wait_more_than_30_minutes: float | None
    n_arrived: int
    n_triage_started: int
    n_room_started: int
    n_departed: int

    def as_dict(self) -> dict[str, Any]:
        """Return the measures as a plain dictionary for reporting/export."""
        return asdict(self)


def _require_columns(frame: pd.DataFrame, columns: set[str], name: str) -> None:
    missing = columns.difference(frame.columns)
    if missing:
        raise ValueError(f"{name} is missing required columns: {sorted(missing)}")


def _numeric_column(frame: pd.DataFrame, column: str, name: str) -> pd.Series:
    try:
        values = pd.to_numeric(frame[column], errors="raise").astype(float)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{name}.{column} must contain numeric values") from exc
    if not values.dropna().map(isfinite).all():
        raise ValueError(f"{name}.{column} must contain finite values or NaN")
    return values


def _share_over_30(durations: pd.Series) -> float | None:
    if durations.empty:
        return None
    return float((durations > 30.0).mean())


def _time_average(
    times: pd.Series,
    values: pd.Series,
    *,
    run_length: float,
    label: str,
) -> float:
    """Integrate a step function from the state log over the full run."""
    if times.empty or len(times) != len(values):
        raise ValueError(f"state_log.{label} must have one value per state row")
    if times.iloc[0] != 0.0 or times.iloc[-1] != run_length:
        raise ValueError(
            "state_log must begin at time 0 and end at the configured run_length"
        )

    durations = times.iloc[1:].reset_index(drop=True) - times.iloc[:-1].reset_index(
        drop=True
    )
    if (durations < 0).any():
        raise ValueError("state_log.time must be in non-decreasing order")

    areas = values.iloc[:-1].reset_index(drop=True) * durations
    return float(areas.sum() / run_length)


def compute_performance_metrics(result: Any) -> PerformanceMetrics:
    """Compute the documented performance measures from one simulation run.

    Queue delays include patients who started service in that queue, matching
    ``docs/assumptions.md``. Time in system includes departed patients only.
    The main 30-minute measure is the combined triage plus room wait among
    patients who entered a room; the two component shares use that same cohort.
    """
    try:
        params = result.params
        patients = result.patients
        state_log = result.state_log
    except AttributeError as exc:
        raise ValueError(
            "result must provide params, patients, and state_log attributes"
        ) from exc

    if not isinstance(params, dict):
        raise ValueError("result.params must be a dictionary")
    for parameter in ("run_length", "n_rooms"):
        if parameter not in params:
            raise ValueError(f"result.params is missing {parameter!r}")
    try:
        run_length = float(params["run_length"])
        room_count = float(params["n_rooms"])
    except (TypeError, ValueError) as exc:
        raise ValueError("run_length and n_rooms must be numeric") from exc
    if not isfinite(run_length) or run_length <= 0:
        raise ValueError("run_length must be finite and greater than zero")
    if not isfinite(room_count) or not room_count.is_integer() or room_count <= 0:
        raise ValueError("n_rooms must be a positive integer")
    n_rooms = int(room_count)

    if not isinstance(patients, pd.DataFrame):
        raise ValueError("result.patients must be a pandas DataFrame")
    if not isinstance(state_log, pd.DataFrame):
        raise ValueError("result.state_log must be a pandas DataFrame")

    _require_columns(
        patients,
        {"arrival", "triage_start", "triage_end", "room_start", "departure"},
        "patients",
    )
    _require_columns(
        state_log,
        {"time", "triage_queue", "room_queue", "nurse_busy", "rooms_busy"},
        "state_log",
    )

    patient_times = {
        column: _numeric_column(patients, column, "patients")
        for column in ("arrival", "triage_start", "triage_end", "room_start", "departure")
    }
    if patient_times["arrival"].isna().any():
        raise ValueError("every row in patients must have an arrival time")
    triage_waits = patient_times["triage_start"] - patient_times["arrival"]
    room_waits = patient_times["room_start"] - patient_times["triage_end"]
    time_in_systems = patient_times["departure"] - patient_times["arrival"]

    triage_started = patient_times["triage_start"].notna()
    triage_ended = patient_times["triage_end"].notna()
    room_started = patient_times["room_start"].notna()
    departed = patient_times["departure"].notna()
    if (triage_ended & ~triage_started).any():
        raise ValueError("patients.triage_end requires patients.triage_start")
    if (room_started & patient_times["triage_end"].isna()).any():
        raise ValueError("patients.room_start requires patients.triage_end")
    if (departed & ~room_started).any():
        raise ValueError("patients.departure requires patients.room_start")

    triage_wait_values = triage_waits.loc[triage_started]
    room_wait_values = room_waits.loc[room_started]
    system_time_values = time_in_systems.loc[departed]
    if (triage_wait_values < 0).any():
        raise ValueError("triage wait contains a negative duration")
    triage_service_durations = (
        patient_times["triage_end"].loc[triage_ended]
        - patient_times["triage_start"].loc[triage_ended]
    )
    if (triage_service_durations < 0).any():
        raise ValueError("triage service contains a negative duration")
    if (room_wait_values < 0).any():
        raise ValueError("room wait contains a negative duration")
    if (system_time_values < 0).any():
        raise ValueError("time in system contains a negative duration")
    treatment_durations = (
        patient_times["departure"].loc[departed]
        - patient_times["room_start"].loc[departed]
    )
    if (treatment_durations < 0).any():
        raise ValueError("treatment contains a negative duration")

    log_times = _numeric_column(state_log, "time", "state_log")
    if log_times.isna().any():
        raise ValueError("state_log.time cannot contain NaN")
    state_values: dict[str, pd.Series] = {}
    for column in ("triage_queue", "room_queue", "nurse_busy", "rooms_busy"):
        values = _numeric_column(state_log, column, "state_log")
        if values.isna().any() or (values < 0).any():
            raise ValueError(f"state_log.{column} must contain non-negative values")
        state_values[column] = values

    if (state_values["nurse_busy"] > 1).any():
        raise ValueError("state_log.nurse_busy must be 0 or 1")
    if (state_values["rooms_busy"] > n_rooms).any():
        raise ValueError("state_log.rooms_busy cannot exceed params.n_rooms")

    average_triage_queue = _time_average(
        log_times,
        state_values["triage_queue"],
        run_length=run_length,
        label="triage_queue",
    )
    average_room_queue = _time_average(
        log_times,
        state_values["room_queue"],
        run_length=run_length,
        label="room_queue",
    )
    number_in_system = (
        state_values["triage_queue"]
        + state_values["room_queue"]
        + state_values["nurse_busy"]
        + state_values["rooms_busy"]
    )
    average_system_size = _time_average(
        log_times,
        number_in_system,
        run_length=run_length,
        label="number_in_system",
    )
    nurse_utilisation = _time_average(
        log_times,
        state_values["nurse_busy"],
        run_length=run_length,
        label="nurse_busy",
    )
    rooms_utilisation = _time_average(
        log_times,
        state_values["rooms_busy"],
        run_length=run_length,
        label="rooms_busy",
    ) / n_rooms

    # The assumptions define the main cohort as patients who entered a room.
    completed_room_cohort = room_started
    combined_waits = triage_waits.loc[completed_room_cohort] + room_waits.loc[
        completed_room_cohort
    ]

    return PerformanceMetrics(
        average_triage_delay=float(triage_wait_values.mean())
        if not triage_wait_values.empty
        else None,
        average_room_delay=float(room_wait_values.mean())
        if not room_wait_values.empty
        else None,
        average_time_in_system=float(system_time_values.mean())
        if not system_time_values.empty
        else None,
        average_triage_queue_length=average_triage_queue,
        average_room_queue_length=average_room_queue,
        average_number_in_system=average_system_size,
        nurse_utilisation=nurse_utilisation,
        rooms_utilisation=rooms_utilisation,
        share_waiting_more_than_30_minutes=_share_over_30(combined_waits),
        share_triage_wait_more_than_30_minutes=_share_over_30(
            triage_waits.loc[completed_room_cohort]
        ),
        share_room_wait_more_than_30_minutes=_share_over_30(
            room_waits.loc[completed_room_cohort]
        ),
        n_arrived=len(patients),
        n_triage_started=int(triage_started.sum()),
        n_room_started=int(room_started.sum()),
        n_departed=int(departed.sum()),
    )
