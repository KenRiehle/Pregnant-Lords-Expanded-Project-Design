using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Resolves player-facing choices without changing campaign state. The campaign
    /// adapter owns prompts, persistence, relationship changes, and travel.
    /// Milestone 2D-F-B adds explicit escort-plan approval choices while preserving
    /// the legacy approval value for save/backward compatibility.
    /// </summary>
    public static class PlayerWithdrawalDecisionCalculator
    {
        public static PlayerWithdrawalResolution ResolvePlayerAuthority(
            PlayerWithdrawalChoice choice)
        {
            switch (choice)
            {
                case PlayerWithdrawalChoice.ApprovePetition:
                case PlayerWithdrawalChoice.ApproveStrongEscort:
                case PlayerWithdrawalChoice.ApproveLeanEscort:
                case PlayerWithdrawalChoice.ApproveMinimalEscort:
                    return new PlayerWithdrawalResolution(
                        WithdrawalDecision.Approve,
                        WithdrawalDecision.Approve,
                        WithdrawalResponsibility.WithdrawalApproved,
                        false);

                case PlayerWithdrawalChoice.DenyPetition:
                    return new PlayerWithdrawalResolution(
                        WithdrawalDecision.Deny,
                        WithdrawalDecision.Deny,
                        WithdrawalResponsibility.CommanderOverride,
                        true);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(choice),
                        "A player authority must approve an escort plan or deny the petition.");
            }
        }

        public static bool TryGetEscortPlan(
            PlayerWithdrawalChoice choice,
            out WithdrawalEscortPlan plan)
        {
            switch (choice)
            {
                case PlayerWithdrawalChoice.ApproveStrongEscort:
                    plan = WithdrawalEscortPlan.Strong;
                    return true;

                case PlayerWithdrawalChoice.ApproveLeanEscort:
                    plan = WithdrawalEscortPlan.Lean;
                    return true;

                case PlayerWithdrawalChoice.ApproveMinimalEscort:
                    plan = WithdrawalEscortPlan.Minimal;
                    return true;

                default:
                    plan = default(WithdrawalEscortPlan);
                    return false;
            }
        }

        public static PlayerWithdrawalResolution ResolvePregnantPlayer(
            WithdrawalDecision authorityDecision,
            PlayerWithdrawalChoice choice)
        {
            if (choice != PlayerWithdrawalChoice.Withdraw
                && choice != PlayerWithdrawalChoice.ContinueCampaigning)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(choice),
                    "The pregnant player must withdraw or continue campaigning.");
            }

            bool commanderDenied = authorityDecision == WithdrawalDecision.Deny;
            if (choice == PlayerWithdrawalChoice.Withdraw)
            {
                return new PlayerWithdrawalResolution(
                    authorityDecision,
                    WithdrawalDecision.Approve,
                    WithdrawalResponsibility.WithdrawalApproved,
                    commanderDenied);
            }

            WithdrawalResponsibility responsibility = commanderDenied
                ? WithdrawalResponsibility.CommanderOverride
                : WithdrawalResponsibility.VoluntaryRefusal;
            WithdrawalDecision finalDecision = commanderDenied
                ? WithdrawalDecision.Deny
                : WithdrawalDecision.ContinueVoluntarily;

            return new PlayerWithdrawalResolution(
                authorityDecision,
                finalDecision,
                responsibility,
                commanderDenied);
        }
    }
}
