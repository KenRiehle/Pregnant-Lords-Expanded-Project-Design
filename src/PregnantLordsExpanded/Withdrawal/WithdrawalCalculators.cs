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

            // A denial by itself causes minor resentment. Injury and attributable
            // pregnancy loss escalate from this target in later consequence stages.
            return -5;
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


namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Milestone 2D-D routine resentment rule. Each normalized month from 4 through 9
    /// may contribute exactly one -5 denial penalty. The campaign adapter persists the
    /// returned ledger key so daily ticks, repeated petitions in the same month, and
    /// save/load cannot apply the routine penalty twice.
    /// </summary>
    public static class MonthlyDenialResentmentCalculator
    {
        public const int FirstRoutineDenialMonth = 4;
        public const int LastRoutineDenialMonth = 9;
        public const int RoutineDenialPenalty = -5;

        public static int GetPendingPenalty(
            int normalizedMonth,
            bool alreadyAppliedForNormalizedMonth)
        {
            if (alreadyAppliedForNormalizedMonth
                || normalizedMonth < FirstRoutineDenialMonth
                || normalizedMonth > LastRoutineDenialMonth)
            {
                return 0;
            }

            return RoutineDenialPenalty;
        }

        public static int GetMaximumRoutinePenalty()
        {
            return RoutineDenialPenalty
                * (LastRoutineDenialMonth - FirstRoutineDenialMonth + 1);
        }

        public static string GetLedgerKey(string pregnancyKey, int normalizedMonth)
        {
            if (string.IsNullOrWhiteSpace(pregnancyKey))
            {
                throw new ArgumentException(
                    "A pregnancy key is required.",
                    nameof(pregnancyKey));
            }

            if (normalizedMonth < FirstRoutineDenialMonth
                || normalizedMonth > LastRoutineDenialMonth)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedMonth));
            }

            return pregnancyKey + "|month:" + normalizedMonth;
        }

        public static bool TryGetNormalizedMonthFromRequestKey(
            string requestKey,
            out int normalizedMonth)
        {
            normalizedMonth = 0;
            if (string.IsNullOrWhiteSpace(requestKey))
            {
                return false;
            }

            const string marker = "|month:";
            int markerIndex = requestKey.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            int parsed;
            if (!int.TryParse(
                    requestKey.Substring(markerIndex + marker.Length),
                    out parsed)
                || parsed < FirstRoutineDenialMonth
                || parsed > LastRoutineDenialMonth)
            {
                return false;
            }

            normalizedMonth = parsed;
            return true;
        }
    }
}
