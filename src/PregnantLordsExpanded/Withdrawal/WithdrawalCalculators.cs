using System;

namespace PregnantLordsExpanded.Withdrawal
{
    public static class WithdrawalMonthCalculator
    {
        public static WithdrawalMonthResult Calculate(
            int normalizedMonth,
            WithdrawalSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (normalizedMonth < 1 || normalizedMonth > 9)
            {
                return new WithdrawalMonthResult(normalizedMonth, WithdrawalMonthStage.None);
            }

            if (normalizedMonth == settings.WarningMonth)
            {
                return new WithdrawalMonthResult(
                    normalizedMonth,
                    WithdrawalMonthStage.AdvanceWarning);
            }

            if (normalizedMonth == settings.FormalPetitionMonth)
            {
                return new WithdrawalMonthResult(
                    normalizedMonth,
                    WithdrawalMonthStage.FormalPetition);
            }

            if (normalizedMonth > settings.FormalPetitionMonth)
            {
                return new WithdrawalMonthResult(
                    normalizedMonth,
                    WithdrawalMonthStage.RenewedPetition);
            }

            return new WithdrawalMonthResult(normalizedMonth, WithdrawalMonthStage.None);
        }
    }

    public static class WithdrawalResponsibilityCalculator
    {
        public static WithdrawalResponsibility FromDecision(WithdrawalDecision decision)
        {
            switch (decision)
            {
                case WithdrawalDecision.Approve:
                    return WithdrawalResponsibility.WithdrawalApproved;
                case WithdrawalDecision.Deny:
                    return WithdrawalResponsibility.CommanderOverride;
                case WithdrawalDecision.ContinueVoluntarily:
                    return WithdrawalResponsibility.VoluntaryRefusal;
                case WithdrawalDecision.ForcedDelay:
                    return WithdrawalResponsibility.ForcedCircumstances;
                default:
                    return WithdrawalResponsibility.None;
            }
        }
    }

    public static class CommanderLiabilityCalculator
    {
        public static int GetDefaultCumulativePenalty(int normalizedMonth)
        {
            if (normalizedMonth < 4 || normalizedMonth > 9)
            {
                return 0;
            }

            switch (normalizedMonth)
            {
                case 4:
                    return -25;
                case 5:
                    return -35;
                case 6:
                    return -45;
                case 7:
                    return -55;
                case 8:
                    return -65;
                default:
                    return -75;
            }
        }

        public static int GetAdditionalPenalty(int alreadyApplied, int targetCumulativePenalty)
        {
            if (targetCumulativePenalty >= alreadyApplied)
            {
                return 0;
            }

            return targetCumulativePenalty - alreadyApplied;
        }
    }

    /// <summary>
    /// Calculates the relationship delta still owed for the current denial tier.
    /// The already-applied value must come from the Milestone 2D relationship ledger,
    /// not the older diagnostic liability ledger.
    /// </summary>
    public static class CommanderRelationPenaltyCalculator
    {
        public static int GetPendingPenalty(
            int normalizedMonth,
            int alreadyAppliedCumulativePenalty)
        {
            int targetCumulativePenalty =
                CommanderLiabilityCalculator.GetDefaultCumulativePenalty(normalizedMonth);
            return CommanderLiabilityCalculator.GetAdditionalPenalty(
                alreadyAppliedCumulativePenalty,
                targetCumulativePenalty);
        }
    }
}
