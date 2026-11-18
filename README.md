# ER Triage Simulation

Discrete-event simulation of a hospital emergency room: patients pass
through a single triage nurse, then wait for one of several treatment
rooms. Built with SimPy; replayed in 3D with Unity.

## Getting started

```bash
pip install -r requirements.txt
jupyter notebook notebooks/
```

## Simulation model (M1)

```python
import sys; sys.path.insert(0, "src")
from er_model import run_simulation, write_event_log

result = run_simulation(n_rooms=6, mean_interarrival=3.0, run_length=1440.0, seed=1)
write_event_log(result, "data/events_rooms6_ia3.csv")
```

From the repository root:

```bash
python -m unittest discover -s tests -v   # model and event log tests
python src/export_event_logs.py           # data/events_rooms{R}_ia{IA}.csv for all scenarios
python src/event_graph.py                 # figures/event_graph.dot + .png (needs Graphviz)
```

Model validation against queueing theory: `notebooks/m1_model_validation.ipynb`.

## Repository layout

- `src/` simulation model and analysis code
- `notebooks/` main notebook (one person edits it at a time). During development it imports code from `src/`; the final version contains all code in its own cells
- `data/` generated event logs (CSV)
- `figures/` plots and the event graph
- `docs/` event log format, model output and modelling assumptions
- `unity/` 3D replay project

## Status

Work in progress.
