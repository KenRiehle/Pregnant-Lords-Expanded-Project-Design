using System;
using System.Collections.Generic;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Integrations;
using PregnantLordsExpanded.Pregnancy.Providers;
using TaleWorlds.CampaignSystem;

namespace PregnantLordsExpanded.Pregnancy
{
    public sealed class PregnancyProgressService
    {
        private static readonly PregnancyProgressService SharedInstance = new PregnancyProgressService();
        private readonly PregnancyProviderRegistry _providers;

        private PregnancyProgressService()
        {
            _providers = CompatibilityHub.Instance.PregnancyProviders;
            _providers.Register(new NativePregnancyProvider());
        }

        public static PregnancyProgressService Instance
        {
            get { return SharedInstance; }
        }

        public PregnancyProgressResult GetProgress(Hero hero)
        {
            if (hero == null)
            {
                return PregnancyProgressResult.NotPregnant(null, "Hero is null.");
            }

            if (!hero.IsAlive || !hero.IsFemale)
            {
                return PregnancyProgressResult.NotPregnant(
                    hero,
                    "Hero is not an eligible living female hero.");
            }

            PregnancyDataSnapshot bestUnknownSnapshot = null;
            string bestUnknownReason = string.Empty;
            IList<IPregnancyDataProvider> providers = _providers.GetProviders();

            foreach (IPregnancyDataProvider provider in providers)
            {
                if (provider == null || !provider.IsAvailable)
                {
                    continue;
                }

                try
                {
                    PregnancyDataSnapshot snapshot;
                    if (!provider.TryGetPregnancy(hero, out snapshot) || snapshot == null)
                    {
                        continue;
                    }

                    double conceptionDay;
                    double dueDay;
                    string timingFailure;
                    if (!TryResolveTimingWindow(snapshot, out conceptionDay, out dueDay, out timingFailure))
                    {
                        if (bestUnknownSnapshot == null)
                        {
                            bestUnknownSnapshot = snapshot;
                            bestUnknownReason = FirstNonEmpty(timingFailure, snapshot.FailureReason);
                        }

                        continue;
                    }

                    double progress;
                    int month;
                    string calculationFailure;
                    if (PregnancyProgressCalculator.TryCalculate(
                        CampaignTime.Now.ToDays,
                        conceptionDay,
                        dueDay,
                        out progress,
                        out month,
                        out calculationFailure))
                    {
                        return PregnancyProgressResult.PregnantKnown(snapshot, progress, month);
                    }

                    if (bestUnknownSnapshot == null)
                    {
                        bestUnknownSnapshot = snapshot;
                        bestUnknownReason = FirstNonEmpty(calculationFailure, snapshot.FailureReason);
                    }
                }
                catch (Exception exception)
                {
                    DiagnosticLog.WarnOnce(
                        "provider:" + provider.Id,
                        "Pregnancy provider '" + provider.Id + "' failed and was ignored: "
                        + exception.Message);
                }
            }

            if (bestUnknownSnapshot != null)
            {
                return PregnancyProgressResult.PregnantUnknown(
                    bestUnknownSnapshot,
                    FirstNonEmpty(bestUnknownReason, "Pregnancy timing is unavailable."));
            }

            return PregnancyProgressResult.NotPregnant(hero, string.Empty);
        }

        private static bool TryResolveTimingWindow(
            PregnancyDataSnapshot snapshot,
            out double conceptionDay,
            out double dueDay,
            out string failureReason)
        {
            conceptionDay = 0.0;
            dueDay = 0.0;
            failureReason = string.Empty;

            if (snapshot.ConceptionDay.HasValue && snapshot.DueDay.HasValue)
            {
                conceptionDay = snapshot.ConceptionDay.Value;
                dueDay = snapshot.DueDay.Value;
                return true;
            }

            if (snapshot.DueDay.HasValue && IsPositiveFinite(snapshot.DurationDays))
            {
                dueDay = snapshot.DueDay.Value;
                conceptionDay = dueDay - snapshot.DurationDays.Value;
                return true;
            }

            if (snapshot.ConceptionDay.HasValue && IsPositiveFinite(snapshot.DurationDays))
            {
                conceptionDay = snapshot.ConceptionDay.Value;
                dueDay = conceptionDay + snapshot.DurationDays.Value;
                return true;
            }

            failureReason = FirstNonEmpty(
                snapshot.FailureReason,
                "No valid conception/due-time pair or active duration is available.");
            return false;
        }

        private static bool IsPositiveFinite(double? value)
        {
            return value.HasValue
                && !double.IsNaN(value.Value)
                && !double.IsInfinity(value.Value)
                && value.Value > 0.0;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrEmpty(first) ? first : (second ?? string.Empty);
        }
    }
}

