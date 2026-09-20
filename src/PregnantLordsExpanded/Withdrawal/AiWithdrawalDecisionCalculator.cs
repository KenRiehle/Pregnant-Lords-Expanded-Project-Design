using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Pure, bounded, and explainable diagnostic decision. No campaign state is changed.
    /// A random component can be added later only when its result is persisted in the ledger.
    /// </summary>
    public static class AiWithdrawalDecisionCalculator
    {
        public static AiWithdrawalDecisionResult Calculate(AiWithdrawalDecisionInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.NormalizedMonth < 4 || input.NormalizedMonth > 9)
            {
                return new AiWithdrawalDecisionResult(
                    WithdrawalDecision.NoDecision,
                    0,
                    "No formal withdrawal petition is due.");
            }

            int monthScore = (input.NormalizedMonth - 5) * 20;
            int mercyScore = Clamp(input.MercyLevel, -2, 2) * 12;
            int honorScore = Clamp(input.HonorLevel, -2, 2) * 12;
            int valorScore = Clamp(input.ValorLevel, -2, 2) * -8;
            int calculatingScore = Clamp(input.CalculatingLevel, -2, 2) * 5;
            int relationScore = Clamp(input.RelationWithMother, -100, 100) / 10;
            int replacementScore = input.HasReplacement ? 10 : 0;
            int safetyScore = input.HasNearbyFriendlyProtection ? 10 : 0;
            int dynasticScore = input.HasHighDynasticRisk ? 10 : 0;
            int emergencyScore = input.IsMilitaryEmergency ? -40 : 0;

            int total = Clamp(
                monthScore
                + mercyScore
                + honorScore
                + valorScore
                + calculatingScore
                + relationScore
                + replacementScore
                + safetyScore
                + dynasticScore
                + emergencyScore,
                -100,
                100);

            bool approvesWithdrawal = total >= 0;
            WithdrawalDecision decision = approvesWithdrawal
                ? WithdrawalDecision.Approve
                : (input.IsSelfAuthority
                    ? WithdrawalDecision.ContinueVoluntarily
                    : WithdrawalDecision.Deny);

            string explanation =
                "score=" + total
                + " [month=" + monthScore
                + ", mercy=" + mercyScore
                + ", honor=" + honorScore
                + ", valor=" + valorScore
                + ", calculating=" + calculatingScore
                + ", relation=" + relationScore
                + ", replacement=" + replacementScore
                + ", safety=" + safetyScore
                + ", dynastic=" + dynasticScore
                + ", emergency=" + emergencyScore + "]";

            return new AiWithdrawalDecisionResult(decision, total, explanation);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
