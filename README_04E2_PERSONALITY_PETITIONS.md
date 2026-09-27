# Milestone 2D-F-C Build 04e2 — Personality-Driven Pregnant-Prisoner Petitions

Build 04e2 is a narrative refinement on top of the compile-clean/live-validated 04e1 monthly pregnant-prisoner petition system.

## Mechanical behavior intentionally unchanged

- One petition per separately reached normalized pregnancy month while a pregnant noblewoman remains a player-controlled prisoner.
- `Continue Imprisonment` applies exactly -2 relation once for that month.
- `Chivalric Maternal Release` continues to use Bannerlord's native `EndCaptivityAction.ApplyByReleasedByChoice` path.
- Main-party, player-clan party, and player-owned settlement dungeon captivity remain supported.
- No escort transfer/travel mechanic is added in 04e2. That remains the next isolated build after this narrative layer is validated.

## New 04e2 presentation layer

Each petition now selects a deterministic role-play style from the prisoner's native Bannerlord data:

- CharmingDiplomat — Charm plus merciful/generous tendencies.
- HonorableAppeal — Honor/Mercy/Generosity.
- CalculatingCoercive — Calculating/Roguery and low Honor.
- DefiantWarrior — Valor plus Leadership/Tactics.
- YoungVulnerable — young, low-Valor noblewomen.
- ProudNoble — fallback, strengthened for clan leaders and Leadership.

The chosen style does not change the mechanical -2 refusal consequence.

## Honorifics

The petition addresses the player using the player's displayed name. If the displayed name already begins with a recognized title (for example King, Queen, Emperor, Empress, Lord, Lady, Duke, Duchess, Jarl, Prince, Princess, Khan, Khatun, Sultan, Sultana, Emir, Count/Countess, Baron/Baroness), the displayed title/name is preserved. This allows simple compatibility with title mods that alter the visible hero name.

If no recognized title is present, the fallback is `My Lord <Name>` or `My Lady <Name>`.

## Refusal escalation

A new save-persistent refusal counter is scoped to the current captivity episode. Petition wording escalates through five tone levels:

0. courteous opening request
1. renewed/earnest request
2. firm warning
3. stern clan/reputation warning
4+. angry, threatening, insulting, or coldly formal depending on the prisoner's personality style

Release, escape, pregnancy end, or death clears the episode/refusal counter.

Build 04e1 did not persist a refusal count. When an older 04e1 save is loaded, 04e2 conservatively migrates any prisoner with an already-resolved monthly petition to at least one prior refusal; exact earlier counts cannot be reconstructed from 04e1 save data.

## Diagnostics

Queued petition logs now include:

- `style=`
- `address=`
- `priorRefusals=`
- Charm, Roguery, Leadership, Tactics
- Honor, Mercy, Generosity, Valor, Calculating
- age

Refusal-resolution logs now include `refusalCount=`.

## First live test

1. Overlay 04e2 on 04e1 and rebuild.
2. Load a save with at least two pregnant prisoners of different personalities/ages if possible.
3. Advance to the next safe hourly tick.
4. Confirm the petition contains an in-character quoted request and an appropriate `My Lord/My Lady` or visible-title address.
5. Choose Continue Imprisonment and confirm -2 relation.
6. Advance the same prisoner to a later normalized pregnancy month and confirm the wording escalates and `priorRefusals` increases.
7. Confirm no duplicate petition/penalty in the same normalized month after save/reload.

Do not commit until compile and live behavior are validated.
