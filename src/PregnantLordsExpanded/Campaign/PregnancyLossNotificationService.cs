using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Withdrawal;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Emits a player-relevant pregnancy-loss event through Bannerlord's native
    /// right-side map-notice pipeline. Bannerlord itself uses ChildBornMapNotification
    /// with a null newborn for stillborn children, so PLE deliberately uses that same
    /// native/saveable notification type rather than introducing a custom UI type.
    /// </summary>
    internal static class PregnancyLossNotificationService
    {
        public static bool TryShowCombatLossNotice(
            Hero mother,
            Hero responsibleHero,
            WithdrawalResponsibility responsibility,
            out string description,
            out string suppressionReason)
        {
            description = string.Empty;
            suppressionReason = string.Empty;

            if (mother == null)
            {
                suppressionReason = "mother is null";
                return false;
            }

            if (!IsPlayerRelevant(mother, responsibleHero))
            {
                suppressionReason = "event is not player-relevant";
                return false;
            }

            TaleWorlds.CampaignSystem.Campaign campaign =
                TaleWorlds.CampaignSystem.Campaign.Current;
            if (campaign == null || campaign.CampaignInformationManager == null)
            {
                suppressionReason = "campaign information manager is unavailable";
                return false;
            }

            description = BuildDescription(mother, responsibleHero, responsibility);
            TextObject descriptionText = new TextObject(description);

            // This is the same built-in notification type Bannerlord uses for a
            // stillborn child (NewbornHero == null). It is already registered with
            // MapNotificationVM and is saveable by the base game.
            campaign.CampaignInformationManager.NewMapNoticeAdded(
                new ChildBornMapNotification(
                    null,
                    descriptionText,
                    CampaignTime.Now));

            DiagnosticLog.Info(
                "PLE pregnancy-loss map notice added: mother=" + mother.Name
                + ", responsibility=" + responsibility
                + ", responsibleHero="
                + (responsibleHero != null ? responsibleHero.Name.ToString() : "<none>")
                + ", text=\"" + description + "\".");

            return true;
        }

        private static string BuildDescription(
            Hero mother,
            Hero responsibleHero,
            WithdrawalResponsibility responsibility)
        {
            string opening = mother.Name
                + " has lost her unborn child after being wounded in battle. ";

            switch (responsibility)
            {
                case WithdrawalResponsibility.CommanderOverride:
                    if (responsibleHero != null && responsibleHero != mother)
                    {
                        return opening + "She blames " + responsibleHero.Name
                            + ", whose order kept her in active service.";
                    }

                    return opening
                        + "She blames the commander whose order kept her in active service.";

                case WithdrawalResponsibility.VoluntaryRefusal:
                    return opening
                        + "She accepts responsibility for choosing to remain in active service.";

                case WithdrawalResponsibility.WithdrawalApproved:
                    return opening
                        + "The loss occurred despite her approved withdrawal from active service.";

                case WithdrawalResponsibility.ForcedCircumstances:
                case WithdrawalResponsibility.None:
                default:
                    return opening
                        + "The loss is regarded as a consequence of the fighting.";
            }
        }

        private static bool IsPlayerRelevant(Hero mother, Hero responsibleHero)
        {
            if (mother == Hero.MainHero
                || mother.Clan == Clan.PlayerClan
                || mother.Spouse == Hero.MainHero)
            {
                return true;
            }

            if (responsibleHero == Hero.MainHero
                || (responsibleHero != null && responsibleHero.Clan == Clan.PlayerClan))
            {
                return true;
            }

            MobileParty party = mother.PartyBelongedTo;
            Army army = party != null ? party.Army : null;
            return army != null
                && army.LeaderParty != null
                && army.LeaderParty.LeaderHero == Hero.MainHero;
        }
    }
}
