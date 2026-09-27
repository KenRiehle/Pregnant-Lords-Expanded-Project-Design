# Build 04A compile fix

Build 04A is Build 04 plus one compile correction in `PregnancyProgressBehavior.cs`:

```csharp
using PregnantLordsExpanded.Withdrawal;
```

The Build 04 combat-pregnancy-loss types (`CombatPregnancyLossSettings`,
`CombatPregnancyLossCalculator`, `CombatPregnancyNativeProtection`, and
`WithdrawalResponsibility`) live in the `PregnantLordsExpanded.Withdrawal`
namespace. Build 04 omitted that import from `PregnancyProgressBehavior.cs`,
which caused CS0246 at the native-protection return type.

No gameplay logic or locked 5% / 15% / 35% values are changed by this fix.
