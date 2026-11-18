"""Tests for the M1 model and the event log.

Run from the repository root:

    python -m unittest discover -s tests -v
"""

import csv
import math
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "src"))

from er_model import (  # noqa: E402
    EVENT_LOG_COLUMNS, PATIENT_COLUMNS, SCENARIOS, STATE_COLUMNS,
    RandomStreams, event_log_path, run_simulation, write_event_log,
)


class TestModelOutputContract(unittest.TestCase):
    """docs/model_output.md"""

    @classmethod
    def setUpClass(cls):
        cls.results = {name: run_simulation(n_rooms=r, mean_interarrival=ia,
                                            run_length=1440.0, seed=1)
                       for name, (r, ia) in SCENARIOS.items()}

    def test_params(self):
        res = run_simulation(n_rooms=4, mean_interarrival=6, run_length=500, seed=7)
        self.assertEqual(res.params, {"n_rooms": 4, "mean_interarrival": 6.0,
                                      "run_length": 500.0, "seed": 7})

    def test_columns(self):
        for res in self.results.values():
            self.assertEqual(list(res.patients.columns), PATIENT_COLUMNS)
            self.assertEqual(list(res.state_log.columns), ["time"] + STATE_COLUMNS)

    def test_patient_ids_and_order(self):
        for res in self.results.values():
            p = res.patients
            self.assertEqual(p["patient_id"].tolist(), list(range(1, len(p) + 1)))
            self.assertTrue(p["arrival"].is_monotonic_increasing)
            self.assertTrue(set(p["severity"]) <= {"low", "medium", "high"})

    def test_times_are_ordered_and_nan_after_end(self):
        cols = ["arrival", "triage_start", "triage_end", "room_start", "departure"]
        for res in self.results.values():
            p = res.patients
            t = p[cols].to_numpy()
            for a, b in zip(range(4), range(1, 5)):
                ok = np.isnan(t[:, b]) | (t[:, b] >= t[:, a])
                self.assertTrue(ok.all())
                # a later time is never reached if an earlier one was not
                self.assertTrue((~np.isnan(t[:, b]) <= ~np.isnan(t[:, a])).all())
            self.assertTrue((np.nan_to_num(t, nan=0) <= res.params["run_length"]).all())
            # room_id is set exactly when room_start is reached
            has_room = p["room_id"] != ""
            self.assertTrue((has_room == p["room_start"].notna()).all())

    def test_fcfs(self):
        for res in self.results.values():
            p = res.patients
            for col in ["triage_start", "room_start"]:
                s = p[col].dropna()
                self.assertTrue(s.is_monotonic_increasing, col)
                # a patient is served only if all earlier patients were
                self.assertEqual(s.index.tolist(), list(range(len(s))))

    def test_triage_one_at_a_time(self):
        for res in self.results.values():
            p = res.patients.dropna(subset=["triage_end"])
            self.assertTrue((p["triage_start"].to_numpy()[1:] >=
                             p["triage_end"].to_numpy()[:-1]).all())

    def test_rooms_not_shared(self):
        for res in self.results.values():
            p = res.patients[res.patients["room_id"] != ""]
            for _, g in p.groupby("room_id"):
                g = g.sort_values("room_start")
                end = g["departure"].fillna(math.inf).to_numpy()
                self.assertTrue((g["room_start"].to_numpy()[1:] >= end[:-1]).all())
            n = res.params["n_rooms"]
            self.assertTrue(set(p["room_id"]) <= {f"room_{k}" for k in range(1, n + 1)})

    def test_state_log(self):
        for res in self.results.values():
            s = res.state_log
            n = res.params["n_rooms"]
            self.assertEqual(s.iloc[0].tolist(), [0, 0, 0, 0, 0])
            self.assertEqual(s["time"].iloc[-1], res.params["run_length"])
            self.assertTrue(s["time"].is_monotonic_increasing)
            self.assertTrue((s[STATE_COLUMNS] >= 0).all().all())
            self.assertTrue((s["nurse_busy"] <= 1).all())
            self.assertTrue((s["rooms_busy"] <= n).all())
            # nobody waits for a room while a room is free (after each instant)
            last = s.groupby("time").tail(1)
            self.assertTrue(((last["room_queue"] == 0) | (last["rooms_busy"] == n)).all())
            self.assertTrue(((last["triage_queue"] == 0) | (last["nurse_busy"] == 1)).all())

    def test_state_log_matches_patients(self):
        """Final state equals the number of patients in each stage."""
        for res in self.results.values():
            p, last = res.patients, res.state_log.iloc[-1]
            self.assertEqual(last["triage_queue"], p["triage_start"].isna().sum())
            self.assertEqual(last["nurse_busy"],
                             (p["triage_start"].notna() & p["triage_end"].isna()).sum())
            self.assertEqual(last["room_queue"],
                             (p["triage_end"].notna() & p["room_start"].isna()).sum())
            self.assertEqual(last["rooms_busy"],
                             (p["room_start"].notna() & p["departure"].isna()).sum())

    def test_reproducible(self):
        a = run_simulation(6, 3.0, 1440.0, seed=3)
        b = run_simulation(6, 3.0, 1440.0, seed=3)
        pd.testing.assert_frame_equal(a.patients, b.patients)
        pd.testing.assert_frame_equal(a.state_log, b.state_log)
        c = run_simulation(6, 3.0, 1440.0, seed=4)
        self.assertFalse(a.patients["arrival"].equals(c.patients["arrival"]))

    def test_common_random_numbers(self):
        """Same seed: same arrivals and severities whatever the number of rooms."""
        a = self.results["Base"].patients   # 6 rooms, ia 3
        c = self.results["C"].patients      # 4 rooms, ia 3
        pd.testing.assert_series_equal(a["arrival"], c["arrival"])
        pd.testing.assert_series_equal(a["severity"], c["severity"])

    def test_invalid_parameters(self):
        for kwargs in [dict(n_rooms=0), dict(n_rooms=2.5), dict(mean_interarrival=0),
                       dict(run_length=-1)]:
            with self.assertRaises(ValueError):
                run_simulation(**kwargs)


class TestInputDistributions(unittest.TestCase):

    def test_triangular_parameter_order(self):
        s = RandomStreams(1)
        x = np.array([s.triage_time() for _ in range(50_000)])
        self.assertGreaterEqual(x.min(), 2.0)
        self.assertLessEqual(x.max(), 6.0)
        self.assertAlmostEqual(x.mean(), 4.0, delta=0.02)
        # mode 4: the bin around 4 is the most frequent one
        hist, edges = np.histogram(x, bins=8, range=(2, 6))
        self.assertIn(hist.argmax(), (3, 4))

    def test_treatment_times_positive_and_means(self):
        s = RandomStreams(2)
        for sev, mean in [("low", 15), ("medium", 25), ("high", 45)]:
            x = np.array([s.treatment_time(sev) for _ in range(20_000)])
            self.assertTrue((x > 0).all())
            self.assertAlmostEqual(x.mean(), mean, delta=0.3)

    def test_severity_shares(self):
        s = RandomStreams(3)
        x = pd.Series([s.severity() for _ in range(50_000)])
        shares = x.value_counts(normalize=True)
        for sev, prob in [("low", 0.5), ("medium", 0.3), ("high", 0.2)]:
            self.assertAlmostEqual(shares[sev], prob, delta=0.01)


class TestEventLog(unittest.TestCase):
    """docs/event_log_format.md"""

    def test_path(self):
        self.assertEqual(event_log_path(6, 3.0).as_posix(), "data/events_rooms6_ia3.csv")
        self.assertEqual(event_log_path(8, 6).as_posix(), "data/events_rooms8_ia6.csv")

    def test_format_and_consistency(self):
        for name, (r, ia) in SCENARIOS.items():
            with self.subTest(scenario=name):
                res = run_simulation(r, ia, 1440.0, seed=1)
                with tempfile.TemporaryDirectory() as d:
                    path = Path(d) / "log.csv"
                    write_event_log(res, path)
                    raw = path.read_text(encoding="utf-8")
                    with open(path, encoding="utf-8", newline="") as f:
                        rows = list(csv.reader(f))
                self.assertNotIn("\r", raw)
                self.assertEqual(rows[0], EVENT_LOG_COLUMNS)
                body = rows[1:]
                p = res.patients

                # end row
                self.assertEqual(body[-1][:5], ["1440.00", "", "end", "", ""])
                self.assertEqual([int(x) for x in body[-1][5:]],
                                 res.state_log.iloc[-1][STATE_COLUMNS].tolist())
                self.assertEqual(sum(r_[2] == "end" for r_ in body), 1)

                # 2-decimal times, sorted
                times = [float(r_[0]) for r_ in body]
                self.assertTrue(all(len(r_[0].split(".")[1]) == 2 for r_ in body))
                self.assertEqual(times, sorted(times))

                # one row per reached patient time, events in allowed set
                counts = pd.Series([r_[2] for r_ in body]).value_counts()
                expected = {"arrive": p["arrival"].notna().sum(),
                            "triage_start": p["triage_start"].notna().sum(),
                            "triage_end": p["triage_end"].notna().sum(),
                            "room_start": p["room_start"].notna().sum(),
                            "room_end": p["departure"].notna().sum(),
                            "end": 1}
                self.assertEqual({k: int(v) for k, v in counts.items()},
                                 {k: int(v) for k, v in expected.items() if v})

                # resources, severities and stable room numbering
                rooms = {}
                for r_ in body[:-1]:
                    pid, ev, sev, resrc = int(r_[1]), r_[2], r_[3], r_[4]
                    self.assertEqual(sev, p.loc[pid - 1, "severity"])
                    if ev == "arrive":
                        self.assertEqual(resrc, "")
                    elif ev.startswith("triage"):
                        self.assertEqual(resrc, "nurse")
                    else:
                        self.assertRegex(resrc, rf"^room_[1-{r}]$")
                        if ev == "room_start":
                            rooms[pid] = resrc
                        else:
                            self.assertEqual(rooms[pid], resrc)

                # state columns equal the state log (rows between first and last)
                state = res.state_log[STATE_COLUMNS].to_numpy()[1:-1]
                logged = np.array([[int(x) for x in r_[5:]] for r_ in body[:-1]])
                np.testing.assert_array_equal(state, logged)

                # replaying the events reproduces the state columns
                tq = rq = nb = rb = 0
                for r_ in body[:-1]:
                    ev = r_[2]
                    tq += {"arrive": 1, "triage_start": -1}.get(ev, 0)
                    nb += {"triage_start": 1, "triage_end": -1}.get(ev, 0)
                    rq += {"triage_end": 1, "room_start": -1}.get(ev, 0)
                    rb += {"room_start": 1, "room_end": -1}.get(ev, 0)
                    self.assertEqual([tq, rq, nb, rb], [int(x) for x in r_[5:]])


if __name__ == "__main__":
    unittest.main()
