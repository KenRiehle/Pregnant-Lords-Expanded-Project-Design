using System;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Resolves player-facing choices without changing campaign state. The campaign
    /// adapter owns prompts, persistence, relationship changes, and later travel.
    /// </summary>
    public static class PlayerWithdrawalDecisionCalculator
    {
        public static PlayerWithdrawalResolution ResolvePlayerAuthority(
            PlayerWithdrawalChoice choice)
        {
            switch (choice)
            {
                case PlayerWithdrawalChoice.ApprovePetition:
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
                        "A player authority must approve or deny the petition.");
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
