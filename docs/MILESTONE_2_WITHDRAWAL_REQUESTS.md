# Milestone 2 — Withdrawal Requests, Authority, and Responsibility

## Status and Boundary

- Current game target: **Mount & Blade II: Bannerlord 1.5.3 beta**
  (live-tested on build `1.5.3.122374`)
- Milestone 1 is preserved at tag `v0.1.0-milestone1`.
- Milestones 2A through 2C began as and completed a **diagnostic-only implementation**.
- Milestone 2D-A activates commander-denial relationship consequences.
- Milestone 2D-B adds save-safe player decision prompts and has passed its first live branches.
- Milestone 2D-C revises ordinary denial to a one-time minor consequence and adds pure,
  automated graduated battle-risk calculations. No battle hook or loss roll is active yet.
- This milestone decides when a withdrawal request is due, who has authority to answer it,
  what that answer means, and who is responsible for continued campaigning.
- It does not yet remove a hero from a party, relocate her, add pregnancy loss risk, create an
  escort quest, or enforce postpartum recovery.

The diagnostic boundary is deliberate. Authority and responsibility must be proven in live
campaigns before the mod changes campaign state.

## Design Principle

Pregnancy does not remove a hero's agency or martial ability. It creates a temporary duty to
protect both the mother and the child she carries. A pregnant hero may still fight in immediate
self-defense or in defense of the settlement where she is resting.

Responsibility follows the final decision:

| Responsibility | Meaning |
|---|---|
| `VoluntaryRefusal` | The pregnant hero elects to continue campaigning when the decision is hers. |
| `CommanderOverride` | She requests withdrawal, but the person with military authority orders her to remain. |
| `ForcedCircumstances` | Immediate conditions make safe withdrawal impossible; no one is automatically blamed. |
| `WithdrawalApproved` | The responsible authority approves withdrawal. Later milestones perform the actual departure. |

The system must not assign `CommanderOverride` merely because a hero is present in an army. It
must establish that she requested withdrawal and that the correct authority refused it.

## Protected Rest and Later Departure

Beginning at the warning month, a pregnant hero who is not campaigning or imprisoned and resides
in a settlement is recorded as being in protected pregnancy rest. This is an observational state;
Milestone 2C does not lock her inside the settlement or move her party.

If she later leaves that protection and resumes ordinary campaigning, the departure is
provisionally recorded as `VoluntaryRefusal` with the mother as the responsible hero. At month 4
or later, that transition causes an immediate withdrawal petition even if a normal petition was
already processed earlier in the same normalized month. This exception is necessary because
returning to the field after reaching safety is a new decision, not a duplicate daily tick.

Responsibility may then change prospectively:

- If the responsible commander orders her to remain, the command decision is recorded as
  `CommanderOverride`.
- If withdrawal is approved, the later active milestone must carry out or schedule the return to
  protection.
- If she is her own authority and continues, responsibility remains `VoluntaryRefusal`.
- If she is captured or disappears from protection without a trustworthy campaigning state, the
  diagnostic records `ForcedCircumstances` and assigns no voluntary blame.

A pregnant hero may fight in defense of the same settlement where she was resting. While that
settlement remains under siege, local defensive mobilization preserves her protected status and
does not create a withdrawal petition or blame. If she subsequently leaves the settlement and
continues campaigning after the defense ends, that later transition is evaluated normally.

On the first observation of an existing pregnancy, the system establishes a baseline rather than
inventing a prior departure. This avoids false blame when adding Milestone 2C to an older save.

## Normalized Month Schedule

All stages use the normalized 1–9 month produced by the Milestone 1 pregnancy-progress service.
No raw or hard-coded pregnancy-day count may be used.

| Normalized month | Default stage |
|---:|---|
| 1–2 | Normal campaigning; no withdrawal event. |
| 3 | Advance warning and preparation. |
| 4 | Formal withdrawal petition. |
| 5 | Urgent renewed petition. |
| 6 | Serious renewed petition. |
| 7 | Grave renewed petition. |
| 8 | Extreme renewed petition. |
| 9 | Birth-imminent petition. |

The month 3 warning informs both the pregnant hero and her current military authority. It creates
no relationship liability and requires no decision; its purpose is to allow time to prepare a
replacement and safe withdrawal. If command changes before month 4, the formal petition is made
to the new authority.

Each warning or petition is processed no more than once per pregnancy month. If a pregnancy
duration mod causes progress to skip a normalized month, the system processes the current stage
once; it does not replay every missed petition in a single tick.

If Bannerlord reports **Pregnant — Progress Unknown**, no month-based warning or petition occurs.
The mod waits for trustworthy timing data rather than guessing.

## Authority Resolution

Authority is resolved from the hero's current campaign state when a petition becomes due.

| Pregnant hero's state | Decision authority |
|---|---|
| Member of an army led by another hero | The army leader. |
| Leader of that army | Herself. |
| Independent party leader who is not her clan leader | Her clan leader by default; see the configurable independent-authority rule below. |
| Independent party leader who is also her clan leader | Herself. |
| Member of another mobile party | That party's leader. |
| Resting in a settlement and not campaigning | No petition is required. |
| Prisoner | Captivity rules handle the case in a later milestone. |
| No valid party or authority can be resolved | Fail safely, log once, and retry when state changes. |

The resolver must use native Bannerlord party and army ownership. It must not infer authority from
clan rank when a different hero actually commands the army.

### Independent Withdrawal Authority

When a pregnant hero campaigns independently, the default political authority is her clan leader,
not the ruler of the entire kingdom. This keeps the decision close to the noble house responsible
for its own members while avoiding kingdom-wide petitions from every independent party.

A later optional MCM setting may expose three modes:

| Setting | Effect |
|---|---|
| `ClanLeader` (default) | A non-clan-leader petitions her clan leader. A clan leader decides for herself. |
| `KingdomRuler` | A kingdom member petitions the faction ruler. Clan leaders without a kingdom decide for themselves. |
| `Self` | Every independent party leader decides for herself. |

Army and party command always take priority over this setting. A hero serving under another
commander petitions that actual commander rather than a distant political authority.

### Player Cases

- If the player commands the army or party, the player receives the decision prompt in a later
  activation phase.
- If the pregnant player serves under an AI commander, the AI may approve or deny the request,
  but the player must retain the final ability to leave the army.
- If the pregnant player commands independently, continuing is a player choice and cannot be
  attributed to a fictional superior.

Whether an AI-controlled pregnant hero can disobey a denied order is intentionally left for a
later decision. Milestone 2A records the commander's answer without forcing either outcome.

## Decision Outcomes

The diagnostic evaluator returns one explicit outcome:

| Outcome | Diagnostic interpretation |
|---|---|
| `Approve` | Withdrawal is authorized. |
| `Deny` | Withdrawal is refused by the resolved commander; responsibility is `CommanderOverride`. |
| `ContinueVoluntarily` | The pregnant hero is her own authority and elects to remain; responsibility is `VoluntaryRefusal`. |
| `ForcedDelay` | A safe departure is temporarily impossible; responsibility is `ForcedCircumstances`. |
| `NoDecision` | The hero is not campaigning, is a prisoner, progress is unknown, or authority cannot be resolved. |

A future active implementation may allow a short emergency delay during an immediate battle or
siege. It must not silently turn a temporary delay into a permanent denial.

## AI Decision Factors

AI decisions use a bounded, explainable score. They must not require a general-purpose AI model
or perform expensive searches.

Factors favoring approval include:

- Later normalized pregnancy month
- High Mercy or Honor
- Strong relationship with the mother, spouse, or her clan
- Availability of a replacement leader
- Nearby friendly protection
- High dynastic or succession risk

Factors that may favor refusal or delay include:

- Cruel or strongly martial personality
- Immediate battle, siege, escape, or encirclement
- No viable replacement during a critical operation
- Severe military emergency

`ForcedCircumstances` is reserved for a genuine inability to depart safely. Strategic
inconvenience alone is not sufficient to erase commander responsibility.

Pregnancy month must become increasingly influential. By months 8 and 9, denial should be rare
except for extreme personalities or circumstances.

Any random component is rolled once for that month's decision and stored in the request ledger.
Loading a save must not reroll an already resolved decision. The diagnostic log should preserve
the major positive and negative score factors so surprising AI behavior can be explained.

### Milestone 2B Diagnostic Score

The first live diagnostic uses no random component. This makes every result reproducible while
we inspect the score balance in real campaigns.

| Factor | Diagnostic contribution |
|---|---:|
| Month 4 / 5 / 6 / 7 / 8 / 9 | −20 / 0 / +20 / +40 / +60 / +80 |
| Mercy level | 12 points per level, clamped to −2 through +2 |
| Honor level | 12 points per level, clamped to −2 through +2 |
| Valor level | −8 points per level, clamped to −2 through +2 |
| Calculating level | 5 points per level, clamped to −2 through +2 |
| Existing relation with the mother | Relation divided by 10, clamped to −10 through +10 |
| Viable replacement available | +10 |
| Nearby settlement protection | +10 |
| Mother is her clan leader | +10 dynastic-risk weight |
| Active siege or comparable military emergency | −40 |

A score of zero or more recommends approval. A negative score recommends denial by an external
authority or voluntary continuation when the mother is her own authority. This formula is
provisional: Milestone 2B logs every component so live evidence can guide tuning before any
decision changes gameplay.

## Active Commander Relationship Liability

Milestone 2A calculated and logged liability without changing relationships. Milestone 2D applies
a relationship consequence when a commander denies a withdrawal petition. Live testing showed
that escalating the penalty merely because another month passed was too severe. Milestone 2D-C
therefore uses a one-time minor target of −5 for an ordinary denial in months 4–9.

Renewed petitions still occur each month and become narratively more urgent, but they do not add
another routine penalty for the same commander and pregnancy. Stronger consequences require an
actual later event, such as a significant wound or attributable pregnancy loss.

The liability record is keyed by pregnancy and responsible hero. If command changes, each
commander retains responsibility for the decisions that commander personally made. A new
commander's first denial uses the current pregnancy month's target severity.

A healthy birth does not automatically erase accumulated resentment. Catastrophic outcomes such
as child loss, maternal death, or deliberate execution belong to later consequence milestones and
may increase relationships toward −100 or create Blood Debt.

The relationship mutation uses Bannerlord's native `ChangeRelationAction` and suppresses the
normal quick notification for AI-versus-AI events. Consequently, the result follows the active
effective-relation model. Vanilla Bannerlord may resolve some opinions through clan leaders; an
optional mod such as True Noble Opinion may instead retain an individual noble-to-noble result.
Pregnant Lords Expanded does not require or patch either behavior.

The previously saved diagnostic-liability ledger remains separate from the Milestone 2D ledger
that records penalties actually applied. This is essential for upgrades: a denial recorded by an
older diagnostic build must not falsely suppress the first real relationship consequence. The
applied ledger is saved and prevents the same target from being charged again after save/load.
If an older active build already applied more than −5, Milestone 2D-C never attempts to reverse
or add to that historical relationship change.

## Graduated Post-Battle Pregnancy Risk

Milestone 2D-C provides a pure calculator and automated boundary tests for later campaign use.
It performs no daily roll. A future battle hook will evaluate a pregnant hero once after a
completed battle, only when her health declined during that battle.

| Health after battle | Injury class | Pregnancy-loss chance | Commander relation target when responsible |
|---:|---|---:|---:|
| 75%–100% | None/minor | 0% | 0 |
| 50%–74% | Significant | 10% | −10 |
| 25%–49% | Severe | 30% | −25 |
| Below 25% | Critical | 50% | −35 |

The commander target applies only when a tracked `CommanderOverride` kept her in the field.
When the mother continued voluntarily, the physical risk is the same but no commander is blamed.
A battle identity must be persisted before enabling the hook so save/load cannot reroll the same
outcome.

## Attributable Child-Loss Family Reactions

These reactions belong to a later consequence milestone; Milestone 2 records enough responsibility
data to support them without applying them yet. They occur only when the child's loss is causally
attributed to continued campaigning, severe injury, harsh captivity, or another tracked action.
A natural or medically unrelated stillbirth does not automatically create blame.

The penalty is applied between each affected relative and the hero who is responsible:

| Affected relative | Default relation change | MCM range |
|---|---:|---:|
| Pregnant mother, when another hero is responsible | −75 | −100 to 0 |
| Husband or other recorded parent of the child | −50 | −100 to 0 |
| Each living parent of the pregnant mother | −10 | −100 to 0 |
| Each living adult sibling of the pregnant mother | −5 | −100 to 0 |

Responsibility controls the target:

- If a commander denied withdrawal and ordered her to remain, the family reactions target that
  commander.
- If withdrawal was approved but the mother voluntarily refused to leave, the family reactions
  target the mother. No mother-to-self relation change is attempted.
- If safe departure was genuinely impossible, the event remains `ForcedCircumstances` unless a
  later action establishes a responsible captor or other hero.

Each setting is an independent integer slider. Values are displayed as actual relation changes
from −100 through 0, where 0 disables that role's reaction. Bannerlord's native relation limits
still apply.

The family-reaction event is applied at most once per pregnancy loss. Heroes are deduplicated by
stable identity before penalties are applied, so one person cannot be counted twice because that
person occupies more than one family role. Existing commander-withdrawal resentment and the
catastrophic family reaction are separate consequences, but each has its own one-time ledger and
must never be repeatedly applied on daily ticks or save/load.

## State and Save/Load Contract

Milestone 2 stores an event ledger, not a second pregnancy timeline. The pregnancy month and due
date continue to come from Bannerlord through Milestone 1.

Each active request state should retain only what is necessary to prevent duplication and assign
responsibility:

- Mother's stable hero identifier
- Pregnancy identity derived from the active pregnancy record/start time
- Highest warning month processed
- Petition months already processed
- Authority resolved for each decision
- Decision outcome and responsibility classification
- Target liability recorded for each responsible commander
- Cumulative commander relationship penalty actually applied
- Responsible hero for any later attributable pregnancy loss
- Stored AI decision roll or resolved outcome for each processed petition
- Whether the request state has ended
- Last protected-rest state and protected settlement
- A departure sequence so multiple genuine rest-to-field transitions in one month remain distinct

The ledger must be synchronized through Bannerlord's campaign save system. On load, the same
month must not generate the same warning or petition again.

A birth, non-birth pregnancy ending, maternal death, or invalidated pregnancy record closes the
active request state. A later pregnancy starts a new state and must not inherit prior monthly
petition flags.

## Notification and Logging Rules

Milestone 2 favors diagnostics over player-facing interruptions.

- AI-versus-AI decisions are logged and do not produce repeated global notifications.
- Events involving the player may display one concise message.
- Repeated daily logs for an unchanged state are prohibited.
- Every decision log identifies the mother, normalized month, resolved authority, outcome,
  responsibility type, target liability, relationship delta applied, and applied cumulative
  relationship penalty.

Example diagnostic:

```text
[PregnantLordsExpanded] Areliana entered normalized month 4.
[PregnantLordsExpanded] Withdrawal authority: Niphon (army commander).
[PregnantLordsExpanded] Areliana requested withdrawal; Niphon denied the request.
[PregnantLordsExpanded] Responsibility: CommanderOverride; target liability: 25.
```

## Implementation Phases

### Milestone 2A — Pure Calculations

- Define settings and default schedule.
- Define authority, decision, responsibility, and liability result types.
- Implement month-stage and liability calculations independent of Bannerlord where practical.
- Add automated boundary and deduplication tests.

### Milestone 2B — Campaign Diagnostics

- Resolve native party and army authority.
- Persist request ledgers with `SyncData`.
- Log warnings, petitions, decisions, and cleanup.
- Perform no party movement or relationship mutation.

### Milestone 2C — Protected-Rest and Departure Diagnostics

- Observe protected settlement rest daily without repeating stable-state logs.
- Detect rest-to-field transitions even when the normalized month does not change.
- Attribute an ordinary departure provisionally to the mother.
- Preserve protected status during defense of the same besieged settlement.
- Treat capture and unresolved forced removal separately from voluntary campaigning.
- Permit an immediate month 4+ petition for each genuine departure event.
- Perform no party movement, settlement lock, relationship mutation, or pregnancy-risk roll.

### Milestone 2D — Activated Decisions

Only after 2C passes live tests:

- **2D-A:** Apply AI commander-denial relationship deltas exactly once through the native
  relationship action while preserving the diagnostic trail.
- **2D-B:** Add player decision prompts and explicit player agency.
- **2D-C:** Recalibrate ordinary denial consequences and add pure graduated battle-risk
  calculations with automated boundary tests.
- **2D-D:** Allow approved decisions to authorize later physical withdrawal actions.

The current implementation includes 2D-C. It presents explicit choices instead of choosing on
behalf of the player, uses the revised one-time minor denial consequence, and exposes the tested
risk calculation without invoking it in a campaign. It does not yet move a party, force a mother
home, or end a pregnancy.

Player decisions use these rules:

- An NPC petitioning a player commander can be approved or denied by the player.
- A pregnant player under an AI commander sees that commander's decision but retains the final
  choice to withdraw or remain.
- A pregnant player who is her own authority chooses withdrawal or voluntary continuation.
- An AI commander's denial causes its relationship consequence even if the player withdraws
  anyway, because the harmful order was still issued.
- Withdrawing despite a denial records final responsibility as `WithdrawalApproved`; the
  commander is not blamed for a later field loss that occurs after an authorized departure.
- Player responses are saved per request, and a pending response can be presented again after
  loading without repeating a completed choice or relationship penalty.

### Milestone 2D-A Validation Evidence

Automated calculations and two live sessions passed on Bannerlord `1.5.3.122374`.

The values below document the superseded escalating-liability build. They remain useful proof
that native relationship mutation, per-commander ledgers, and save/load deduplication worked, but
Milestone 2D-C replaces those numerical targets with a one-time −5 ordinary-denial target.

The initial activation session produced 43 petitions: 27 approvals and 16 AI denials. All 16
denials applied a relationship consequence, every observed before/after difference matched the
requested delta, and no approval applied a relationship change. The session also demonstrated:

- Upgrade safety: an older diagnostic liability did not suppress Hvana's first real Milestone 2D
  consequence; the full current target of −35 was applied.
- Tier progression: Vitharsura's commander received −25 at month 4, then −10 at month 5, then
  −10 at month 6, reaching the cumulative −45 target without repeating an earlier tier.
- Authority changes: when Brighan later served under a different commander, the new commander's
  first denial received that month's full cumulative target rather than inheriting another
  authority's ledger.
- Pregnancy isolation: a later pregnancy created a new liability state only after the earlier
  pregnancy had closed in birth.

The reload session restored the applied-penalty ledger without replaying any of the 16 earlier
changes. Only newly reached denial tiers applied: Popilia received the next −10 delta and Brighan
received −20 to advance an existing commander from −45 to the month-8 target of −65. The resulting
relations again matched the requested changes exactly. Both sessions saved and exited cleanly,
and neither produced a Pregnant Lords Expanded exception.

### Milestone 2D-B Initial Player Validation Evidence

The player-command branch was exercised from a save immediately before the month-4 petition.
The denial choice recorded the player's answer and applied the then-current relationship change.
A second save at month 5 proved that a renewed petition appeared and could be approved without a
relationship change. Those saves are retained as matched branch-test points for Milestone 2D-C.

## Acceptance Tests

Milestone 2 diagnostics are complete only when all of the following are demonstrated:

1. Months 1–2 produce no withdrawal event.
2. Month 3 produces one warning.
3. Month 4 produces one formal petition.
4. Months 5–9 produce no more than one renewed petition per month.
5. An army member petitions the actual army commander.
6. An army commander is identified as her own authority.
7. A party member outside an army petitions the correct party leader.
8. A resting hero produces no unnecessary petition.
9. A prisoner is deferred to the later captivity system.
10. Unknown pregnancy progress produces no month-based action.
11. Save/load does not repeat a processed warning or petition.
12. Changing commanders assigns future decisions to the new commander without rewriting the old
    commander's responsibility.
13. Leaving or joining an army causes authority to be recalculated safely.
14. Birth or another pregnancy-ending event closes the request state.
15. A later pregnancy begins with a clean monthly ledger.
16. The player is included without surrendering final player agency.
17. No party, travel, combat-risk, fertility, or birth behavior changes during the diagnostic
    phase.
18. No Pregnant Lords Expanded exceptions appear in campaign logs.
19. An independent non-clan-leader resolves to the configured clan, kingdom, or self authority.
20. Family-reaction calculations use the configured values, skip self-relationships, deduplicate
    overlapping roles, and produce no mutation during the diagnostic phase.
21. Protected rest is established once and does not generate daily log spam.
22. Leaving protected rest for ordinary campaigning records provisional `VoluntaryRefusal`.
23. A month 4+ rest-to-field transition creates an immediate petition even if that month had an
    earlier petition before the hero rested.
24. Defense of the protected settlement creates no voluntary blame or withdrawal petition.
25. Continued settlement defense does not repeat its transition log after save/load.
26. Capture or an unresolved removal from protected rest creates no voluntary blame.
27. An AI commander's first denial applies the one-time −5 ordinary-denial target.
28. A later routine denial by that commander in the same pregnancy applies no additional penalty.
29. Save/load does not repeat a relationship penalty already applied.
30. A diagnostic liability saved by an older build does not suppress the first active Milestone
    2D relationship consequence.
31. Approvals, self-authorized voluntary continuation, and forced circumstances apply no
    commander relationship penalty.
32. A player commander can approve or deny an NPC petition without the mod choosing for the
    player.
33. A player commander's denial applies the same one-time relationship rule as an AI denial.
34. A pregnant player retains the final choice to withdraw or continue after an AI decision.
35. Withdrawing despite an AI denial records approval as the final outcome while applying the
    relationship consequence for the denial itself.
36. Continuing after an approval records `VoluntaryRefusal`; remaining under a denied order
    records `CommanderOverride`.
37. Completed player responses and their relationship effects do not repeat after save/load.
38. Pure battle-risk calculations use exact 75%, 50%, and 25% health boundaries and roll no risk
    when battle health did not decline.
39. Voluntary continuation preserves physical injury risk but assigns no commander relationship
    target.

## Explicitly Deferred Features

The following are designed separately after withdrawal authority is proven:

- Removing or replacing party leaders
- Actual withdrawal and destination selection
- Native Traveling state and narrative clan handoff
- Player escort quest and AI simulated escort
- Chivalric Mercy and Maternal Safe Conduct for captives
- Combat- and captivity-related pregnancy loss
- Postpartum recovery
- Applying attributable child-loss family reactions calculated from recorded responsibility
- Blood Debt, blood money, and AI-versus-AI feud persistence
- Optional MCM module
