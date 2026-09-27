# Milestone 2D-F-C — Elite Escort Execution and Surplus Troop Handoff

Baseline: Milestone 2D-F-B (locked, tested in Bannerlord, committed, and pushed).

This overlay turns the staged Strong / Lean / Minimal escort choices into live campaign
execution while preserving the proven 2D-E withdrawal lifecycle.

## Live execution

For an NPC mother who leads her own party:

- Strong retains the best 50 ordinary troops available.
- Lean retains the best 35 ordinary troops available.
- Minimal retains the best 5 ordinary troops available.
- Heroes are not counted as escort troops and are not transferred by the escort allocator.
- Tier descending, then troop level descending, remains the elite-retention rule from 2D-F-A.
- Surplus transfer runs in the reverse priority direction: lower-tier / lower-level surplus moves first.
- The mother is never given troops that did not already exist.

## Recipient rules

- Only other active parties in the same army are eligible recipients.
- The withdrawing mother's own party is never a recipient.
- A recipient can receive troops only up to its current legal party-size capacity.
- Recipients are ordered by available capacity descending.
- Equal-capacity ties use ordinal party id for deterministic save/reload behavior.
- The player party receives no hidden priority; it is selected naturally when it has the most capacity.
- If total recipient capacity is smaller than the surplus, the untransferable remainder stays with the mother.
- No troop is deleted because recipient capacity is insufficient.

Conservation target:

`starting ordinary troops = departure ordinary troops + troops transferred to recipients`

and

`departure ordinary troops = elite escort retained + untransferable surplus`

## Wounded troops and XP

Live roster mutation preserves troop counts, wounded counts, and stack XP. When a stack is split,
wounded soldiers are handed off before healthy soldiers of the same troop type, allowing the mother
to retain the healthiest soldiers available inside an otherwise equal troop stack. XP is divided
proportionally and conserved as integer roster XP.

If a live mutation or post-transfer conservation check fails, 2D-F-C attempts to roll back every
mutation already made and keeps the staged withdrawal pending for a later retry.

## Relationship consequence

The chosen escort relationship effect is applied once per request and persisted separately from the
roster-execution ledger:

- Strong: +5
- Lean: 0
- Minimal: -25

A reload cannot replay the effect after it has been recorded.

**Build 02 correction:** Bannerlord's normal `ChangeRelationAction` applies positive-relation
gain modifiers, which turned the configured Strong `+5` into an observed `+10` in live testing.
Escort-choice relationship consequences now use the same effective-hero mapping and relation-change
event dispatch, but set the configured delta exactly. Strong therefore changes the effective relation
by **+5**, not +10, except when the native relationship cap prevents the full delta.
This correction is scoped to 2D-F escort-choice consequences; the existing denial relationship
path is unchanged.

## Safe execution boundary

The inquiry callback only records the choice. Relationship application, roster mutation, army
separation, and travel are deferred until the next safe pregnancy observation outside the inquiry
callback. A map-event or siege state postpones the handoff and retries later.

After a successful handoff, the existing 2D-E authorization path takes over: the mother leaves the
army and physically travels toward the protected fortification. The 2D-E native party-retirement /
garrison merge remains unchanged.

## Naval scope

This first 2D-F-C test build intentionally does **not** change the 2D-E naval routing order. The
recently identified sea-locked routing defect is tracked for the next isolated routing correction.
The first live validation target is the Strong-50 land withdrawal so troop allocation/handoff can be
proven independently from naval pathfinding.

## New persistence

- `PLE_M2DFC_RelationshipChangeByRequest`
- `PLE_M2DFC_CompletedByRequest`

The existing 2D-F-B `PLE_M2DF_PlanByRequest` ledger remains authoritative for the selected plan.
Pregnancy cleanup removes all three request-scoped ledgers.

## Calculation test

The new pure `WithdrawalEscortHandoffPlanner` verifies recipient capacity allocation, insufficient
capacity behavior, deterministic ties, and conservation without Bannerlord dependencies.

Expected final test line:

`All Milestone 2D-F-C escort handoff capacity-planning tests passed.`

## First Bannerlord test

Use the preserved Thyrsif pre-petition save and test **Strong Escort (50)** on land.

Before choosing Strong:

1. Record Thyrsif's ordinary troop count.
2. Empty enough capacity in the player's army party to accept all expected surplus.
3. Record the player's troop count and party-size limit.

After choosing Strong, allow campaign time to advance until the next pregnancy observation. Expected:

- relationship with Thyrsif increases once by **exactly +5** (unless already near the positive relationship cap);
- Thyrsif retains exactly 50 ordinary troops when recipient capacity can absorb all surplus;
- lower-tier surplus appears in eligible army recipient parties;
- the sum of Thyrsif's departure troops plus transferred troops equals her starting ordinary roster;
- Thyrsif separates from the army;
- 2D-E begins normal physical withdrawal travel;
- saving/reloading does not repeat the troop transfer or +5 relationship effect.


### Build 03 compile-only correction
- Fully qualifies `TaleWorlds.CampaignSystem.Campaign.Current` in the exact relationship-delta helper to avoid collision with the local `PregnantLordsExpanded.Campaign` namespace.
- No gameplay behavior changed from Build 02.
