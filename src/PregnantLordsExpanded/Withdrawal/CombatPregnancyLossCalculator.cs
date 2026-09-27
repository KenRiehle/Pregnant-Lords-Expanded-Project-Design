using System;

namespace PregnantLordsExpanded.Withdrawal
{
    public sealed class CombatPregnancyLossInput
    {
        public bool IsPregnant { get; set; }

        public bool QualifyingCombatWound { get; set; }

        public bool BirthAndAgingEnabled { get; set; }

        public int NormalizedMonth { get; set; }

        public CombatPregnancyNativeProtection NativeProtection { get; set; }
    }

    public sealed class CombatPregnancyLossResult
    {
        public CombatPregnancyLossResult(
            bool shouldRoll,
            double baseRiskPercent,
            double effectiveRiskPercent,
            string suppressionReason)
        {
            ShouldRoll = shouldRoll;
            BaseRiskPercent = baseRiskPercent;
            EffectiveRiskPercent = effectiveRiskPercent;
            SuppressionReason = suppressionReason ?? string.Empty;
        }

        public bool ShouldRoll { get; }

        public double BaseRiskPercent { get; }

        public double EffectiveRiskPercent { get; }

        public string SuppressionReason { get; }

        public bool IsPregnancyLossRoll(double rollPercent)
        {
            if (double.IsNaN(rollPercent) || double.IsInfinity(rollPercent)
                || rollPercent < 0.0 || rollPercent >= 100.0)
            {
                throw new ArgumentOutOfRangeException(nameof(rollPercent));
            }

            return ShouldRoll && rollPercent < EffectiveRiskPercent;
        }
    }

    /// <summary>
    /// Pure calculator for the locked PLE combat pregnancy-loss design.
    /// A roll is allowed only for a qualifying combat wound. Pregnancy stage sets the
    /// base chance (3-6 = 5%, 7-8 = 15%, 9 = 35% by default). Birth & Aging is a hard
    /// dependency. Native death protection is applied only when the PLE setting elects
    /// to respect Bannerlord battle-death settings.
    /// </summary>
    public static class CombatPregnancyLossCalculator
    {
        public static CombatPregnancyLossResult Calculate(
            CombatPregnancyLossSettings settings,
            CombatPregnancyLossInput input)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (!settings.EnableCombatPregnancyLoss)
            {
                return Suppressed("PLE combat pregnancy loss is disabled.");
            }

            if (!input.BirthAndAgingEnabled)
            {
                return Suppressed("Bannerlord Birth and Aging is disabled.");
            }

            if (!input.IsPregnant)
            {
                return Suppressed("Hero is not pregnant.");
            }

            if (!input.QualifyingCombatWound)
            {
                return Suppressed("No qualifying combat wound was recorded.");
            }

            double baseRisk = settings.GetBaseRiskPercent(input.NormalizedMonth);
            if (baseRisk <= 0.0)
            {
                return new CombatPregnancyLossResult(
                    false,
                    baseRisk,
                    0.0,
                    "Pregnancy month is outside the configured combat-loss window.");
            }

            double effectiveRisk = baseRisk;
            if (settings.RespectBannerlordBattleDeathSettings)
            {
                switch (input.NativeProtection)
                {
                    case CombatPregnancyNativeProtection.Disabled:
                        effectiveRisk = 0.0;
                        break;
                    case CombatPregnancyNativeProtection.ReducedByHalf:
                        effectiveRisk *= 0.5;
                        break;
                }
            }

            if (effectiveRisk <= 0.0)
            {
                return new CombatPregnancyLossResult(
                    false,
                    baseRisk,
                    0.0,
                    "Native Bannerlord battle-death protection suppresses the PLE roll.");
            }

            return new CombatPregnancyLossResult(
                true,
                baseRisk,
                effectiveRisk,
                string.Empty);
        }

        private static CombatPregnancyLossResult Suppressed(string reason)
        {
            return new CombatPregnancyLossResult(false, 0.0, 0.0, reason);
        }
    }
}
