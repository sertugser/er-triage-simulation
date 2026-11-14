# ER Triage Simulation

Discrete-event simulation of a hospital emergency room: patients pass
through a single triage nurse, then wait for one of several treatment
rooms. Built with SimPy; replayed in 3D with Unity.

## Getting started

```bash
pip install -r requirements.txt
jupyter notebook notebooks/
```

## Repository layout

- `src/` simulation model and analysis code
- `notebooks/` main notebook (one person edits it at a time). During development it imports code from `src/`; the final version contains all code in its own cells
- `data/` generated event logs (CSV)
- `figures/` plots and the event graph
- `docs/` event log format and modelling assumptions
- `unity/` 3D replay project

## Status

Work in progress.
