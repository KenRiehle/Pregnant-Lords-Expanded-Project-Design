using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Pure Milestone 2D-E state transition for an already-approved withdrawal.
    /// The campaign adapter owns destination selection and the native Bannerlord
    /// travel action. This calculator only decides whether travel should start,
    /// remain in progress, retry after cancellation, or be considered complete.
    /// </summary>
    public static class ApprovedWithdrawalExecutionCalculator
    {
        public static ApprovedWithdrawalExecutionResult Calculate(
            ApprovedWithdrawalExecutionState previousState,
            bool isTraveling,
            bool isRestingAtDestination,
            bool canBeginTravel)
        {
            if (isRestingAtDestination)
            {
                return new ApprovedWithdrawalExecutionResult(
                    ApprovedWithdrawalExecutionState.Completed,
                    false);
            }

            if (previousState == ApprovedWithdrawalExecutionState.Completed)
            {
                return new ApprovedWithdrawalExecutionResult(
                    ApprovedWithdrawalExecutionState.Completed,
                    false);
            }

            if (isTraveling)
            {
                return new ApprovedWithdrawalExecutionResult(
                    ApprovedWithdrawalExecutionState.Traveling,
                    false);
            }

            if (canBeginTravel)
            {
                return new ApprovedWithdrawalExecutionResult(
                    ApprovedWithdrawalExecutionState.Traveling,
                    true);
            }

            return new ApprovedWithdrawalExecutionResult(
                ApprovedWithdrawalExecutionState.Pending,
                false);
        }
    }


    /// <summary>
    /// Pure rule for the pregnancy-long military-service restriction created by an
    /// approved withdrawal. Pending, Traveling, and Completed all remain restricted
    /// while the same pregnancy is active; ending the pregnancy clears the restriction.
    /// </summary>
    public static class PregnancyServiceRestrictionCalculator
    {
        public static bool ShouldRestrict(
            ApprovedWithdrawalExecutionState executionState,
            bool pregnancyActive)
        {
            return pregnancyActive
                && executionState != ApprovedWithdrawalExecutionState.None;
        }
    }

    /// <summary>
    /// Pure rule for PLE-caused army cleanup after removing a restricted withdrawal
    /// party. PLE may disband an army only when PLE performed the removal, the removed
    /// party was not the army leader, and no attached parties remain. This prevents the
    /// withdrawal system from globally disbanding legitimate one-party gathering armies.
    /// </summary>
    public static class LeaderOnlyArmyCleanupCalculator
    {
        public static bool ShouldDisbandAfterPleRemoval(
            bool removalWasCausedByPle,
            bool removedPartyWasArmyLeader,
            int remainingAttachedPartyCount)
        {
            return removalWasCausedByPle
                && !removedPartyWasArmyLeader
                && remainingAttachedPartyCount <= 0;
        }
    }
}
