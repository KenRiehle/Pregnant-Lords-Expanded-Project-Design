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
}
