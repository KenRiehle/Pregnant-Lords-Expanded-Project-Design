using System.Collections.Generic;
using System.Globalization;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Integrations;
using PregnantLordsExpanded.Pregnancy;
using PregnantLordsExpanded.Withdrawal;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Pregnancy observation and Milestone 2 withdrawal behavior. Approved NPC withdrawal
    /// travel and service restrictions remain intact. Combat pregnancy-loss evaluation is
    /// now staged from Bannerlord's HeroWounded event and resolved only after the owning
    /// map event ends, keeping pregnancy-ledger mutation out of mission casualty handling.
    /// The locked default risks are 5% in months 3-6, 15% in months 7-8, and 35% in month 9.
    /// </summary>
    public sealed class PregnancyProgressBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<string, int> _lastObservedState =
            new Dictionary<string, int>();
        private readonly WithdrawalDiagnosticsCoordinator _withdrawalDiagnostics =
            new WithdrawalDiagnosticsCoordinator();
        private readonly Dictionary<Hero, MapEvent> _pendingCombatPregnancyWounds =
            new Dictionary<Hero, MapEvent>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.OnChildConceivedEvent.AddNonSerializedListener(this, OnChildConceived);
            CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.HeroWounded.AddNonSerializedListener(this, OnHeroWounded);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.CanHeroLeadPartyEvent.AddNonSerializedListener(
                this,
                new ReferenceAction<Hero, bool>(OnCanHeroLeadParty));
            CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(
                this,
                OnPartyJoinedArmy);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(
                this,
                OnHourlyTickParty);
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
            _pendingCombatPregnancyWounds.Clear();
            _withdrawalDiagnostics.ResetSessionPrompts();
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "Pregnant Lords Expanded: combat pregnancy-loss staging is active with default 5% / 15% / 35% month-based risk; approved withdrawal travel and service restrictions remain active."));

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
            if (victim != null)
            {
                _pendingCombatPregnancyWounds.Remove(victim);
            }

            _withdrawalDiagnostics.Close(victim, "maternal death: " + detail);
            Forget(victim);
        }

        private void OnHeroWounded(Hero woundedHero)
        {
            if (woundedHero == null || !woundedHero.IsFemale || !woundedHero.IsPregnant)
            {
                return;
            }

            MobileParty party = woundedHero.PartyBelongedTo;
            MapEvent mapEvent = party != null ? party.Party.MapEvent : null;
            if (mapEvent == null || mapEvent.EventType == MapEvent.BattleTypes.None)
            {
                return;
            }

            _pendingCombatPregnancyWounds[woundedHero] = mapEvent;

            DiagnosticLog.Info(
                woundedHero.Name
                + " sustained a qualifying combat wound while pregnant; PLE staged"
                + " one pregnancy-loss evaluation for map event " + mapEvent.EventType + ".");
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || _pendingCombatPregnancyWounds.Count == 0)
            {
                return;
            }

            List<Hero> mothersToEvaluate = new List<Hero>();
            foreach (KeyValuePair<Hero, MapEvent> pending in _pendingCombatPregnancyWounds)
            {
                if (pending.Value == mapEvent)
                {
                    mothersToEvaluate.Add(pending.Key);
                }
            }

            for (int index = 0; index < mothersToEvaluate.Count; index++)
            {
                Hero mother = mothersToEvaluate[index];
                _pendingCombatPregnancyWounds.Remove(mother);
                EvaluateCombatPregnancyLoss(mother, mapEvent);
            }
        }

        private void EvaluateCombatPregnancyLoss(Hero mother, MapEvent mapEvent)
        {
            if (mother == null || !mother.IsAlive || !mother.IsPregnant)
            {
                return;
            }

            PregnancyProgressResult progress = PregnancyProgressService.Instance.GetProgress(mother);
            if (!progress.IsPregnant || !progress.HasKnownProgress)
            {
                DiagnosticLog.WarnOnce(
                    "combat-loss-unknown-progress:" + HeroKey(mother),
                    mother.Name
                    + " was wounded in combat while pregnant, but PLE could not resolve"
                    + " a normalized pregnancy month; no pregnancy-loss roll was made.");
                return;
            }

            CombatPregnancyLossSettings settings =
                CombatPregnancyLossRuntimeSettings.Snapshot();
            CombatPregnancyNativeProtection nativeProtection =
                GetNativeCombatPregnancyProtection(mother);

            CombatPregnancyLossResult result = CombatPregnancyLossCalculator.Calculate(
                settings,
                new CombatPregnancyLossInput
                {
                    IsPregnant = mother.IsPregnant,
                    QualifyingCombatWound = true,
                    BirthAndAgingEnabled = !CampaignOptions.IsLifeDeathCycleDisabled,
                    NormalizedMonth = progress.ApproximateMonth,
                    NativeProtection = nativeProtection
                });

            WithdrawalResponsibility responsibility =
                _withdrawalDiagnostics.GetCurrentResponsibility(mother);

            if (!result.ShouldRoll)
            {
                DiagnosticLog.Info(
                    "PLE combat pregnancy-loss evaluation: mother=" + mother.Name
                    + ", month=" + progress.ApproximateMonth
                    + ", battleType=" + mapEvent.EventType
                    + ", baseRisk=" + FormatPercent(result.BaseRiskPercent)
                    + ", effectiveRisk=" + FormatPercent(result.EffectiveRiskPercent)
                    + ", combatLossEnabled=" + settings.EnableCombatPregnancyLoss
                    + ", respectNative=" + settings.RespectBannerlordBattleDeathSettings
                    + ", birthAndAging=" + (!CampaignOptions.IsLifeDeathCycleDisabled)
                    + ", heroBattleDeath=" + CampaignOptions.BattleDeath
                    + ", clanMemberBattleDeath=" + CampaignOptions.ClanMemberDeathChance
                    + ", nativeProtection=" + nativeProtection
                    + ", responsibility=" + responsibility
                    + ", result=Suppressed, reason=" + result.SuppressionReason);
                return;
            }

            double roll = MBRandom.RandomFloat * 100.0;
            bool pregnancyLost = result.IsPregnancyLossRoll(roll);

            DiagnosticLog.Info(
                "PLE combat pregnancy-loss roll: mother=" + mother.Name
                + ", month=" + progress.ApproximateMonth
                + ", battleType=" + mapEvent.EventType
                + ", baseRisk=" + FormatPercent(result.BaseRiskPercent)
                + ", effectiveRisk=" + FormatPercent(result.EffectiveRiskPercent)
                + ", combatLossEnabled=" + settings.EnableCombatPregnancyLoss
                + ", respectNative=" + settings.RespectBannerlordBattleDeathSettings
                + ", birthAndAging=" + (!CampaignOptions.IsLifeDeathCycleDisabled)
                + ", heroBattleDeath=" + CampaignOptions.BattleDeath
                + ", clanMemberBattleDeath=" + CampaignOptions.ClanMemberDeathChance
                + ", nativeProtection=" + nativeProtection
                + ", wounded=True"
                + ", responsibility=" + responsibility
                + ", roll=" + FormatPercent(roll)
                + ", result=" + (pregnancyLost ? "Loss" : "Preserved") + ".");

            if (!pregnancyLost)
            {
                return;
            }

            string failureReason;
            if (!NativePregnancyLossService.TryEndPregnancy(mother, out failureReason))
            {
                DiagnosticLog.WarnOnce(
                    "combat-loss-native-failure:" + HeroKey(mother),
                    "PLE rolled a combat pregnancy loss for " + mother.Name
                    + " but did not alter the pregnancy because the native pregnancy"
                    + " record could not be removed safely: " + failureReason + ".");
                return;
            }

            Hero responsibleHero =
                _withdrawalDiagnostics.GetCurrentResponsibleHero(mother);
            string noticeDescription;
            string noticeSuppressionReason;
            bool noticeAdded = PregnancyLossNotificationService.TryShowCombatLossNotice(
                mother,
                responsibleHero,
                responsibility,
                out noticeDescription,
                out noticeSuppressionReason);

            if (!noticeAdded)
            {
                DiagnosticLog.Info(
                    "PLE pregnancy-loss map notice suppressed for " + mother.Name
                    + ": " + noticeSuppressionReason + ".");
            }

            DiagnosticLog.Info(
                mother.Name + " combat pregnancy loss completed at normalized month "
                + progress.ApproximateMonth + "; responsibility=" + responsibility
                + ". Native pregnancy record removed and Hero.IsPregnant cleared.");

            _withdrawalDiagnostics.Close(
                mother,
                "combat pregnancy loss; responsibility=" + responsibility);
            Forget(mother);
        }

        private static CombatPregnancyNativeProtection GetNativeCombatPregnancyProtection(
            Hero mother)
        {
            if (CampaignOptions.BattleDeath == CampaignOptions.Difficulty.VeryEasy)
            {
                return CombatPregnancyNativeProtection.Disabled;
            }

            if (mother == Hero.MainHero
                && CampaignOptions.BattleDeath == CampaignOptions.Difficulty.Easy)
            {
                return CombatPregnancyNativeProtection.Disabled;
            }

            if (mother != null && mother.Clan == Clan.PlayerClan)
            {
                if (CampaignOptions.ClanMemberDeathChance
                    == CampaignOptions.Difficulty.VeryEasy)
                {
                    return CombatPregnancyNativeProtection.Disabled;
                }

                if (CampaignOptions.ClanMemberDeathChance
                    == CampaignOptions.Difficulty.Easy)
                {
                    return CombatPregnancyNativeProtection.ReducedByHalf;
                }
            }

            return CombatPregnancyNativeProtection.Normal;
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private void OnCanHeroLeadParty(Hero hero, ref bool result)
        {
            if (!result || hero == null)
            {
                return;
            }

            if (_withdrawalDiagnostics.IsPregnancyServiceRestricted(hero))
            {
                result = false;
            }
        }

        private void OnPartyJoinedArmy(MobileParty party)
        {
            _withdrawalDiagnostics.OnRestrictedPartyJoinedArmy(party);
        }

        private void OnHourlyTickParty(MobileParty party)
        {
            _withdrawalDiagnostics.OnRestrictedPartyHourlyTick(party);
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
            bool observationUnchanged = _lastObservedState.TryGetValue(
                    heroId,
                    out previousState)
                && previousState == observedState;

            // Protected-rest transitions can occur within the same normalized month,
            // so withdrawal diagnostics must observe known pregnancies every day.
            // Its persisted ledgers suppress duplicate warnings and petitions.
            if (result.HasKnownProgress)
            {
                _withdrawalDiagnostics.Observe(hero, result.ApproximateMonth);
            }

            if (observationUnchanged)
            {
                return;
            }

            _lastObservedState[heroId] = observedState;
            if (result.HasKnownProgress)
            {
                DiagnosticLog.Info(
                    hero.Name + " pregnancy observed at normalized month "
                    + result.ApproximateMonth + " via " + result.DataSource + ".");
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
