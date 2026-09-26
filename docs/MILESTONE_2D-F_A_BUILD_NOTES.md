# Milestone 2D-F-A — Withdrawal Planning and Elite Escort Allocation

Baseline: Milestone 2D-E-03e (locked).

This overlay deliberately does **not** modify the Milestone 2D-E campaign execution files.
It adds the pure escort-planning layer only.

Locked escort plans:

- Minimal Escort: 5 troops, -25 relationship with the mother, SevereRisk
- Lean Escort: 35 troops, 0 relationship change, Standard
- Strong Escort: 50 troops, +5 relationship with the mother, High

Rules implemented in this increment:

- The withdrawing mother retains the highest-tier ordinary troops available.
- Tier sorts descending, then troop level descending.
- Equal-priority stacks use an ordinal troop-id tie-breaker for reload determinism.
- A cutoff can split a troop stack.
- If fewer troops exist than requested, all available troops are retained; no replacements are created.
- Every stack records original, retained, and surplus counts.
- The allocation exposes conservation checks so no troop can be silently lost or duplicated by the planner.
- No Bannerlord roster is changed in 2D-F-A.
- Relationship values are recorded as plan metadata only; campaign application comes with the later decision/execution integration and must be one-time/save-safe.
- `Remain in service` remains a withdrawal decision handled by the existing 2D-D/2D-E responsibility flow; it is not an escort plan.

Next increment after local validation: 2D-F-B Random Events-style player decision presentation using Minimal / Lean / Strong / Remain in Service.
