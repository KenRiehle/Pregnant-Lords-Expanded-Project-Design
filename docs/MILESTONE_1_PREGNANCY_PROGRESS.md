# Milestone 1 — Pregnancy Detection and Normalized Progress

## Locked Project Direction

- Initial game target: **Mount & Blade II: Bannerlord 1.5.2**
- Project form: **new standalone mod**
- The existing **Pregnant Lords Stay Home** mod is reference material only.
- The mod observes Bannerlord's active pregnancy system. It does not replace conception, fertility, pregnancy chance, pregnancy duration, twins, or normal birth handling.

## Purpose

Milestone 1 establishes one reliable service that answers:

1. Is this eligible female hero currently pregnant?
2. How far through the active pregnancy is she?
3. What approximate month, from 1 through 9, should campaign rules and dialogue use?

No withdrawal, teleportation, party changes, combat risks, or dialogue should be implemented until this service is proven reliable in a real campaign.

## Source-of-Truth Priority

Pregnancy timing must be obtained in this order:

1. Bannerlord's active pregnancy record, including its start and expected completion/due time when exposed.
2. The active Bannerlord pregnancy model's duration combined with the pregnancy's recorded start time.
3. A compatible runtime value exposed by another active pregnancy-duration mod.

Never use a fixed number of campaign days compiled into Pregnant Lords Expanded.

If Bannerlord reports that a hero is pregnant but no valid timing information can be obtained, the result is **Pregnant — Progress Unknown**. The mod must not invent a month or trigger a month-based withdrawal from an unreliable value. It should record a diagnostic warning and check again later.

## Normalization

Given valid start, due, and current campaign times:

```text
totalDuration = dueTime - startTime
elapsed       = currentTime - startTime
progress      = clamp(elapsed / totalDuration, 0.0, 1.0)
```

The internal progress value is a fraction from `0.0` through `1.0`.

The initial month rule is:

```text
month = clamp(floor(progress * 9) + 1, 1, 9)
```

This produces month 1 at the beginning of pregnancy and month 9 during the final ninth of the active duration. At exactly 50% progress, the result is month 5.

Campaign restrictions use the normalized month, not raw Bannerlord days. For example, a withdrawal threshold of month 3 begins when the calculated month first becomes 3, regardless of whether the active pregnancy lasts 36, 72, or another number of campaign days.

## Required Result

The pregnancy-progress service should return a result equivalent to:

| Field | Meaning |
|---|---|
| `IsPregnant` | Bannerlord currently identifies the hero as pregnant |
| `HasKnownProgress` | Valid start and duration/due-time data were found |
| `Progress` | Clamped value from 0.0 through 1.0 |
| `ApproximateMonth` | Integer from 1 through 9 when progress is known |
| `ExpectedDueTime` | Bannerlord-derived due time when available |
| `DataSource` | Which supported Bannerlord/runtime source produced the timing |
| `FailureReason` | Diagnostic reason when progress cannot be calculated safely |

This object is computed from the active campaign state. Pregnant Lords Expanded should not maintain a second competing pregnancy timeline.

## Eligibility

The service applies to pregnant:

- Female lords
- Female companions
- Female wanderers
- Other eligible female heroes represented by Bannerlord's pregnancy system
- The female player character

The player character is not excluded. Later milestones may provide separate MCM behavior for player-controlled and AI-controlled heroes.

## Safety and Edge Cases

- A non-pregnant hero returns `IsPregnant = false` and no month.
- Null, missing, zero-length, negative, or reversed timing data must not cause a crash.
- Progress before the recorded start clamps to 0.0.
- Progress after the expected due time clamps to 1.0 until Bannerlord completes the birth state transition.
- Save/load must produce the same result from the same active pregnancy data.
- A pregnancy-duration mod changing the active duration must change the calculation proportionally without requiring a Pregnant Lords Expanded patch.
- Birth, miscarriage, maternal death, or another native pregnancy-ending event immediately makes the previous progress result obsolete.
- Diagnostic messages should be written to a log and should not repeatedly spam the player.
- After a campaign finishes loading, one on-screen message confirms that the Milestone 1
  pregnancy-observation behavior is active. This message does not claim that a pregnancy was found.
- A Bannerlord birth event should record the mother's name, reported child count, and stillborn count.
- If a tracked pregnancy becomes inactive without a birth event, record a separate diagnostic so
  miscarriage, maternal death, or a pregnancy ended by another mod is not silently mistaken for birth.

## Acceptance Tests

Milestone 1 is complete only when all of the following are demonstrated:

1. A newly pregnant eligible hero is detected.
2. A non-pregnant hero is not falsely detected.
3. Progress advances across campaign time.
4. Exactly 50% progress reports approximately month 5.
5. The same percentage produces the same month with two different pregnancy durations.
6. Months never fall below 1 or above 9.
7. Invalid timing data fails safely without triggering withdrawal.
8. The female player character follows the same pregnancy detection rules.
9. Saving and reloading does not change the calculated month unexpectedly.
10. A completed or ended pregnancy no longer returns an active pregnancy month and records the
    appropriate birth or non-birth diagnostic.
11. Loading a campaign displays the one-time Milestone 1 activation message.

## Milestone Boundary

This milestone does **not** yet:

- Withdraw anyone from campaigning
- Select a destination
- Teleport a hero
- Create an In Transit or Resting state
- Change party leadership
- Select or transfer escort troops
- Add combat pregnancy-loss risks
- Add dialogue or relationship effects

Those systems depend on this milestone and will be added only after its calculations are validated in Bannerlord 1.5.2 gameplay.
