using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Pure calculation for a single completed battle. Campaign integration must
    /// persist the battle identifier before applying any random outcome so reloads
    /// cannot repeat the roll.
    /// </summary>
    public static class PregnancyBattleRiskCalculator
    {
        public static PregnancyBattleRiskResult Calculate(PregnancyBattleRiskInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ValidateHealth(input.HealthBeforeBattlePercent, nameof(input.HealthBeforeBattlePercent));
            ValidateHealth(input.HealthAfterBattlePercent, nameof(input.HealthAfterBattlePercent));

            if (!input.IsPregnant
                || input.BattleAlreadyProcessed
                || input.HealthAfterBattlePercent >= input.HealthBeforeBattlePercent
                || input.HealthAfterBattlePercent >= 75.0)
            {
                return NoRisk();
            }

            PregnancyBattleInjurySeverity severity;
            int lossChance;
            int commanderTarget;

            if (input.HealthAfterBattlePercent < 25.0)
            {
                severity = PregnancyBattleInjurySeverity.Critical;
                lossChance = 50;
                commanderTarget = -35;
            }
            else if (input.HealthAfterBattlePercent < 50.0)
            {
                severity = PregnancyBattleInjurySeverity.Severe;
                lossChance = 30;
                commanderTarget = -25;
            }
            else
            {
                severity = PregnancyBattleInjurySeverity.Significant;
                lossChance = 10;
                commanderTarget = -10;
            }

            int responsiblePartyRelationTarget =
                input.Responsibility == WithdrawalResponsibility.CommanderOverride
                    ? commanderTarget
                    : 0;

            return new PregnancyBattleRiskResult(
                true,
                severity,
                lossChance,
                responsiblePartyRelationTarget);
        }

        private static PregnancyBattleRiskResult NoRisk()
        {
            return new PregnancyBattleRiskResult(
                false,
                PregnancyBattleInjurySeverity.None,
                0,
                0);
        }

        private static void ValidateHealth(double healthPercent, string parameterName)
        {
            if (double.IsNaN(healthPercent)
                || double.IsInfinity(healthPercent)
                || healthPercent < 0.0
                || healthPercent > 100.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "A health percentage must be between 0 and 100.");
            }
        }
    }
}
