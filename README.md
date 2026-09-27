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

## Build 04e — monthly pregnant-prisoner petitions

04e supersedes the manual prisoner-dialogue experiment in 04d/04d2. Pregnant prisoners now request an audience automatically once per unresolved normalized pregnancy month while held by the player/player clan. Refusal applies -2 relation once for that month; Chivalric Maternal Release uses Bannerlord's native release action. See `README_04E_MONTHLY_PRISONER_PETITIONS.md`.

## Build 04e1 — compile fix
Adds the Bannerlord 1.5.3 five-argument `HeroPrisonerReleased` event handler signature. No gameplay logic changes.

## Build 04e2 — Personality-driven pregnant-prisoner petitions

Build 04e2 preserves the validated monthly petition/release/refusal mechanics from 04e1 and adds deterministic personality-based petition wording, title-aware honorifics, and save-persistent refusal escalation. Escort allocation/travel remains intentionally deferred to the next isolated build. See `README_04E2_PERSONALITY_PETITIONS.md`.
