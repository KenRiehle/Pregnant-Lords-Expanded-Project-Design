# Build 04e1 — Bannerlord 1.5.3 prisoner-release event signature fix

This overlay fixes the compile error from Build 04e against the installed Bannerlord 1.5.3 assemblies.

`CampaignEvents.HeroPrisonerReleased` in this game build expects a five-argument listener:

- `Hero prisoner`
- `PartyBase party`
- `IFaction capturerFaction`
- `EndCaptivityDetail detail`
- `bool showNotification`

04e used the older four-argument handler signature. 04e1 adds the fifth `showNotification` argument and intentionally does not otherwise change the monthly pregnant-prisoner petition behavior.
