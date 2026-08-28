using TaleWorlds.CampaignSystem;

namespace PregnantLordsExpanded.Pregnancy
{
    public sealed class PregnancyProgressResult
    {
        private PregnancyProgressResult(
            Hero mother,
            Hero father,
            bool isPregnant,
            bool hasKnownProgress,
            double progress,
            int approximateMonth,
            CampaignTime? expectedDueTime,
            string dataSource,
            string failureReason)
        {
            Mother = mother;
            Father = father;
            IsPregnant = isPregnant;
            HasKnownProgress = hasKnownProgress;
            Progress = progress;
            ApproximateMonth = approximateMonth;
            ExpectedDueTime = expectedDueTime;
            DataSource = dataSource ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public Hero Mother { get; private set; }

        public Hero Father { get; private set; }

        public bool IsPregnant { get; private set; }

        public bool HasKnownProgress { get; private set; }

        public double Progress { get; private set; }

        public int ApproximateMonth { get; private set; }

        public CampaignTime? ExpectedDueTime { get; private set; }

        public string DataSource { get; private set; }

        public string FailureReason { get; private set; }

        public static PregnancyProgressResult NotPregnant(Hero hero, string reason)
        {
            return new PregnancyProgressResult(
                hero,
                null,
                false,
                false,
                0.0,
                0,
                null,
                string.Empty,
                reason);
        }

        public static PregnancyProgressResult PregnantUnknown(PregnancyDataSnapshot snapshot, string reason)
        {
            return new PregnancyProgressResult(
                snapshot.Mother,
                snapshot.Father,
                true,
                false,
                0.0,
                0,
                snapshot.ExpectedDueTime,
                snapshot.DataSource,
                reason);
        }

        public static PregnancyProgressResult PregnantKnown(
            PregnancyDataSnapshot snapshot,
            double progress,
            int approximateMonth)
        {
            return new PregnancyProgressResult(
                snapshot.Mother,
                snapshot.Father,
                true,
                true,
                progress,
                approximateMonth,
                snapshot.ExpectedDueTime,
                snapshot.DataSource,
                string.Empty);
        }
    }
}

