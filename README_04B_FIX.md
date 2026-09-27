# Build 04b compile fix

This overlay corrects the three compile errors reported after Build 04a.

- `NativePregnancyLossService.cs`: fully qualifies `TaleWorlds.CampaignSystem.Campaign.Current` to avoid collision with the mod namespace `PregnantLordsExpanded.Campaign`.
- `PregnancyProgressBehavior.cs`: imports `TaleWorlds.Core` so Bannerlord 1.5.3 `MBRandom` resolves correctly.

No gameplay balance or combat-pregnancy-loss logic changed. Locked defaults remain 5% / 15% / 35%.
