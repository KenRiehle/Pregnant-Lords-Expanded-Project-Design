# Milestone 2D-F-C Build 04e3 — Natural Petition Wording + Pregnancy Death Record Cleanup

Build 04e3 is a cleanup/refinement overlay on top of live-validated 04e2.

## 1. Natural player-facing pregnancy wording

The internal term `normalized pregnancy month` remains unchanged in diagnostics and save/deduplication logic, but it no longer appears in the prisoner petition UI.

Player-facing wording is now:

- Month 1: `She is in the first month of her pregnancy.`
- Months 2-8: `She is two/three/.../eight months pregnant.`
- Month 9: `She is in the final month of her pregnancy.`

The petition consequence text is now:

`Refusing her request will reduce her relation with you by 2. If she remains imprisoned, she may petition again next month.`

The refusal tooltip likewise says `this pregnancy month`, while diagnostic logs continue to use normalized-month terminology for technical verification.

## 2. Death/execution while pregnant

Bannerlord 1.5.3 removes the native pregnancy ledger entry when a pregnant hero dies, but the tested execution path left `Hero.IsPregnant` true. That caused a deceased noblewoman to continue showing the active `Pregnant` status in the Encyclopedia.

Build 04e3 now:

- captures whether the woman was actively pregnant at the moment the `HeroKilled` event is handled;
- preserves that fact in her persistent `Hero.EncyclopediaText` obituary;
- clears the active `Hero.IsPregnant` flag after the historical text is recorded;
- leaves Bannerlord's normal death/execution relation, trait, blood-feud, and other consequence handling untouched;
- leaves the older `became pregnant` history event intact as historical information.

Examples of appended historical wording:

- Execution: `Sukayna was executed while with child by Ragnar.`
- Murder: `<Name> was murdered while with child by <killer>.`
- Battle death: `<Name> was killed in battle while with child.`
- Battle wounds: `<Name> died from battle wounds while with child.`

`DiedInLabor` is not given a redundant `while with child` sentence because Bannerlord already supplies childbirth-specific obituary wording.

## Diagnostics

Pregnancy-at-death cleanup logs:

`PLE maternal-death pregnancy cleanup: mother=<name>, detail=<detail>, killer=<name>, historicalRecordPreserved=True, activePregnancyCleared=True.`

## Live test

1. Overlay 04e3 and rebuild.
2. Confirm **0 compile errors**.
3. Load the prisoner test save and wait for a petition:
   - confirm no player-facing `normalized pregnancy month` wording remains;
   - confirm the mechanical -2 refusal behavior is unchanged.
4. Reload a clean save with a pregnant prisoner and execute her while she is still pregnant.
5. Open her Encyclopedia page:
   - the active `Pregnant` badge should be gone;
   - the obituary should contain the `while with child` sentence;
   - Bannerlord's ordinary execution history/consequences should still be present.
6. Check the PLE log for the maternal-death cleanup line above.

Do not commit until compile and live behavior are validated.
