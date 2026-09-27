# Pregnant Lords Expanded — Build 04 Overlay

Overlay this ZIP onto the current accepted repository baseline that already contains Milestone 2D-E 03e, 2D-F-A, 2D-F-B, and 2D-F-C Build 03.

Build 04 adds the combat pregnancy-loss mechanics test pass:

- qualifying native `HeroWounded` hook
- one evaluation per map event
- locked 5% / 15% / 35% stage defaults
- master enable toggle in runtime settings
- separate respect-Bannerlord-death-settings toggle in runtime settings
- Birth and Aging hard gate
- correct player-clan 50% / disabled native protection mapping
- Bannerlord native pregnancy-record cleanup on loss
- responsibility logging through the existing withdrawal ledger
- pure calculation tests for settings and probability mapping

The visible MCM controls/sliders are **not** added yet. The runtime settings bridge is intentionally present now so the validated mechanic can be bound to MCM after live testing.

See `docs/MILESTONE_2D-F_C_COMBAT_PREGNANCY_LOSS_BUILD_04_NOTES.md` for the test plan and expected log lines.

## Build 04c test overlay
Adds the native right-side pregnancy-loss map notification. See `README_04C_TEST.md`.
