# data

Event logs in the format of [`docs/event_log_format.md`](../docs/event_log_format.md), one per scenario.

**The current `events_*.csv` files are samples**, written by
`python unity/tools/make_sample_event_logs.py` so the Unity replay can be built before the
model exists. They use the distributions in `docs/assumptions.md` but are not results of
the project's model. Replace them with the output of `write_event_log` (issue #2); the
Unity editor copies whatever is here into `Assets/StreamingAssets` when it opens and
before every build.
