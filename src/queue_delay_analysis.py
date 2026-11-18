"""Replication-based confidence intervals for the two ER queue delays.

The defaults follow ``docs/assumptions.md``: 10 initial replications, 95%
Student-t confidence intervals, and a relative error target of 10% for each
queue.  Each replication contributes one average delay for triage and one for
the room queue, as defined in ``src/performance_metrics.py``.
"""

from __future__ import annotations

from math import isfinite, sqrt
from typing import Mapping

import pandas as pd
from scipy import stats

if __package__:
    from .er_model import RUN_LENGTH, SCENARIOS, run_simulation
    from .performance_metrics import compute_performance_metrics
else:  # Support notebooks that add ``src`` directly to ``sys.path``.
    from er_model import RUN_LENGTH, SCENARIOS, run_simulation
    from performance_metrics import compute_performance_metrics


def _validate_options(
    n0: int, relative_error: float, confidence_level: float
) -> None:
    if isinstance(n0, bool) or int(n0) != n0 or n0 < 2:
        raise ValueError("n0 must be an integer of at least 2")
    if not isfinite(relative_error) or relative_error <= 0:
        raise ValueError("relative_error must be finite and greater than zero")
    if not isfinite(confidence_level) or not 0 < confidence_level < 1:
        raise ValueError("confidence_level must be between 0 and 1")


def confidence_interval(
    observations: list[float], confidence_level: float = 0.95
) -> tuple[float, float, float, float]:
    """Return ``(mean, lower, upper, half_width)`` using a two-sided t interval."""
    if not 0 < confidence_level < 1:
        raise ValueError("confidence_level must be between 0 and 1")
    values = pd.Series(observations, dtype="float64")
    if len(values) < 2:
        raise ValueError("at least two replication observations are required")
    if values.isna().any() or not values.map(isfinite).all():
        raise ValueError("replication observations must be finite numbers")
    if (values < 0).any():
        raise ValueError("queue delays cannot be negative")

    n = len(values)
    mean = float(values.mean())
    standard_error = float(values.std(ddof=1)) / sqrt(n)
    critical_value = float(stats.t.ppf(0.5 + confidence_level / 2, n - 1))
    half_width = critical_value * standard_error
    return mean, mean - half_width, mean + half_width, half_width


def required_replications(
    initial_observations: list[float],
    *,
    n0: int = 10,
    relative_error: float = 0.10,
    confidence_level: float = 0.95,
) -> int:
    """Find the smallest ``n >= n0`` meeting the relative half-width target.

    The initial sample standard deviation and mean stay fixed while searching,
    matching the sample-size rule in ``docs/assumptions.md``.
    """
    _validate_options(n0, relative_error, confidence_level)
    mean, _, _, _ = confidence_interval(initial_observations, confidence_level)
    values = pd.Series(initial_observations, dtype="float64")
    sample_sd = float(values.std(ddof=1))
    if mean == 0:
        if sample_sd == 0:
            return int(n0)
        raise ValueError(
            "a finite relative-precision replication count cannot be computed "
            "when the initial mean delay is zero and its sample deviation is positive"
        )

    target_half_width = relative_error * abs(mean)

    def half_width(n: int) -> float:
        critical = float(stats.t.ppf(0.5 + confidence_level / 2, n - 1))
        return critical * sample_sd / sqrt(n)

    if half_width(int(n0)) <= target_half_width:
        return int(n0)

    # The t half-width decreases with n; bracket the solution, then find its
    # first integer value with binary search instead of stepping one by one.
    low = int(n0) + 1
    high = max(int(n0) * 2, low)
    while half_width(high) > target_half_width:
        low = high + 1
        high *= 2

    while low < high:
        middle = (low + high) // 2
        if half_width(middle) <= target_half_width:
            high = middle
        else:
            low = middle + 1
    return low


def analyze_queue_delays(
    scenarios: Mapping[str, tuple[int, float]] | None = None,
    *,
    n0: int = 10,
    relative_error: float = 0.10,
    confidence_level: float = 0.95,
    run_length: float = RUN_LENGTH,
) -> pd.DataFrame:
    """Run the initial and required replications and return a comparison table.

    The same seed ``i`` is used for replication ``i`` in every scenario. Each
    scenario's final sample size is the larger requirement from its two queues.
    Initial replications are reused when computing the final intervals.
    """
    _validate_options(n0, relative_error, confidence_level)
    if not isfinite(run_length) or run_length <= 0:
        raise ValueError("run_length must be finite and greater than zero")

    scenario_map = SCENARIOS if scenarios is None else scenarios
    rows: list[dict[str, object]] = []
    for scenario, (n_rooms, mean_interarrival) in scenario_map.items():
        initial: dict[str, list[float]] = {"triage": [], "room": []}
        for seed in range(1, int(n0) + 1):
            result = run_simulation(
                n_rooms=n_rooms,
                mean_interarrival=mean_interarrival,
                run_length=run_length,
                seed=seed,
            )
            metrics = compute_performance_metrics(result)
            for queue, delay in (
                ("triage", metrics.average_triage_delay),
                ("room", metrics.average_room_delay),
            ):
                if delay is None:
                    raise ValueError(
                        f"scenario {scenario!r}, seed {seed}: no patients started "
                        f"service from the {queue} queue, so its delay is undefined"
                    )
                initial[queue].append(float(delay))

        required = {
            queue: required_replications(
                observations,
                n0=n0,
                relative_error=relative_error,
                confidence_level=confidence_level,
            )
            for queue, observations in initial.items()
        }
        n_star = max(required.values())
        final = {queue: values.copy() for queue, values in initial.items()}

        for seed in range(int(n0) + 1, n_star + 1):
            result = run_simulation(
                n_rooms=n_rooms,
                mean_interarrival=mean_interarrival,
                run_length=run_length,
                seed=seed,
            )
            metrics = compute_performance_metrics(result)
            for queue, delay in (
                ("triage", metrics.average_triage_delay),
                ("room", metrics.average_room_delay),
            ):
                if delay is None:
                    raise ValueError(
                        f"scenario {scenario!r}, seed {seed}: no patients started "
                        f"service from the {queue} queue, so its delay is undefined"
                    )
                final[queue].append(float(delay))

        triage_initial = confidence_interval(initial["triage"], confidence_level)
        room_initial = confidence_interval(initial["room"], confidence_level)
        triage_final = confidence_interval(final["triage"], confidence_level)
        room_final = confidence_interval(final["room"], confidence_level)
        rows.append(
            {
                "scenario": scenario,
                "n_rooms": n_rooms,
                "mean_interarrival": mean_interarrival,
                "n0": int(n0),
                "triage_initial_mean": triage_initial[0],
                "triage_initial_ci_low": triage_initial[1],
                "triage_initial_ci_high": triage_initial[2],
                "triage_required_n": required["triage"],
                "room_initial_mean": room_initial[0],
                "room_initial_ci_low": room_initial[1],
                "room_initial_ci_high": room_initial[2],
                "room_required_n": required["room"],
                "scenario_n_star": n_star,
                "triage_n_star_mean": triage_final[0],
                "triage_n_star_ci_low": triage_final[1],
                "triage_n_star_ci_high": triage_final[2],
                "room_n_star_mean": room_final[0],
                "room_n_star_ci_low": room_final[1],
                "room_n_star_ci_high": room_final[2],
            }
        )

    return pd.DataFrame(rows)
