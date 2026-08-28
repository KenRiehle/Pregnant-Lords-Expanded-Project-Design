using TaleWorlds.CampaignSystem;

namespace PregnantLordsExpanded.Pregnancy
{
    /// <summary>
    /// Neutral timing data supplied by Bannerlord or an optional compatibility provider.
    /// This is an observation of an existing pregnancy, not a second pregnancy timeline.
    /// </summary>
    public sealed class PregnancyDataSnapshot
    {
        public PregnancyDataSnapshot(
            Hero mother,
            Hero father,
            double? conceptionDay,
            double? dueDay,
            double? durationDays,
            CampaignTime? expectedDueTime,
            string dataSource,
            string failureReason)
        {
            Mother = mother;
            Father = father;
            ConceptionDay = conceptionDay;
            DueDay = dueDay;
            DurationDays = durationDays;
            ExpectedDueTime = expectedDueTime;
            DataSource = dataSource ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public Hero Mother { get; private set; }

        public Hero Father { get; private set; }

        public double? ConceptionDay { get; private set; }

        public double? DueDay { get; private set; }

        public double? DurationDays { get; private set; }

        public CampaignTime? ExpectedDueTime { get; private set; }

        public string DataSource { get; private set; }

        public string FailureReason { get; private set; }
    }
}

