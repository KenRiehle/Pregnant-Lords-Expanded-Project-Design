using TaleWorlds.CampaignSystem;

namespace PregnantLordsExpanded.Pregnancy
{
    /// <summary>
    /// Optional pregnancy sources implement this contract without changing Bannerlord's
    /// conception or birth lifecycle. A provider only reports pregnancies it owns or can
    /// authoritatively observe.
    /// </summary>
    public interface IPregnancyDataProvider
    {
        string Id { get; }

        int Priority { get; }

        bool IsAvailable { get; }

        bool TryGetPregnancy(Hero hero, out PregnancyDataSnapshot snapshot);
    }
}

