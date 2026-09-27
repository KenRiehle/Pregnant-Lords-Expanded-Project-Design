# Milestone 2D-F-C — Pregnant Prisoner Interaction 04d2

This is a narrow correction to 04d after live testing showed pregnant prisoners still falling through to Bannerlord's ordinary prisoner dialogue.

## Changes from 04d

- Player-controlled captivity detection now follows Bannerlord's own release-authority pattern instead of requiring exact `PartyBase.MainParty` object identity.
  - main-party prisoners qualify;
  - prisoners held by a player-clan captor party qualify;
  - prisoners in a player-owned settlement dungeon qualify.
- Added a second high-priority disclosure entry at `lord_start` so an already-met prisoner's normal greeting cannot bypass the PLE disclosure.
- Added a registration diagnostic:
  - `PLE pregnant-prisoner dialogue lines registered for player-controlled captivity.`
- Disclosure log now records the actual prisoner-party label.
- No combat-pregnancy-loss, withdrawal, escort, relation, honor, or captivity-risk balance changes are included.

## Live test

1. Overlay 04d2 on top of the current repository and rebuild.
2. Start/load the test save. Confirm the RGL log contains the registration diagnostic above.
3. Put Sira or Sukayna in Ragnar's prisoner roster (or use a player-owned dungeon).
4. Talk to her.
5. Expected before ordinary prisoner options:
   `Before you decide my fate, know this: I am with child. I ask that you spare my child the dangers of captivity.`
6. Test **Retain in Captivity** first. Expected log contains `decision=RetainInCaptivity, informed=True`.
7. Talk again. Expected special reconsidered Chivalric Maternal Release option.
8. Reload pre-choice save and test immediate **Chivalric Maternal Release**. Expected log contains `decision=ChivalricMaternalRelease, nativeRelease=True` and she is released through Bannerlord's native captivity action.

Do not commit until both paths pass live testing.
