"""Compare all documented performance measures across ER scenarios."""

from __future__ import annotations

from math import isfinite
from pathlib import Path
from typing import Mapping

import pandas as pd

if __package__:
    from .er_model import (
        RUN_LENGTH,
        SCENARIOS,
        SEVERITIES,
        SEVERITY_PROBS,
        TREATMENT_PARAMS,
        TRIAGE_LEFT,
        TRIAGE_MODE,
        TRIAGE_RIGHT,
        run_simulation,
    )
    from .performance_metrics import PerformanceMetrics, compute_performance_metrics
    from .queue_delay_analysis import confidence_interval
else:  # Support notebooks that add ``src`` directly to ``sys.path``.
    from er_model import (
        RUN_LENGTH,
        SCENARIOS,
        SEVERITIES,
        SEVERITY_PROBS,
        TREATMENT_PARAMS,
        TRIAGE_LEFT,
        TRIAGE_MODE,
        TRIAGE_RIGHT,
        run_simulation,
    )
    from performance_metrics import PerformanceMetrics, compute_performance_metrics
    from queue_delay_analysis import confidence_interval


_METRIC_FIELDS = tuple(PerformanceMetrics.__dataclass_fields__)
_DEFAULT_N_STAR_PATH = (
    Path(__file__).resolve().parent.parent
    / "data"
    / "queue_delay_confidence_intervals.csv"
)
_MEAN_TRIAGE_TIME = (TRIAGE_LEFT + TRIAGE_MODE + TRIAGE_RIGHT) / 3
_MEAN_TREATMENT_TIME = sum(
    probability * TREATMENT_PARAMS[severity][0]
    for severity, probability in zip(SEVERITIES, SEVERITY_PROBS)
)


def _load_n_star_values(
    path: str | Path, scenarios: Mapping[str, tuple[int, float]]
) -> dict[str, int]:
    table = pd.read_csv(path)
    required_columns = {"scenario", "scenario_n_star"}
    missing = required_columns.difference(table.columns)
    if missing:
        raise ValueError(
            f"{path} is missing required columns: {sorted(missing)}"
        )
    if table["scenario"].duplicated().any():
        raise ValueError(f"{path} contains duplicate scenario rows")

    raw_values = table.set_index("scenario")["scenario_n_star"]
    result: dict[str, int] = {}
    for scenario in scenarios:
        if scenario not in raw_values.index:
            raise ValueError(f"{path} has no n* value for scenario {scenario!r}")
        value = float(raw_values.loc[scenario])
        if not isfinite(value) or not value.is_integer() or value < 2:
            raise ValueError(
                f"{path} has an invalid n* value for scenario {scenario!r}: {value!r}"
            )
        result[scenario] = int(value)
    return result


def compare_scenarios(
    scenarios: Mapping[str, tuple[int, float]] | None = None,
    *,
    n_star_path: str | Path = _DEFAULT_N_STAR_PATH,
    confidence_level: float = 0.95,
    run_length: float = RUN_LENGTH,
) -> pd.DataFrame:
    """Run each scenario for its documented ``n*`` and summarize every metric.

    ``n*`` is read from the queue-delay analysis output, which is calculated
    using decision 9 in ``docs/assumptions.md``. The function runs seeds
    ``1..n*`` for each scenario and averages each replication-level performance
    measure. It also returns 95% t intervals for the two average queue delays.
    """
    if not 0 < confidence_level < 1:
        raise ValueError("confidence_level must be between 0 and 1")
    if not isfinite(run_length) or run_length <= 0:
        raise ValueError("run_length must be finite and greater than zero")

    scenario_map = SCENARIOS if scenarios is None else scenarios
    n_star_values = _load_n_star_values(n_star_path, scenario_map)
    rows: list[dict[str, object]] = []

    for scenario, (n_rooms, mean_interarrival) in scenario_map.items():
        n_star = n_star_values[scenario]
        nurse_rho = _MEAN_TRIAGE_TIME / mean_interarrival
        rooms_rho = _MEAN_TREATMENT_TIME / (mean_interarrival * n_rooms)
        replications: list[dict[str, float | int | None]] = []
        for seed in range(1, n_star + 1):
            result = run_simulation(
                n_rooms=n_rooms,
                mean_interarrival=mean_interarrival,
                run_length=run_length,
                seed=seed,
            )
            replications.append(compute_performance_metrics(result).as_dict())

        metrics = pd.DataFrame(replications, columns=_METRIC_FIELDS)
        row: dict[str, object] = {
            "scenario": scenario,
            "n_rooms": n_rooms,
            "mean_interarrival": mean_interarrival,
            "run_length": float(run_length),
            "n_star": n_star,
            "nurse_rho": nurse_rho,
            "rooms_rho": rooms_rho,
            "system_stability": (
                "stable" if nurse_rho < 1 and rooms_rho < 1 else "not stable"
            ),
        }
        row.update(metrics.mean(numeric_only=True).to_dict())

        for metric, prefix in (
            ("average_triage_delay", "triage_delay"),
            ("average_room_delay", "room_delay"),
        ):
            observations = metrics[metric].dropna().astype(float).tolist()
            if len(observations) != n_star:
                raise ValueError(
                    f"scenario {scenario!r}: {metric} is undefined for one or "
                    "more replications, so the comparison interval is incomplete"
                )
            _, lower, upper, _ = confidence_interval(
                observations, confidence_level
            )
            row[f"{prefix}_ci_low"] = lower
            row[f"{prefix}_ci_high"] = upper

        rows.append(row)

    return pd.DataFrame(rows)
