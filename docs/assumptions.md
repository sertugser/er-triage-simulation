# Modelling assumptions

Decisions taken where the problem statement leaves a choice. Every analysis result depends on these; change them only through a pull request that updates this file.

## System (from the problem statement)

| Item | Value |
| --- | --- |
| Interarrival time | Exponential, mean 3 min (base) or 6 min |
| Triage | 1 nurse, FCFS, triangular(min 2, mode 4, max 6) min |
| Treatment rooms | 6 (base), 4 or 8; one physician per room |
| Severity | low 0.5, medium 0.3, high 0.2 |
| Treatment time | low N(15, 3), medium N(25, 5), high N(45, 8) min |

Note: libraries order the triangular parameters differently. Python `random.triangular` takes `(low, high, mode)`, NumPy `triangular` takes `(left, mode, right)`. Always pass them by name:

```python
random.triangular(low=2, high=6, mode=4)   # Python random
rng.triangular(left=2, mode=4, right=6)    # NumPy
```

With Python `random` a wrong order does not raise an error; it silently produces a wrong distribution.

## Decisions

| # | Topic | Decision | Reason |
| --- | --- | --- | --- |
| 1 | Run type and length | Terminating simulation of one day: 1440 min. Every replication starts empty and idle; no warm-up | An ER runs around the clock; some scenarios have utilisation above 1 and no steady state |
| 2 | Room queue discipline | FCFS | The statement defines FCFS for triage and no priority rule for rooms; severity is specified to affect service time only |
| 3 | Negative treatment times | A sample <= 0 is redrawn until it is positive | Keeps the normal shape; with the given means and deviations it practically never happens |
| 4 | Patients still in the system at the end | Average delay counts patients who started service in that queue; average time in system counts patients who departed. Time averages and utilisation use the whole interval [0, 1440] | Standard treatment for terminating runs. In unstable scenarios the averages of delays are biased low, because the longest waiters are still queued at the end; this is reported in the notebook |
| 5 | "Waiting more than 30 minutes" | Main measure: triage wait + room wait > 30 min, over patients who entered a room. Also reported: triage wait > 30 and room wait > 30 separately | The statement does not define the wait; reporting all three covers every reading |
| 6 | Confidence level | 95% | |
| 7 | Initial replications | n0 = 10 | |
| 8 | Error margin | beta = 10% of the estimated mean delay (relative), per queue | Delays differ by orders of magnitude between scenarios; an absolute value would not fit all |
| 9 | Required replications | Smallest n >= n0 with t(n-1, 0.975) * s / sqrt(n) <= beta, computed for each queue. Per scenario, n* is the larger of the two queues' values; each scenario is then run with its own n* and its intervals recomputed | Scenarios differ strongly in variability; unstable ones need more replications |
| 10 | Seeds | Replication i uses seed i in every scenario | Reproducible results; scenarios are compared on the same random numbers |

## Scenarios

| Scenario | Rooms | Mean interarrival (min) | Nurse rho | Room rho |
| --- | --- | --- | --- | --- |
| Base | 6 | 3 | 1.33 | 1.33 |
| A | 4 | 6 | 0.67 | 1.00 |
| B | 8 | 6 | 0.67 | 0.50 |
| C | 4 | 3 | 1.33 | 2.00 |
| D | 8 | 3 | 1.33 | 1.00 |

rho = mean service time / (mean interarrival x servers); mean triage 4 min, mean treatment 0.5x15 + 0.3x25 + 0.2x45 = 24 min.

## Required outputs

For every scenario:

- Average delay in the triage queue and in the room queue
- Average time in system
- Time-average number in each queue and in the system
- Utilisation of the nurse and of the rooms
- B(t) for the nurse and the rooms, Q(t) for both queues
- Share of patients waiting more than 30 minutes (decision 5)
- 95% confidence intervals for the average delay in each queue, with n0 and with n*
