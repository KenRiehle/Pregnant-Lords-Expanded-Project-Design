using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Core combat-pregnancy-loss configuration. MCM will bind to these values in a
    /// later settings pass; the mechanics use the locked defaults now.
    /// </summary>
    public sealed class CombatPregnancyLossSettings
    {
        public CombatPregnancyLossSettings(
            bool enableCombatPregnancyLoss,
            bool respectBannerlordBattleDeathSettings,
            double monthsThreeToSixRiskPercent,
            double monthsSevenToEightRiskPercent,
            double monthNineRiskPercent)
        {
            ValidatePercent(monthsThreeToSixRiskPercent, nameof(monthsThreeToSixRiskPercent));
            ValidatePercent(monthsSevenToEightRiskPercent, nameof(monthsSevenToEightRiskPercent));
            ValidatePercent(monthNineRiskPercent, nameof(monthNineRiskPercent));

            EnableCombatPregnancyLoss = enableCombatPregnancyLoss;
            RespectBannerlordBattleDeathSettings = respectBannerlordBattleDeathSettings;
            MonthsThreeToSixRiskPercent = monthsThreeToSixRiskPercent;
            MonthsSevenToEightRiskPercent = monthsSevenToEightRiskPercent;
            MonthNineRiskPercent = monthNineRiskPercent;
        }

        public bool EnableCombatPregnancyLoss { get; }

        public bool RespectBannerlordBattleDeathSettings { get; }

        public double MonthsThreeToSixRiskPercent { get; }

        public double MonthsSevenToEightRiskPercent { get; }

        public double MonthNineRiskPercent { get; }

        public static CombatPregnancyLossSettings Default =>
            new CombatPregnancyLossSettings(
                true,
                true,
                5.0,
                15.0,
                35.0);

        public double GetBaseRiskPercent(int normalizedMonth)
        {
            if (normalizedMonth >= 3 && normalizedMonth <= 6)
            {
                return MonthsThreeToSixRiskPercent;
            }

            if (normalizedMonth >= 7 && normalizedMonth <= 8)
            {
                return MonthsSevenToEightRiskPercent;
            }

            if (normalizedMonth == 9)
            {
                return MonthNineRiskPercent;
            }

            return 0.0;
        }

        private static void ValidatePercent(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)
                || value < 0.0 || value > 100.0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// Runtime bridge for the future MCM settings surface. The defaults are the locked
    /// PLE design values; MCM can bind these properties without changing the calculator.
    /// </summary>
    public static class CombatPregnancyLossRuntimeSettings
    {
        public static bool EnableCombatPregnancyLoss { get; set; } = true;

        public static bool RespectBannerlordBattleDeathSettings { get; set; } = true;

        public static double MonthsThreeToSixRiskPercent { get; set; } = 5.0;

        public static double MonthsSevenToEightRiskPercent { get; set; } = 15.0;

        public static double MonthNineRiskPercent { get; set; } = 35.0;

        public static CombatPregnancyLossSettings Snapshot()
        {
            return new CombatPregnancyLossSettings(
                EnableCombatPregnancyLoss,
                RespectBannerlordBattleDeathSettings,
                MonthsThreeToSixRiskPercent,
                MonthsSevenToEightRiskPercent,
                MonthNineRiskPercent);
        }

        public static void ResetToDefaults()
        {
            EnableCombatPregnancyLoss = true;
            RespectBannerlordBattleDeathSettings = true;
            MonthsThreeToSixRiskPercent = 5.0;
            MonthsSevenToEightRiskPercent = 15.0;
            MonthNineRiskPercent = 35.0;
        }
    }

    /// <summary>
    /// The native Bannerlord protection that applies to the wounded mother when the
    /// player elects to have PLE respect Bannerlord battle-death settings.
    /// </summary>
    public enum CombatPregnancyNativeProtection
    {
        Normal = 0,
        ReducedByHalf = 1,
        Disabled = 2
    }
}
