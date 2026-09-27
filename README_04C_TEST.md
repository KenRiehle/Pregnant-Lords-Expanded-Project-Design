# Build 04c test overlay — Pregnancy Loss Map Notification

This overlay is based on the validated/pushed Build 04b combat pregnancy-loss checkpoint.
It does **not** change the locked 5% / 15% / 35% combat-loss probabilities.

## What changes

- Adds a player-relevant right-side Bannerlord map notification after a combat pregnancy
  loss is successfully completed.
- Uses Bannerlord's built-in `ChildBornMapNotification` with a null newborn — the same
  native/saveable notification path the base game uses for stillborn children.
- Adds responsibility-specific wording:
  - `CommanderOverride`: names/blames the recorded commander.
  - `VoluntaryRefusal`: mother accepts responsibility for remaining in service.
  - `WithdrawalApproved`: notes the loss occurred despite approved withdrawal.
  - `ForcedCircumstances` / `None`: attributes the loss to the fighting without blame.
- Suppresses unrelated world-spam unless the mother/responsible hero is in the player
  clan, the mother is the player's spouse, or the mother is in a player-led army.
- Adds a diagnostic log line when a map notice is added or suppressed.

## First live test

Use the same Thyrsif/Ragnar denial case. A successful combat pregnancy loss should produce
one right-side native map notification whose description says that Thyrsif lost her unborn
child and blames Ragnar because his order kept her in active service.

Expected log fragment:

`PLE pregnancy-loss map notice added: mother=Thyrsif, responsibility=CommanderOverride, responsibleHero=Ragnar, ...`

Do not commit/push 04c until it compiles and the notification is confirmed in game.
