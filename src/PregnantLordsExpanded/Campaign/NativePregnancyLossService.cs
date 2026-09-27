using System.Collections;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Ends a Bannerlord-native pregnancy atomically enough for PLE: the matching
    /// private PregnancyCampaignBehavior record is removed first, then Hero.IsPregnant
    /// is cleared. If the native record cannot be found, the pregnancy is left intact
    /// rather than creating a ghost due-date record.
    /// </summary>
    internal static class NativePregnancyLossService
    {
        public static bool TryEndPregnancy(Hero mother, out string failureReason)
        {
            failureReason = string.Empty;

            if (mother == null)
            {
                failureReason = "mother is null";
                return false;
            }

            if (!mother.IsPregnant)
            {
                failureReason = "mother is no longer marked pregnant";
                return false;
            }

            if (TaleWorlds.CampaignSystem.Campaign.Current == null)
            {
                failureReason = "Campaign.Current is null";
                return false;
            }

            PregnancyCampaignBehavior behavior =
                TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<PregnancyCampaignBehavior>();
            if (behavior == null)
            {
                failureReason = "native PregnancyCampaignBehavior was not found";
                return false;
            }

            FieldInfo pregnanciesField = typeof(PregnancyCampaignBehavior).GetField(
                "_heroPregnancies",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (pregnanciesField == null)
            {
                failureReason = "native _heroPregnancies field was not found";
                return false;
            }

            IList pregnancies = pregnanciesField.GetValue(behavior) as IList;
            if (pregnancies == null)
            {
                failureReason = "native pregnancy ledger was unavailable";
                return false;
            }

            int removed = 0;
            for (int index = pregnancies.Count - 1; index >= 0; index--)
            {
                object pregnancy = pregnancies[index];
                if (pregnancy == null)
                {
                    continue;
                }

                FieldInfo motherField = pregnancy.GetType().GetField(
                    "Mother",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (motherField == null)
                {
                    continue;
                }

                Hero recordedMother = motherField.GetValue(pregnancy) as Hero;
                if (recordedMother == mother)
                {
                    pregnancies.RemoveAt(index);
                    removed++;
                }
            }

            if (removed == 0)
            {
                failureReason = "matching native pregnancy record was not found";
                return false;
            }

            mother.IsPregnant = false;
            return true;
        }
    }
}
