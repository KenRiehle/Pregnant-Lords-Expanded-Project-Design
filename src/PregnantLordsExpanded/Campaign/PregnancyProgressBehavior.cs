using System.Collections.Generic;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Integrations;
using PregnantLordsExpanded.Pregnancy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Pregnancy observation and Milestone 2B withdrawal diagnostics. It deliberately
    /// performs no withdrawal, teleportation, relationship, party, combat, dialogue,
    /// fertility, or birth changes.
    /// </summary>
    public sealed class PregnancyProgressBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<string, int> _lastObservedState =
            new Dictionary<string, int>();
        private readonly WithdrawalDiagnosticsCoordinator _withdrawalDiagnostics =
            new WithdrawalDiagnosticsCoordinator();

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.OnChildConceivedEvent.AddNonSerializedListener(this, OnChildConceived);
            CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Pregnancy progress remains derived from Bannerlord. Only the withdrawal
            // diagnostic event ledger is persisted to prevent duplicate requests.
            _withdrawalDiagnostics.SyncData(dataStore);
        }

        private void OnGameLoadFinished()
        {
            _lastObservedState.Clear();
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "Pregnant Lords Expanded: Milestone 2B loaded - withdrawal diagnostics are active."));

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                Observe(hero);
            }
        }

        private void OnChildConceived(Hero mother)
        {
            Observe(mother);
        }

        private void OnGivenBirth(Hero mother, List<Hero> children, int stillbornCount)
        {
            int childCount = children != null ? children.Count : 0;
            string motherName = mother != null ? mother.Name.ToString() : "<unknown mother>";

            DiagnosticLog.Info(
                motherName + " pregnancy ended in birth: " + childCount
                + " child(ren) reported, " + stillbornCount + " stillborn.");

            _withdrawalDiagnostics.Close(
                mother,
                stillbornCount > 0
                    ? "birth with stillborn child count reported; no causal blame assigned"
                    : "birth");

            Forget(mother);
        }

        private void OnHeroKilled(
            Hero victim,
            Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            _withdrawalDiagnostics.Close(victim, "maternal death: " + detail);
            Forget(victim);
        }

        private void OnDailyTickHero(Hero hero)
        {
            Observe(hero);
        }

        private void Observe(Hero hero)
        {
            if (hero == null || !hero.IsFemale)
            {
                return;
            }

            PregnancyProgressResult result = PregnancyProgressService.Instance.GetProgress(hero);
            string heroId = HeroKey(hero);

            if (!result.IsPregnant)
            {
                int endedPregnancyState;
                if (_lastObservedState.TryGetValue(heroId, out endedPregnancyState))
                {
                    string previousDescription = endedPregnancyState > 0
                        ? "normalized month " + endedPregnancyState
                        : "an unknown-progress state";

                    DiagnosticLog.Info(
                        hero.Name + " pregnancy is no longer active after "
                        + previousDescription + "; no birth event was observed.");

                    _withdrawalDiagnostics.Close(hero, "pregnancy no longer active");
                    _lastObservedState.Remove(heroId);
                }

                return;
            }

            int observedState = result.HasKnownProgress ? result.ApproximateMonth : 0;
            int previousState;
            if (_lastObservedState.TryGetValue(heroId, out previousState)
                && previousState == observedState)
            {
                return;
            }

            _lastObservedState[heroId] = observedState;
            if (result.HasKnownProgress)
            {
                DiagnosticLog.Info(
                    hero.Name + " pregnancy observed at normalized month "
                    + result.ApproximateMonth + " via " + result.DataSource + ".");

                _withdrawalDiagnostics.Observe(hero, result.ApproximateMonth);
            }
            else
            {
                DiagnosticLog.WarnOnce(
                    "unknown-progress:" + heroId,
                    hero.Name + " is pregnant, but progress is unknown: " + result.FailureReason);
            }

            CompatibilityHub.Instance.PublishObservation(result);
        }

        private void Forget(Hero hero)
        {
            if (hero != null)
            {
                _lastObservedState.Remove(HeroKey(hero));
            }
        }

        private static string HeroKey(Hero hero)
        {
            if (!string.IsNullOrEmpty(hero.StringId))
            {
                return hero.StringId;
            }

            return hero.GetHashCode().ToString();
        }
    }
}
