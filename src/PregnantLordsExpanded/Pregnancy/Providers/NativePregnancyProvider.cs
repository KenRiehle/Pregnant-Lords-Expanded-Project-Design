using System;
using System.Collections;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using BannerlordCampaign = TaleWorlds.CampaignSystem.Campaign;

namespace PregnantLordsExpanded.Pregnancy.Providers
{
    /// <summary>
    /// Reads Bannerlord's active pregnancy records without altering them. Reflection is
    /// limited to the private record collection because Bannerlord exposes IsPregnant and
    /// the active model publicly, but not each pregnancy's due date.
    /// </summary>
    public sealed class NativePregnancyProvider : IPregnancyDataProvider
    {
        private const string SourceId = "Bannerlord.NativePregnancyRecord";
        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private FieldInfo _pregnanciesField;

        public string Id
        {
            get { return SourceId; }
        }

        public int Priority
        {
            get { return int.MaxValue; }
        }

        public bool IsAvailable
        {
            get { return BannerlordCampaign.Current != null; }
        }

        public bool TryGetPregnancy(Hero hero, out PregnancyDataSnapshot snapshot)
        {
            snapshot = null;
            if (hero == null || !hero.IsPregnant)
            {
                return false;
            }

            BannerlordCampaign campaign = BannerlordCampaign.Current;
            if (campaign == null)
            {
                snapshot = Unknown(hero, "No active campaign is available.");
                return true;
            }

            PregnancyCampaignBehavior behavior = campaign.GetCampaignBehavior<PregnancyCampaignBehavior>();
            if (behavior == null)
            {
                snapshot = Unknown(hero, "Bannerlord's pregnancy campaign behavior is unavailable.");
                return true;
            }

            ResolvePregnanciesField(behavior.GetType());
            if (_pregnanciesField == null)
            {
                snapshot = Unknown(hero, "Bannerlord's active pregnancy record collection could not be resolved.");
                return true;
            }

            IEnumerable records = _pregnanciesField.GetValue(behavior) as IEnumerable;
            if (records == null)
            {
                snapshot = Unknown(hero, "Bannerlord's active pregnancy record collection is unavailable.");
                return true;
            }

            foreach (object record in records)
            {
                if (record == null)
                {
                    continue;
                }

                Hero mother;
                if (!TryReadMember(record, "Mother", out mother) || mother != hero)
                {
                    continue;
                }

                Hero father;
                TryReadMember(record, "Father", out father);

                CampaignTime dueTime;
                if (!TryReadMember(record, "DueDate", out dueTime))
                {
                    snapshot = new PregnancyDataSnapshot(
                        hero,
                        father,
                        null,
                        null,
                        GetActiveDurationDays(campaign),
                        null,
                        SourceId,
                        "The native pregnancy record has no readable due date.");
                    return true;
                }

                double? durationDays = GetActiveDurationDays(campaign);
                snapshot = new PregnancyDataSnapshot(
                    hero,
                    father,
                    null,
                    dueTime.ToDays,
                    durationDays,
                    dueTime,
                    SourceId,
                    durationDays.HasValue
                        ? string.Empty
                        : "The active pregnancy model returned an invalid duration.");
                return true;
            }

            snapshot = Unknown(
                hero,
                "Hero.IsPregnant is true, but no matching native pregnancy record was found.");
            return true;
        }

        private static double? GetActiveDurationDays(BannerlordCampaign campaign)
        {
            if (campaign.Models == null || campaign.Models.PregnancyModel == null)
            {
                return null;
            }

            double duration = campaign.Models.PregnancyModel.PregnancyDurationInDays;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0.0)
            {
                return null;
            }

            return duration;
        }

        private static PregnancyDataSnapshot Unknown(Hero hero, string reason)
        {
            return new PregnancyDataSnapshot(
                hero,
                null,
                null,
                null,
                null,
                null,
                SourceId,
                reason);
        }

        private void ResolvePregnanciesField(Type behaviorType)
        {
            if (_pregnanciesField != null)
            {
                return;
            }

            _pregnanciesField = behaviorType.GetField("_heroPregnancies", InstanceMembers);
            if (_pregnanciesField != null)
            {
                return;
            }

            foreach (FieldInfo field in behaviorType.GetFields(InstanceMembers))
            {
                if (typeof(IEnumerable).IsAssignableFrom(field.FieldType)
                    && field.Name.IndexOf("pregnan", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _pregnanciesField = field;
                    return;
                }
            }
        }

        private static bool TryReadMember<T>(object instance, string memberName, out T value)
        {
            value = default(T);
            Type type = instance.GetType();

            FieldInfo field = type.GetField(memberName, InstanceMembers);
            if (field != null)
            {
                object fieldValue = field.GetValue(instance);
                if (fieldValue is T)
                {
                    value = (T)fieldValue;
                    return true;
                }

                if (fieldValue == null && !typeof(T).IsValueType)
                {
                    return true;
                }
            }

            PropertyInfo property = type.GetProperty(memberName, InstanceMembers);
            if (property != null)
            {
                object propertyValue = property.GetValue(instance, null);
                if (propertyValue is T)
                {
                    value = (T)propertyValue;
                    return true;
                }

                if (propertyValue == null && !typeof(T).IsValueType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
