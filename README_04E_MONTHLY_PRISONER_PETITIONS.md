# Milestone 2D-F-C — Monthly Pregnant-Prisoner Petitions 04e

04e **supersedes the manual-dialogue prisoner test in 04d/04d2**.

The pregnant prisoner now initiates the interaction. The player does not need to manually talk to her.

## Locked behavior in this test build

- A pregnant noblewoman held by the player/player clan requests an audience on the next safe hourly campaign check once PLE can resolve her normalized pregnancy month.
- Her first request during a captivity episode counts as that pregnancy month's request.
- If she remains imprisoned, she may request release again when her normalized pregnancy month advances.
- A separately refused normalized pregnancy month applies **-2 relation** with the prisoner.
- The same month cannot apply the refusal penalty twice, including after save/reload.
- Releasing/escaping/birth/pregnancy loss/death closes the captivity petition state.
- A later recapture starts a new captivity episode because the prior episode state is cleared on native release.
- Main-party prisoners, prisoners in another player-clan captor party, and prisoners in player-owned settlement dungeons qualify.
- If PLE cannot resolve the native pregnancy month yet, the petition is deferred rather than guessing a month.

## Player choices

**Chivalric Maternal Release**
- Uses Bannerlord's native `EndCaptivityAction.ApplyByReleasedByChoice` path.
- No custom teleport is used.
- Positive relation/honor rewards are intentionally deferred until the release path and monthly ledger are live-validated.

**Continue Imprisonment (-2 relation)**
- Applies -2 relation once for that normalized pregnancy month.
- Records the player as the informed detention decision-maker for the later captivity-risk/responsibility layer.

## Live test — current Sira / Sukayna save

1. Overlay 04e and rebuild. Stop on any compile error.
2. Load the existing save with Sira and Sukayna in Ragnar's prisoner roster.
3. Let campaign time advance. Within the next in-game hourly check, one eligible pregnant prisoner should produce a required native inquiry titled:
   `Pregnant Prisoner Requests an Audience`
4. Choose **Continue Imprisonment (-2 relation)** first.
5. Expected log includes:
   - `PLE pregnant-prisoner petition queued:`
   - `decision=ContinueImprisonment`
   - `relationChange=-2`
   - `This normalized pregnancy month is now resolved and will not be penalized again after save/load.`
6. Confirm her relation changes by exactly -2.
7. Save, reload, and advance at least one in-game hour in the **same normalized pregnancy month**. That woman must not receive another petition or another -2 penalty for that month.
8. Other eligible pregnant prisoners may still petition for their own unresolved current month, one per hourly check.
9. Reload a pre-choice save and test **Chivalric Maternal Release**. She should be removed through Bannerlord's native release path and the log should include `decision=ChivalricMaternalRelease, nativeRelease=True`.

Do not commit 04e until the refusal, reload-deduplication, and release paths pass live testing.
