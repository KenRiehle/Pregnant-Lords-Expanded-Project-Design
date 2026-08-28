# Pregnant Lords Expanded — Project Design Brief

## Development Status

Development has begun against **Mount & Blade II: Bannerlord 1.5.2**.

The initial source tree implements the Milestone 1 foundation:

- Native pregnancy-record observation
- Active pregnancy-duration lookup
- Normalized progress and approximate month calculation
- Safe **Pregnant — Progress Unknown** results when timing cannot be trusted
- Optional provider and observation hooks for later DramaLord and AOC/FWB adapters
- One on-screen confirmation after a campaign finishes loading
- Birth and non-birth pregnancy-ending diagnostics
- No withdrawal, teleportation, party changes, combat risks, fertility changes, or birth replacement

Supporting specifications:

- [Milestone 1 — Pregnancy Detection and Normalized Progress](docs/MILESTONE_1_PREGNANCY_PROGRESS.md)
- [Optional Integration Architecture](docs/OPTIONAL_INTEGRATIONS.md)

### Building the development module

Open `PregnantLordsExpanded.sln` in Visual Studio 2022 and set the `BANNERLORD_DIR`
environment variable to the Bannerlord installation folder. A Release build copies
`PregnantLordsExpanded.dll` into the included `Module/PregnantLordsExpanded` structure.

The calculation test project is independent of Bannerlord and verifies the normalized-month
contract, including different pregnancy durations, the exact 50% boundary, clamping, and invalid
timing data.

## Project Scope

This project expands the existing **Pregnant Lords Stay Home** concept for *Mount & Blade II: Bannerlord*.

For this project, **“pregnant lords” means pregnant female lords, companions, wanderers, or other eligible female heroes**.

The mod should **not** control or replace Bannerlord's conception, fertility, pregnancy-chance, spouse, twin, or normal birth systems. It should observe and react to an NPC who is already pregnant using Bannerlord's existing pregnancy data.

## Compatibility Principle

The mod should use the pregnancy information already maintained by Bannerlord, including the active pregnancy duration/due date where available.

Do not hard-code a fixed number of Bannerlord days for pregnancy.

Pregnancy progress should be normalized to an approximate **nine-month scale** so that other mods can change pregnancy duration without breaking the campaign logic.

Example:

- 50% of the active pregnancy duration = roughly 4.5–5 months pregnant
- The dialogue and campaign rules use the normalized month rather than exposing exact Bannerlord days

This should allow compatibility, where possible, with mods that alter pregnancy duration, fertility, pregnancy chance, twins, birth mortality, or related calculations.

The expanded mod should consume the resulting pregnancy state rather than replace those systems.

## Milestone 1 — Pregnancy Campaign States

### Early Pregnancy

A pregnant female lord may remain in the field during early pregnancy.

The MCM should define a configurable **recommended withdrawal month**.

Example default:
- Month 1–2: normal campaigning
- Month 3: recommended withdrawal begins

### Withdrawal Threshold

When the configured pregnancy month is reached:
- AI-controlled pregnant lords begin returning to safety
- Player-clan pregnant heroes may follow separate configurable rules
- The mod chooses a suitable friendly destination

Preferred destination order can include:
1. Home/clan fief when appropriate
2. Friendly town
3. Friendly castle fallback
4. Other safe friendly settlement if necessary

### Simulated Travel

Teleportation should remain available for technical safety, but should not create instant gameplay travel.

Before teleporting:
1. Determine the destination
2. Estimate reasonable map travel time
3. Store an expected arrival time
4. Technically place the hero safely at the destination
5. Mark the hero **In Transit**
6. Prevent normal availability until the simulated travel time expires

If the home fief is approximately 10 campaign days away, the hero may be technically moved immediately but remains unavailable for 10 campaign days.

Possible player-facing status:
> Returning home due to pregnancy — expected arrival in approximately 10 days.

When the timer expires:
- State changes from **In Transit** to **Resting**
- The hero becomes available at the settlement
- She remains restricted from campaigning until allowed by the pregnancy/recovery rules

If the destination becomes hostile while she is in transit, the mod should reroute her to another valid friendly settlement and recalculate the remaining travel period where practical.

## Milestone 2 — Party Leadership and Escort

### Party Leader Withdrawal

If the pregnant hero commands a party, investigate and reuse Bannerlord's native behavior for:
- Removing/changing a party leader
- Assigning another eligible clan leader
- Disbanding parties when no replacement exists
- Native troop redistribution

Avoid faking the hero's death.

Prefer invoking or reproducing the normal Bannerlord leadership-transition logic.

### Escort Size

When the hero withdraws, she may take a protective escort.

Potential MCM choices:
- 25%
- 33%
- 50%
- Custom percentage

Default candidate: **33%**

### Escort Selection

Possible modes:
- Strongest troops
- Balanced strongest troops

Default candidate: **Balanced strongest troops**

Prioritize higher-tier troops while maintaining a sensible tactical mix when possible.

### Escort Transit

The escort should not instantly appear in the destination garrison.

Instead:
- Remove/store the escort roster during departure
- Treat those troops as traveling with the pregnant hero
- Keep them unavailable during the simulated travel period
- Deliver them only when the hero's arrival timer completes

This prevents troop teleportation exploits.

### Arrival and Garrison

When the hero arrives:
- Transfer escort troops into the local friendly garrison where possible
- Handle overflow safely
- Never silently delete troops

Fallbacks should use Bannerlord's native troop-transfer/disband systems wherever possible.

## Milestone 3 — Pregnancy Combat Risk

### Severe Injury Trigger

Do not roll pregnancy-loss risk for every hit.

Evaluate pregnancy complications only after a serious combat event such as:
- Knocked unconscious
- Reduced to approximately 1 HP
- Below a configurable severe-injury threshold
- Other clearly severe combat outcome if Bannerlord exposes one

Prefer **one pregnancy-risk evaluation per battle**.

### Risk by Pregnancy Month

Risk increases as pregnancy progresses.

Illustrative concept only:
- Month 1: very low
- Month 2: very low
- Month 3: 1–5%
- Month 4: increased
- Month 5: substantially increased
- Month 6: approximately 50%
- Month 7+: very high

Exact values should be configurable, with sensible minimum floors if the design retains enforced risk.

### Mother Killed in Combat

Optional rule:

**If a pregnant mother is killed, the pregnancy also ends.**

Use Bannerlord's existing pregnancy handling where possible instead of creating a competing pregnancy lifecycle.

### Postpartum Recovery

Possible configurable recovery period after childbirth:
- Immediate
- 7 days
- 14 days
- 21 days
- 30 days
- Custom

During recovery, the hero remains at a safe settlement and cannot resume normal campaign leadership.

## Milestone 4 — Prisoner Pregnancy Consequences

A pregnant lord held prisoner late in pregnancy may face increased complications.

Potential factors:
- Pregnancy month
- Captivity duration
- Whether held in a mobile party, castle, or town
- Whether she is still imprisoned when delivery occurs

Possible outcomes may include:
- Mother and child survive
- Mother survives / child does not
- Severe complications
- Other outcomes consistent with Bannerlord's existing pregnancy/birth system

### Blood Debt / Family Feud

If a child is lost while the mother is imprisoned by the player, the mother, father, or clan may attribute responsibility to the captor.

Potential consequences:
- Major relation loss with mother
- Major relation loss with father
- Clan-wide relation loss
- Honor consequences
- Persistent grievance / blood-debt state
- Future hostility or reduced willingness to reconcile

Conversely, releasing a pregnant prisoner before a dangerous late stage may provide positive relation/honor consequences.

This should be a later milestone, after the core withdrawal system is stable.

## Pregnancy Information Dialogue

### Display Philosophy

NPCs should speak in **approximate pregnancy months**, not exact Bannerlord days.

Examples:
- “I am about three months along.”
- “I am nearly six months along.”
- “I am in my eighth month now.”
- “It could be any day.”

The mod may internally know exact pregnancy progress, but dialogue should remain approximate and immersive.

### Basic Inquiry

Potential player dialogue:
> “My lady, if you do not mind my asking, how far along are you?”

The response depends on:
- Relationship
- Faction relationship
- Whether the player is spouse/father
- NPC traits
- Player traits
- Whether she previously refused
- Persuasion outcome

### Friendly Response

A friendly mother may answer warmly:
> “My lord, that is thoughtful of you to ask after my health and the child. I am about five months along now, and if all goes well, perhaps another four months before the birth.”

Possible relation reward: **+1**

### Spouse / Father Response

If the player is the father or spouse, use a warmer response:
> “You worry too much, my love, but I am glad that you do. I am about six months along now. If all goes well, we should meet our child in another three months.”

Relationship and existing marital relationship may influence tone.

### Neutral or Guarded Response

A neutral NPC may say:
> “Forgive me, my lord, but that is a rather personal question.”

Possible responses:
1. Respect her privacy
2. Explain genuine concern
3. Attempt persuasion
4. Demand an answer

### Hostile / Enemy Response

An enemy or low-relation NPC may refuse:
> “My lord, that is personal, and I see no reason why I should share it with you.”

The player may:
- Respect the refusal
- Attempt a persuasion check
- Demand the information

## Persuasion and Social Consequences

Use Bannerlord's native courtship/persuasion concepts where practical, but the pregnancy inquiry may use its own calculation if that gives better control.

Potential factors:
- Charm
- Current relationship
- Kingdom/faction hostility
- Player traits
- NPC traits
- Spouse/father status
- Whether she has already refused
- Time since last inquiry

### Risk / Reward Philosophy

Success should sometimes give a small benefit.

Failure should generally hurt more than success helps.

Illustrative relationship outcomes:

| Success chance | Success | Failure |
|---|---:|---:|
| 76–95% | Information only or +1 | -5 |
| 51–75% | Information +1 | -5 to -7 |
| 26–50% | Information +2 | -7 |
| 25% or less | **Information +5** | **-10** |

A difficult persuasion check (25% success chance or less) should feel like **big risk / big reward**.

The +5 difficult-success reward should normally be limited to once per pregnancy for that NPC.

### Forced Answer

A coercive option should provide guaranteed information but a serious social penalty.

Example:
> “I am not asking. Tell me.”

Possible result:
- Guaranteed answer
- Approximately **-10 relation**
- Possible Honor loss
- Possible movement toward harsher/crueler trait behavior if the Bannerlord trait system supports it cleanly

### Trait-Sensitive Responses

Traits should matter.

Examples:

**Honorable** — More receptive to respectful behavior; reacts strongly to coercion.

**Calculating** — Questions the player's motive and may require a more convincing argument.

**Merciful / Generous** — Potentially more forgiving of awkward but sincere inquiries.

Other traits can be incorporated after verifying Bannerlord's current trait APIs.

## Relationship Reward Cooldowns

A respectful inquiry may award **+1 relation**.

Suggested cooldown: **30 campaign days per mother**

During cooldown:
- NPC still answers if willing
- No additional +1 reward

This allows long-term relationship development without rapid dialogue farming.

A difficult persuasion +5 should have a stronger limit, such as once per pregnancy.

Negative outcomes should not necessarily share the positive cooldown; repeatedly disrespecting an NPC may continue to produce negative consequences.

## Core Development Principle

Build this project in milestones and validate each stage in real Bannerlord gameplay.

Recommended order:
1. Pregnancy progress detection
2. Normalized month calculation
3. Withdrawal threshold
4. Safe destination selection
5. Simulated transit
6. Resting state
7. Native party-leader transition
8. Escort selection/storage/arrival
9. Combat injury pregnancy risk
10. Postpartum recovery
11. Dialogue/persuasion
12. Prisoner complications and blood-debt system
13. Additional trait/social consequences

Always prefer using Bannerlord's existing campaign systems over replacing them.

Do not consider a feature finished merely because it compiles. Test campaign behavior, save/load persistence, AI behavior, edge cases, and interaction with other mods.
