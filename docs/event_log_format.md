# Event log format

The event log is the only interface between the simulation (Python) and the 3D replay (Unity).
It is produced from the model output (see `model_output.md`); Unity only reads it and does no calculations of its own.

## Files

- One file per scenario, replication with `seed = 1`.
- Location and name: `data/events_rooms{R}_ia{IA}.csv`, e.g. `data/events_rooms6_ia3.csv`
  - `R`: number of treatment rooms (4, 6 or 8)
  - `IA`: mean interarrival time in minutes (3 or 6)
- Encoding UTF-8, comma separated, `.` as decimal separator, one header row.

## Columns

| Column | Type | Description |
| --- | --- | --- |
| `time` | float, 2 decimals | Simulation time in minutes |
| `patient_id` | int | Starts at 1, in order of arrival. Empty on the `end` row |
| `event` | text | One of the events below |
| `severity` | text | `low`, `medium` or `high`. Empty on the `end` row |
| `resource` | text | `nurse`, `room_1` ... `room_R`. Empty on `arrive` and `end` |
| `triage_queue` | int | Patients waiting for triage, **after** this event |
| `room_queue` | int | Patients waiting for a room, **after** this event |
| `nurse_busy` | int | 0 or 1, **after** this event |
| `rooms_busy` | int | Occupied rooms, **after** this event |

The four state columns let Unity show counters without computing them, so the replay always matches the Q(t) and B(t) plots.

## Events

| Event | Meaning |
| --- | --- |
| `arrive` | Patient arrives and joins the triage queue |
| `triage_start` | Nurse starts triage |
| `triage_end` | Triage finished; patient joins the room queue |
| `room_start` | Patient enters a treatment room |
| `room_end` | Treatment finished; patient leaves the system |
| `end` | End of the run (`time` = run length); last row of the file |

Queue waiting is not a separate event: a patient is in the triage queue between `arrive` and `triage_start`, and in the room queue between `triage_end` and `room_start`.

## Rules

- Rows are sorted by `time`. Events at the same time keep the order in which they happened in the simulation.
- Events that would happen after the run length are not written. Patients still in the system at the end simply have no later rows.
- Room numbering is stable: a patient keeps the same room from `room_start` to `room_end`.

## Example

```csv
time,patient_id,event,severity,resource,triage_queue,room_queue,nurse_busy,rooms_busy
3.42,1,arrive,high,,1,0,0,0
3.42,1,triage_start,high,nurse,0,0,1,0
7.65,1,triage_end,high,nurse,0,1,0,0
7.65,1,room_start,high,room_3,0,0,0,1
52.10,1,room_end,high,room_3,0,0,0,0
1440.00,,end,,,12,30,1,6
```
