namespace PregnantLordsExpanded.Pregnancy
{
    /// <summary>
    /// Optional integrations such as AOC may observe results through this interface.
    /// Observation sinks cannot change the pregnancy result used by the core mod.
    /// </summary>
    public interface IPregnancyObservationSink
    {
        string Id { get; }

        bool IsAvailable { get; }

        void OnPregnancyObserved(PregnancyProgressResult result);
    }
}

