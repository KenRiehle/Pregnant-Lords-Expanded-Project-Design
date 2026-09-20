using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Pure state transition logic for protected settlement rest. This class does
    /// not use Bannerlord types, which keeps responsibility rules deterministic and
    /// directly testable.
    /// </summary>
    public static class ProtectedRestTransitionCalculator
    {
        public static ProtectedRestTransitionResult Calculate(
            ProtectedRestTransitionInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string protectedSettlementId = input.ProtectedSettlementId ?? string.Empty;
            string observedSettlementId = input.ObservedSettlementId ?? string.Empty;
            bool wasProtected = input.PreviousState == ProtectedRestState.ProtectedRest
                || input.PreviousState == ProtectedRestState.ProtectedDefense;

            if (input.ObservedState == ProtectedRestState.ProtectedRest)
            {
                ProtectedRestTransitionKind transition = wasProtected
                    ? ProtectedRestTransitionKind.None
                    : input.PreviousState == ProtectedRestState.Unknown
                        ? ProtectedRestTransitionKind.ProtectedRestEstablished
                        : ProtectedRestTransitionKind.ReturnedToProtectedRest;

                return new ProtectedRestTransitionResult(
                    transition,
                    ProtectedRestState.ProtectedRest,
                    observedSettlementId,
                    WithdrawalResponsibility.None);
            }

            if (input.ObservedState == ProtectedRestState.Campaigning)
            {
                if (wasProtected
                    && input.IsDefendingProtectedSettlement
                    && SameSettlement(protectedSettlementId, observedSettlementId))
                {
                    return new ProtectedRestTransitionResult(
                        input.PreviousState == ProtectedRestState.ProtectedDefense
                            ? ProtectedRestTransitionKind.None
                            : ProtectedRestTransitionKind.DefensiveMobilization,
                        ProtectedRestState.ProtectedDefense,
                        protectedSettlementId,
                        WithdrawalResponsibility.None);
                }

                return new ProtectedRestTransitionResult(
                    wasProtected
                        ? ProtectedRestTransitionKind.PresumedVoluntaryDeparture
                        : ProtectedRestTransitionKind.None,
                    ProtectedRestState.Campaigning,
                    string.Empty,
                    wasProtected
                        ? WithdrawalResponsibility.VoluntaryRefusal
                        : WithdrawalResponsibility.None);
            }

            if (input.ObservedState == ProtectedRestState.Prisoner)
            {
                return new ProtectedRestTransitionResult(
                    wasProtected
                        ? ProtectedRestTransitionKind.ForcedRemoval
                        : ProtectedRestTransitionKind.None,
                    ProtectedRestState.Prisoner,
                    string.Empty,
                    wasProtected
                        ? WithdrawalResponsibility.ForcedCircumstances
                        : WithdrawalResponsibility.None);
            }

            if (input.ObservedState == ProtectedRestState.Unavailable)
            {
                return new ProtectedRestTransitionResult(
                    wasProtected
                        ? ProtectedRestTransitionKind.UnresolvedDeparture
                        : ProtectedRestTransitionKind.None,
                    ProtectedRestState.Unavailable,
                    string.Empty,
                    wasProtected
                        ? WithdrawalResponsibility.ForcedCircumstances
                        : WithdrawalResponsibility.None);
            }

            return new ProtectedRestTransitionResult(
                ProtectedRestTransitionKind.None,
                input.ObservedState,
                wasProtected ? protectedSettlementId : string.Empty,
                WithdrawalResponsibility.None);
        }

        private static bool SameSettlement(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
