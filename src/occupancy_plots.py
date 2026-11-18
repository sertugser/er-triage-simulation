"""Plot nurse/room occupancy and queue lengths from one simulation run."""

from __future__ import annotations

from math import isclose, isfinite
from pathlib import Path
from typing import Any

from matplotlib.backends.backend_agg import FigureCanvasAgg
from matplotlib.figure import Figure
from matplotlib.ticker import MaxNLocator
import pandas as pd


_STATE_COLUMNS = ("triage_queue", "room_queue", "nurse_busy", "rooms_busy")
_COLORS = {
    "nurse": "#167D78",
    "rooms": "#4263EB",
    "triage_queue": "#D17A00",
    "room_queue": "#C44569",
}


def _number(value: Any, name: str) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{name} must be numeric") from exc
    if not isfinite(number):
        raise ValueError(f"{name} must be finite")
    return number


def _numeric_column(frame: pd.DataFrame, column: str) -> pd.Series:
    try:
        values = pd.to_numeric(frame[column], errors="raise").astype(float)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"state_log.{column} must contain numeric values") from exc
    if values.isna().any() or not values.map(isfinite).all():
        raise ValueError(f"state_log.{column} must contain finite values")
    return values


def _validate_state_log(
    state_log: pd.DataFrame,
    *,
    run_length: float,
    n_rooms: int,
) -> dict[str, pd.Series]:
    missing = set(("time", *_STATE_COLUMNS)).difference(state_log.columns)
    if missing:
        raise ValueError(f"state_log is missing required columns: {sorted(missing)}")
    if state_log.empty:
        raise ValueError("state_log cannot be empty")

    values = {column: _numeric_column(state_log, column) for column in ("time", *_STATE_COLUMNS)}
    times = values["time"]
    if not isclose(times.iloc[0], 0.0) or not isclose(times.iloc[-1], run_length):
        raise ValueError("state_log must start at 0 and end at run_length")
    if (times.diff().dropna() < 0).any():
        raise ValueError("state_log.time must be in non-decreasing order")

    for column in _STATE_COLUMNS:
        column_values = values[column]
        if (column_values < 0).any():
            raise ValueError(f"state_log.{column} cannot contain negative values")
        if (column_values % 1 != 0).any():
            raise ValueError(f"state_log.{column} must contain integer counts")

    if not values["nurse_busy"].isin((0.0, 1.0)).all():
        raise ValueError("state_log.nurse_busy must contain only 0 or 1")
    if (values["rooms_busy"] > n_rooms).any():
        raise ValueError("state_log.rooms_busy cannot exceed params.n_rooms")

    first_state = [values[column].iloc[0] for column in _STATE_COLUMNS]
    if any(first_state):
        raise ValueError("the initial state at time 0 must be empty and idle")
    return values


def _output_path(
    output_dir: str | Path,
    *,
    n_rooms: int,
    mean_interarrival: float,
    seed: int,
) -> Path:
    directory = Path(output_dir)
    if not directory.is_absolute():
        # Resolve relative paths from the repository root, not the notebook's cwd.
        directory = Path(__file__).resolve().parents[1] / directory
    ia_label = f"{mean_interarrival:g}".replace(".", "p")
    return directory / f"occupancy_rooms{n_rooms}_ia{ia_label}_seed{seed}.png"


def plot_occupancy_over_time(
    result: Any,
    output_dir: str | Path = "figures",
) -> Path:
    """Save B(t) and Q(t) as step functions and return the PNG path.

    ``result`` follows ``docs/model_output.md``. Duplicate timestamps are
    retained: their zero-length transitions show the event ordering, while the
    last state at a timestamp is the one held over the next interval.
    """
    try:
        params = result.params
        state_log = result.state_log
    except AttributeError as exc:
        raise ValueError("result must provide params and state_log attributes") from exc
    if not isinstance(params, dict):
        raise ValueError("result.params must be a dictionary")
    required_params = {"n_rooms", "mean_interarrival", "run_length", "seed"}
    missing_params = required_params.difference(params)
    if missing_params:
        raise ValueError(f"result.params is missing: {sorted(missing_params)}")
    if not isinstance(state_log, pd.DataFrame):
        raise ValueError("result.state_log must be a pandas DataFrame")

    run_length = _number(params["run_length"], "params.run_length")
    mean_interarrival = _number(params["mean_interarrival"], "params.mean_interarrival")
    room_count = _number(params["n_rooms"], "params.n_rooms")
    seed_value = _number(params["seed"], "params.seed")
    if run_length <= 0 or mean_interarrival <= 0:
        raise ValueError("run_length and mean_interarrival must be greater than zero")
    if room_count <= 0 or not room_count.is_integer():
        raise ValueError("n_rooms must be a positive integer")
    if seed_value < 0 or not seed_value.is_integer():
        raise ValueError("seed must be a non-negative integer")
    n_rooms = int(room_count)
    seed = int(seed_value)

    state = _validate_state_log(
        state_log,
        run_length=run_length,
        n_rooms=n_rooms,
    )
    times = state["time"]
    output_path = _output_path(
        output_dir,
        n_rooms=n_rooms,
        mean_interarrival=mean_interarrival,
        seed=seed,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)

    fig = Figure(
        figsize=(12.4, 8.0),
        facecolor="#F7F8FA",
    )
    FigureCanvasAgg(fig)
    axes = fig.subplots(2, 2, sharex=True)
    fig.subplots_adjust(left=0.075, right=0.985, bottom=0.17, top=0.82, hspace=0.34, wspace=0.08)
    panels = (
        (axes[0, 0], "nurse_busy", _COLORS["nurse"], "Nurse occupancy · B_nurse(t)", "Nurse state"),
        (axes[0, 1], "rooms_busy", _COLORS["rooms"], "Room occupancy · B_rooms(t)", "Busy rooms"),
        (axes[1, 0], "triage_queue", _COLORS["triage_queue"], "Triage queue · Q_triage(t)", "Patients waiting"),
        (axes[1, 1], "room_queue", _COLORS["room_queue"], "Room queue · Q_room(t)", "Patients waiting"),
    )

    for ax, column, color, title, y_label in panels:
        series = state[column]
        ax.step(times, series, where="post", color=color, linewidth=2.1, zorder=3)
        ax.fill_between(times, 0, series, step="post", color=color, alpha=0.12, zorder=2)
        ax.set_title(title, loc="left", fontsize=12, fontweight="semibold", pad=9)
        ax.set_ylabel(y_label, fontsize=9)
        ax.set_xlim(0, run_length)
        ax.set_ylim(bottom=-0.05)
        ax.yaxis.set_major_locator(MaxNLocator(integer=True))
        ax.grid(axis="y", color="#D9DEE7", linewidth=0.8, alpha=0.75)
        ax.set_axisbelow(True)
        ax.spines["top"].set_visible(False)
        ax.spines["right"].set_visible(False)
        ax.spines["left"].set_color("#CBD2DC")
        ax.spines["bottom"].set_color("#CBD2DC")
        ax.tick_params(colors="#344054", labelsize=8)

    axes[0, 0].set_yticks((0, 1))
    axes[0, 0].set_yticklabels(("Idle", "Busy"))
    axes[0, 1].set_ylim(-0.05, n_rooms + 0.35)
    axes[0, 1].set_yticks(range(n_rooms + 1))
    axes[0, 1].axhline(
        n_rooms,
        color="#667085",
        linestyle=(0, (4, 3)),
        linewidth=1.0,
        alpha=0.8,
        zorder=1,
    )
    axes[0, 1].text(
        0.99,
        n_rooms,
        f" capacity {n_rooms}",
        transform=axes[0, 1].get_yaxis_transform(),
        va="bottom",
        ha="right",
        color="#667085",
        fontsize=8,
    )
    for ax in axes[1, :]:
        ax.set_xlabel("Simulation time (minutes)", fontsize=9)

    fig.suptitle(
        "Emergency room · occupancy timeline",
        x=0.035,
        y=0.975,
        ha="left",
        fontsize=17,
        fontweight="bold",
        color="#172B4D",
    )
    fig.text(
        0.035,
        0.925,
        f"{n_rooms} rooms  ·  mean interarrival {mean_interarrival:g} min  ·  "
        f"run {run_length:g} min  ·  seed {seed}",
        fontsize=9,
        color="#667085",
    )
    fig.text(
        0.035,
        0.035,
        "Step levels are post-event states; simultaneous changes have zero duration.",
        fontsize=8,
        color="#667085",
    )
    fig.savefig(output_path, dpi=180, facecolor=fig.get_facecolor())

    return output_path
