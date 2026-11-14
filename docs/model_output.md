# Model output

Contract between the model code and the analysis code. Both sides follow this; if it needs to change, change this file first in the same pull request.

## Function

```python
result = run_simulation(n_rooms=6, mean_interarrival=3.0, run_length=1440.0, seed=1)
```

| Parameter | Meaning |
| --- | --- |
| `n_rooms` | Number of treatment rooms (4, 6 or 8) |
| `mean_interarrival` | Mean of the exponential interarrival time, minutes (3 or 6) |
| `run_length` | Length of the run, minutes |
| `seed` | Random seed of this replication |

The function runs one replication and returns an object with the attributes below. All times are in minutes.

## `result.params`

Dictionary with the four parameters above.

## `result.patients` (pandas DataFrame, one row per arrived patient)

| Column | Description |
| --- | --- |
| `patient_id` | Starts at 1, in order of arrival |
| `severity` | `low`, `medium` or `high` |
| `arrival` | Arrival time |
| `triage_start` | Start of triage |
| `triage_end` | End of triage = time the patient joins the room queue |
| `room_start` | Time the patient enters a room |
| `departure` | End of treatment = time the patient leaves |
| `room_id` | `room_1` ... `room_R` |

A time that was not reached before the end of the run is `NaN` (and `room_id` is empty).

## `result.state_log` (pandas DataFrame, one row per state change)

| Column | Description |
| --- | --- |
| `time` | Time of the change |
| `triage_queue` | Patients waiting for triage after the change |
| `room_queue` | Patients waiting for a room after the change |
| `nurse_busy` | 0 or 1 after the change |
| `rooms_busy` | Occupied rooms after the change |

- First row at `time = 0` with all zeros; last row at `time = run_length` with the final state.
- Several rows may share the same time; the last one is the state from that time on.
- The values are step functions: each row holds until the next row's time. Q(t), B(t) and all time averages are computed from this table.

## Event log

```python
write_event_log(result, path)
```

Writes the CSV described in `event_log_format.md` from `result.patients` and `result.state_log`.
