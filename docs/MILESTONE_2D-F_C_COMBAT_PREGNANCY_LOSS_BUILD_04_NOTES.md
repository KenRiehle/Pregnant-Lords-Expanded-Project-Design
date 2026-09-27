# Pregnant Lords Expanded — Milestone 2D-F-C Combat Pregnancy Loss — Build 04

## Purpose

Build 04 adds the first live campaign hook for combat-induced pregnancy loss without changing the validated 2D-F-C escort handoff mechanics.

This is a mechanics/test build. The MCM presentation layer is intentionally deferred until the campaign behavior is validated in game.

## Locked defaults

- Enable Combat Pregnancy Loss: ON
- Respect Bannerlord Battle-Death Settings: ON
- Normalized months 3–6: 5%
- Normalized months 7–8: 15%
- Normalized month 9: 35%
- Months 1–2: no combat pregnancy-loss roll
- A roll requires a qualifying combat wound; battle participation by itself does not roll.
- Bannerlord Birth and Aging OFF is a hard stop.

## Native difficulty integration

When `Respect Bannerlord Battle-Death Settings` is ON:

- Hero Battle Death = Disable Battle Death For All Heroes -> PLE combat pregnancy loss is suppressed for every hero.
- Hero Battle Death = Disable Battle Death For Player Hero -> PLE combat pregnancy loss is suppressed for the Main Hero only.
- Player-clan mother + Clan Member Battle Death = Disable -> PLE combat pregnancy loss is suppressed for that mother.
- Player-clan mother + Clan Member Battle Death = Reduced by 50% -> PLE risk is halved.
- Realistic -> full configured PLE risk.

When `Respect Bannerlord Battle-Death Settings` is OFF, PLE uses its configured stage risk even if Bannerlord protects the mother from battle death. Birth and Aging remains a hard dependency either way.

## Campaign hook

Bannerlord's `CampaignEvents.HeroWounded` is used as the qualifying injury signal. Native `Hero.IsWounded` begins at 20 HP or lower. PLE stages one pending evaluation per mother/map event and resolves it from `CampaignEvents.MapEventEnded`, avoiding native pregnancy-ledger mutation inside the casualty callback.

The diagnostic log records:

- mother
- normalized month
- battle type
- base risk
- effective risk
- PLE master toggle
- respect-native toggle
- Birth and Aging state
- Bannerlord Hero Battle Death setting
- Bannerlord Clan Member Battle Death setting
- derived native protection
- withdrawal responsibility
- random roll and result

## Native pregnancy cleanup

A successful PLE loss removes the matching entry from Bannerlord's private `PregnancyCampaignBehavior._heroPregnancies` collection by reflection and only then clears `Hero.IsPregnant`.

If the native record cannot be located or removed safely, PLE does not clear the pregnancy flag. This prevents a ghost native due-date record from later delivering a child after PLE claimed the pregnancy had ended.

## Responsibility

The physical loss probability and accountability remain separate. Build 04 queries the existing persisted withdrawal responsibility ledger. Examples:

- `CommanderOverride` when the player/commander denied withdrawal.
- `VoluntaryRefusal` when the pregnant hero chose to continue service herself.
- `WithdrawalApproved` when combat occurs after an approved withdrawal.

This build logs the responsibility and closes the active PLE withdrawal diagnostic state after a successful combat pregnancy loss. Additional family/relationship consequences remain a later consequence pass.

## Pretest baseline

The 1.5.3 pretest confirmed the intended trigger scenario: Thyrsif can be ordered to remain in service, enter a real battle, survive, and finish at 1% HP. This is below Bannerlord's native wounded threshold and is the condition Build 04 now hooks.

The alternate pretest screenshot using `Reduced by 50%` plus `Disable Battle Death For Player Hero` does **not** fully protect Thyrsif: the latter setting protects the Main Hero only. Thyrsif therefore receives the player-clan 50% reduction when native settings are respected.

## Expected first live test

At normalized month 4 with Realistic clan-member death and battle death enabled for all heroes:

1. Deny Thyrsif's withdrawal petition so responsibility is `CommanderOverride`.
2. Put her in a real combat and get her wounded to 20 HP or below.
3. Complete the map event.
4. The log should show one staged qualifying wound and one 5% pregnancy-loss roll.
5. Most runs will report `Preserved`; a `Loss` result must remove the native pregnancy record and clear `Hero.IsPregnant`.

No forced 100% debug loss is included in this build. The first goal is to prove the native wound/event wiring and risk calculation before adding any temporary loss-forcing test aid.
