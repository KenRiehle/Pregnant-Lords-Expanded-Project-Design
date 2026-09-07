using System;

namespace PregnantLordsExpanded.Withdrawal
{
    public static class WithdrawalAuthorityResolver
    {
        public static WithdrawalAuthorityResult Resolve(
            WithdrawalAuthorityContext context,
            IndependentWithdrawalAuthorityMode independentAuthorityMode)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrWhiteSpace(context.MotherId))
            {
                return None("The pregnant hero has no stable identifier.");
            }

            if (context.IsPrisoner)
            {
                return None("Captivity rules control this case.");
            }

            if (!context.IsCampaigning || context.IsResting)
            {
                return None("The pregnant hero is not currently campaigning.");
            }

            if (context.IsArmyMember)
            {
                if (string.IsNullOrWhiteSpace(context.ArmyLeaderId))
                {
                    return None("The army leader could not be resolved.");
                }

                return Create(
                    context.MotherId,
                    context.ArmyLeaderId,
                    WithdrawalAuthorityKind.ArmyLeader);
            }

            if (!string.IsNullOrWhiteSpace(context.PartyLeaderId)
                && !SameHero(context.MotherId, context.PartyLeaderId))
            {
                return new WithdrawalAuthorityResult(
                    WithdrawalAuthorityKind.PartyLeader,
                    context.PartyLeaderId,
                    string.Empty);
            }

            if (!context.LeadsIndependentParty)
            {
                return None("No valid military authority could be resolved.");
            }

            switch (independentAuthorityMode)
            {
                case IndependentWithdrawalAuthorityMode.Self:
                    return Self(context.MotherId);

                case IndependentWithdrawalAuthorityMode.KingdomRuler:
                    if (!string.IsNullOrWhiteSpace(context.KingdomRulerId))
                    {
                        return Create(
                            context.MotherId,
                            context.KingdomRulerId,
                            WithdrawalAuthorityKind.KingdomRuler);
                    }

                    return ResolveClanAuthority(context);

                default:
                    return ResolveClanAuthority(context);
            }
        }

        private static WithdrawalAuthorityResult ResolveClanAuthority(
            WithdrawalAuthorityContext context)
        {
            if (string.IsNullOrWhiteSpace(context.ClanLeaderId))
            {
                return None("The clan leader could not be resolved.");
            }

            return Create(
                context.MotherId,
                context.ClanLeaderId,
                WithdrawalAuthorityKind.ClanLeader);
        }

        private static WithdrawalAuthorityResult Create(
            string motherId,
            string authorityId,
            WithdrawalAuthorityKind externalKind)
        {
            return SameHero(motherId, authorityId)
                ? Self(motherId)
                : new WithdrawalAuthorityResult(externalKind, authorityId, string.Empty);
        }

        private static WithdrawalAuthorityResult Self(string motherId)
        {
            return new WithdrawalAuthorityResult(
                WithdrawalAuthorityKind.Self,
                motherId,
                string.Empty);
        }

        private static WithdrawalAuthorityResult None(string reason)
        {
            return new WithdrawalAuthorityResult(
                WithdrawalAuthorityKind.None,
                string.Empty,
                reason);
        }

        private static bool SameHero(string firstId, string secondId)
        {
            return string.Equals(firstId, secondId, StringComparison.Ordinal);
        }
    }
}
