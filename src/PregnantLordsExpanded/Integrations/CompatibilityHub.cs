using System;
using System.Collections.Generic;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Pregnancy;

namespace PregnantLordsExpanded.Integrations
{
    /// <summary>
    /// Stable hook surface for optional integrations. DramaLord can register a fallback
    /// pregnancy provider; AOC can register an observation sink. Neither can replace the
    /// native provider or make the standalone mod depend on an external assembly.
    /// </summary>
    public sealed class CompatibilityHub
    {
        private static readonly CompatibilityHub SharedInstance = new CompatibilityHub();
        private readonly List<IPregnancyObservationSink> _observationSinks =
            new List<IPregnancyObservationSink>();

        private CompatibilityHub()
        {
            PregnancyProviders = new PregnancyProviderRegistry();
        }

        public static CompatibilityHub Instance
        {
            get { return SharedInstance; }
        }

        public PregnancyProviderRegistry PregnancyProviders { get; private set; }

        public void RegisterPregnancyProvider(IPregnancyDataProvider provider)
        {
            PregnancyProviders.Register(provider);
        }

        public void RegisterObservationSink(IPregnancyObservationSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            for (int index = _observationSinks.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_observationSinks[index].Id, sink.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _observationSinks.RemoveAt(index);
                }
            }

            _observationSinks.Add(sink);
        }

        public void PublishObservation(PregnancyProgressResult result)
        {
            IPregnancyObservationSink[] sinks = _observationSinks.ToArray();
            foreach (IPregnancyObservationSink sink in sinks)
            {
                try
                {
                    if (sink.IsAvailable)
                    {
                        sink.OnPregnancyObserved(result);
                    }
                }
                catch (Exception exception)
                {
                    DiagnosticLog.WarnOnce(
                        "sink:" + sink.Id,
                        "Optional integration '" + sink.Id + "' failed and was ignored: " + exception.Message);
                }
            }
        }
    }
}

