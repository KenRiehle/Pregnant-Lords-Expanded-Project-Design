using System.Collections.Generic;
using System.Globalization;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Integrations;
using PregnantLordsExpanded.Pregnancy;
using PregnantLordsExpanded.Withdrawal;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
        private const int PregnantPrisonerRefusalRelationChange = -2;

        private Dictionary<string, int> _pregnantPrisonerPetitionMonthByMother =
            new Dictionary<string, int>();
        private Dictionary<string, string> _pregnantPrisonerDetentionCaptorByMother =
            new Dictionary<string, string>();
        private Dictionary<string, int> _pregnantPrisonerRefusalCountByMother =
            new Dictionary<string, int>();

        // UI state is session-only. Petition decisions themselves are save-persistent.
        private bool _pregnantPrisonerInquiryOpen;
        private Hero _activePregnantPrisonerPetitionMother;
        private int _activePregnantPrisonerPetitionMonth;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
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
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
                this,
                OnHourlyTick);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(
                this,
                OnHeroPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(
                this,
                OnHeroPrisonerReleased);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Pregnancy progress remains derived from Bannerlord. Withdrawal diagnostics
            // and the monthly pregnant-prisoner petition ledger are persisted so each
            // separately refused normalized pregnancy month is counted exactly once.
            _withdrawalDiagnostics.SyncData(dataStore);
            dataStore.SyncData(
                "PLE_M2DFC_PrisonerPetitionMonthByMother",
                ref _pregnantPrisonerPetitionMonthByMother);
            dataStore.SyncData(
                "PLE_M2DFC_PrisonerDetentionCaptorByMother",
                ref _pregnantPrisonerDetentionCaptorByMother);
            dataStore.SyncData(
                "PLE_M2DFC_PrisonerRefusalCountByMother",
                ref _pregnantPrisonerRefusalCountByMother);
            EnsurePregnantPrisonerCollections();

            // Build 04e1 did not persist a refusal count. When loading an older 04e1 save,
            // preserve at least one known prior refusal if a monthly petition had already
            // been resolved, rather than pretending the captivity episode is brand-new.
            foreach (string motherKey in _pregnantPrisonerPetitionMonthByMother.Keys)
            {
                if (!_pregnantPrisonerRefusalCountByMother.ContainsKey(motherKey))
                {
                    _pregnantPrisonerRefusalCountByMother[motherKey] = 1;
                }
            }
        }

        private enum PregnantPrisonerPetitionChoice
        {
            ChivalricMaternalRelease = 0,
            ContinueImprisonment = 1
        }

        private enum PregnantPrisonerPetitionStyle
        {
            CharmingDiplomat = 0,
            HonorableAppeal = 1,
            CalculatingCoercive = 2,
            DefiantWarrior = 3,
            YoungVulnerable = 4,
            ProudNoble = 5
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            DiagnosticLog.Info(
                "PLE pregnant-prisoner monthly audience petition system registered;"
                + " personality-driven petition language and honorifics active;"
                + " refusal relation change=" + PregnantPrisonerRefusalRelationChange
                + " per separately refused normalized pregnancy month.");
        }

        private static bool IsPregnantPlayerPrisoner(Hero prisoner)
        {
            if (prisoner == null
                || !prisoner.IsFemale
                || !prisoner.IsAlive
                || !prisoner.IsPregnant
                || !prisoner.IsPrisoner)
            {
                return false;
            }

            // Match Bannerlord's own player-prisoner release authority rather than
            // requiring object identity with PartyBase.MainParty. This covers the main
            // party, another player-clan captor party, and a player-owned settlement
            // dungeon while still excluding enemy/neutral captivity.
            PartyBase prisonerParty = prisoner.PartyBelongedToAsPrisoner;
            if (prisonerParty != null)
            {
                if (prisonerParty == PartyBase.MainParty)
                {
                    return true;
                }

                if (prisonerParty.Owner != null
                    && prisonerParty.Owner.Clan == Clan.PlayerClan)
                {
                    return true;
                }

                if (prisonerParty.IsSettlement
                    && prisonerParty.Settlement != null
                    && prisonerParty.Settlement.OwnerClan == Clan.PlayerClan)
                {
                    return true;
                }
            }

            return prisoner.CurrentSettlement != null
                && prisoner.CurrentSettlement.OwnerClan == Clan.PlayerClan;
        }

        private static string PrisonerPartyLabel(Hero prisoner)
        {
            if (prisoner == null || prisoner.PartyBelongedToAsPrisoner == null)
            {
                return "<none>";
            }

            PartyBase party = prisoner.PartyBelongedToAsPrisoner;
            if (party == PartyBase.MainParty)
            {
                return "main_party";
            }

            if (party.IsSettlement && party.Settlement != null)
            {
                return "settlement:" + party.Settlement.StringId;
            }

            if (party.IsMobile && party.MobileParty != null)
            {
                return "mobile_party:" + party.MobileParty.StringId;
            }

            return party.Name != null ? party.Name.ToString() : "<unknown>";
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (!IsPregnantPlayerPrisoner(prisoner))
            {
                return;
            }

            DiagnosticLog.Info(
                "PLE pregnant-prisoner captivity detected: mother=" + prisoner.Name
                + ", captor=" + Hero.MainHero.Name
                + ", prisonerParty=" + PrisonerPartyLabel(prisoner)
                + ". Her monthly release petition will be offered on the next safe hourly check.");
        }

        private void OnHeroPrisonerReleased(
            Hero prisoner,
            PartyBase party,
            IFaction capturerFaction,
            EndCaptivityDetail detail,
            bool showNotification)
        {
            if (prisoner == null)
            {
                return;
            }

            string key = HeroKey(prisoner);
            if (_pregnantPrisonerPetitionMonthByMother.ContainsKey(key)
                || _pregnantPrisonerDetentionCaptorByMother.ContainsKey(key))
            {
                DiagnosticLog.Info(
                    "PLE pregnant-prisoner captivity episode closed: mother="
                    + prisoner.Name + ", detail=" + detail + ".");
            }

            ClearPregnantPrisonerState(prisoner);
        }

        private void OnHourlyTick()
        {
            TryShowNextPregnantPrisonerPetition();
        }

        private void TryShowNextPregnantPrisonerPetition()
        {
            if (_pregnantPrisonerInquiryOpen)
            {
                return;
            }

            foreach (Hero prisoner in Hero.AllAliveHeroes)
            {
                if (!IsPregnantPlayerPrisoner(prisoner))
                {
                    continue;
                }

                PregnancyProgressResult progress =
                    PregnancyProgressService.Instance.GetProgress(prisoner);
                if (!progress.IsPregnant || !progress.HasKnownProgress)
                {
                    DiagnosticLog.WarnOnce(
                        "prisoner-petition-unknown-progress:" + HeroKey(prisoner),
                        "PLE deferred " + prisoner.Name
                        + "'s pregnant-prisoner release petition because the normalized"
                        + " pregnancy month could not yet be resolved.");
                    continue;
                }

                int normalizedMonth = progress.ApproximateMonth;
                int lastResolvedMonth;
                if (_pregnantPrisonerPetitionMonthByMother.TryGetValue(
                        HeroKey(prisoner),
                        out lastResolvedMonth)
                    && lastResolvedMonth == normalizedMonth)
                {
                    continue;
                }

                ShowPregnantPrisonerPetition(prisoner, normalizedMonth);
                return;
            }
        }

        private void ShowPregnantPrisonerPetition(Hero prisoner, int normalizedMonth)
        {
            _pregnantPrisonerInquiryOpen = true;
            _activePregnantPrisonerPetitionMother = prisoner;
            _activePregnantPrisonerPetitionMonth = normalizedMonth;

            try
            {
                int priorRefusals = GetPregnantPrisonerRefusalCount(prisoner);
                PregnantPrisonerPetitionStyle style = SelectPregnantPrisonerPetitionStyle(prisoner);
                string address = GetPlayerHonorificAddress();
                string spokenRequest = BuildPregnantPrisonerPetitionLine(
                    prisoner,
                    style,
                    address,
                    priorRefusals);

                string text = prisoner.Name
                    + " has requested an audience concerning her continued imprisonment."
                    + "\n\n\"" + spokenRequest + "\""
                    + "\n\nShe is in normalized pregnancy month " + normalizedMonth
                    + ". Refusing this month's petition will reduce her relation with you by "
                    + (-PregnantPrisonerRefusalRelationChange)
                    + ". If she remains imprisoned, she may petition again in a later"
                    + " normalized pregnancy month.";

                List<InquiryElement> choices = new List<InquiryElement>
                {
                    new InquiryElement(
                        PregnantPrisonerPetitionChoice.ChivalricMaternalRelease,
                        "Chivalric Maternal Release",
                        null,
                        true,
                        "Release " + prisoner.Name
                            + " immediately using Bannerlord's native prisoner-release action."),
                    new InquiryElement(
                        PregnantPrisonerPetitionChoice.ContinueImprisonment,
                        "Continue Imprisonment ("
                            + PregnantPrisonerRefusalRelationChange + " relation)",
                        null,
                        true,
                        "Refuse this month's request. The relation loss is applied once for"
                            + " this normalized pregnancy month and will not duplicate after reload.")
                };

                MultiSelectionInquiryData inquiry = new MultiSelectionInquiryData(
                    "Pregnant Prisoner Requests an Audience",
                    text,
                    choices,
                    false,
                    1,
                    1,
                    "Confirm Decision",
                    string.Empty,
                    ResolvePregnantPrisonerPetition,
                    null);

                DiagnosticLog.Info(
                    "PLE pregnant-prisoner petition queued: mother=" + prisoner.Name
                    + ", month=" + normalizedMonth
                    + ", captor=" + Hero.MainHero.Name
                    + ", prisonerParty=" + PrisonerPartyLabel(prisoner)
                    + ", style=" + style
                    + ", address=\"" + address + "\""
                    + ", priorRefusals=" + priorRefusals
                    + ", charm=" + prisoner.GetSkillValue(DefaultSkills.Charm)
                    + ", roguery=" + prisoner.GetSkillValue(DefaultSkills.Roguery)
                    + ", leadership=" + prisoner.GetSkillValue(DefaultSkills.Leadership)
                    + ", tactics=" + prisoner.GetSkillValue(DefaultSkills.Tactics)
                    + ", honor=" + prisoner.GetTraitLevel(DefaultTraits.Honor)
                    + ", mercy=" + prisoner.GetTraitLevel(DefaultTraits.Mercy)
                    + ", generosity=" + prisoner.GetTraitLevel(DefaultTraits.Generosity)
                    + ", valor=" + prisoner.GetTraitLevel(DefaultTraits.Valor)
                    + ", calculating=" + prisoner.GetTraitLevel(DefaultTraits.Calculating)
                    + ", age=" + ((int)prisoner.Age) + ".");

                // Required selection: no Escape/cancel path. Prioritize so the petition is
                // delivered as a player-facing event rather than requiring manual dialogue.
                MBInformationManager.ShowMultiSelectionInquiry(
                    inquiry,
                    true,
                    true);
            }
            catch (System.Exception exception)
            {
                _pregnantPrisonerInquiryOpen = false;
                _activePregnantPrisonerPetitionMother = null;
                _activePregnantPrisonerPetitionMonth = 0;
                DiagnosticLog.WarnOnce(
                    "prisoner-petition-inquiry:" + HeroKey(prisoner)
                        + ":" + normalizedMonth,
                    "PLE could not display " + prisoner.Name
                        + "'s pregnant-prisoner petition. It remains pending and will retry. "
                        + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void ResolvePregnantPrisonerPetition(List<InquiryElement> selected)
        {
            Hero prisoner = _activePregnantPrisonerPetitionMother;
            int petitionMonth = _activePregnantPrisonerPetitionMonth;

            _pregnantPrisonerInquiryOpen = false;
            _activePregnantPrisonerPetitionMother = null;
            _activePregnantPrisonerPetitionMonth = 0;

            if (prisoner == null
                || selected == null
                || selected.Count != 1
                || !(selected[0].Identifier is PregnantPrisonerPetitionChoice))
            {
                DiagnosticLog.WarnOnce(
                    "prisoner-petition-invalid-selection:"
                        + (prisoner != null ? HeroKey(prisoner) : "unknown")
                        + ":" + petitionMonth,
                    "PLE received an invalid pregnant-prisoner petition selection; the"
                        + " petition remains unresolved and will be retried.");
                return;
            }

            if (!IsPregnantPlayerPrisoner(prisoner))
            {
                DiagnosticLog.Info(
                    "PLE discarded a stale pregnant-prisoner petition for "
                    + prisoner.Name
                    + " because she is no longer a pregnant prisoner under player control.");
                ClearPregnantPrisonerState(prisoner);
                return;
            }

            PregnancyProgressResult progress =
                PregnancyProgressService.Instance.GetProgress(prisoner);
            if (!progress.IsPregnant
                || !progress.HasKnownProgress
                || progress.ApproximateMonth != petitionMonth)
            {
                DiagnosticLog.Info(
                    "PLE discarded a stale pregnant-prisoner petition for "
                    + prisoner.Name
                    + " because her pregnancy month changed before the decision was resolved."
                    + " A current petition will be offered on the next safe hourly check.");
                return;
            }

            PregnantPrisonerPetitionChoice choice =
                (PregnantPrisonerPetitionChoice)selected[0].Identifier;
            string key = HeroKey(prisoner);

            if (choice == PregnantPrisonerPetitionChoice.ChivalricMaternalRelease)
            {
                try
                {
                    EndCaptivityAction.ApplyByReleasedByChoice(prisoner, Hero.MainHero);
                    ClearPregnantPrisonerState(prisoner);
                    DiagnosticLog.Info(
                        "PLE pregnant-prisoner petition resolved: mother=" + prisoner.Name
                        + ", month=" + petitionMonth
                        + ", captor=" + Hero.MainHero.Name
                        + ", decision=ChivalricMaternalRelease, nativeRelease=True.");
                }
                catch (System.Exception exception)
                {
                    DiagnosticLog.WarnOnce(
                        "prisoner-petition-release:" + key + ":" + petitionMonth,
                        "PLE could not complete Chivalric Maternal Release for "
                            + prisoner.Name + "; the petition remains pending. "
                            + exception.GetType().Name + ": " + exception.Message);
                }

                return;
            }

            int relationBefore = prisoner.GetRelation(Hero.MainHero);
            ChangeRelationAction.ApplyPlayerRelation(
                prisoner,
                PregnantPrisonerRefusalRelationChange);
            int relationAfter = prisoner.GetRelation(Hero.MainHero);

            _pregnantPrisonerPetitionMonthByMother[key] = petitionMonth;
            _pregnantPrisonerDetentionCaptorByMother[key] = HeroKey(Hero.MainHero);
            int refusalCount = GetPregnantPrisonerRefusalCount(prisoner) + 1;
            _pregnantPrisonerRefusalCountByMother[key] = refusalCount;

            DiagnosticLog.Info(
                "PLE pregnant-prisoner petition resolved: mother=" + prisoner.Name
                + ", month=" + petitionMonth
                + ", captor=" + Hero.MainHero.Name
                + ", decision=ContinueImprisonment"
                + ", relationChange=" + PregnantPrisonerRefusalRelationChange
                + ", relationBefore=" + relationBefore
                + ", relationAfter=" + relationAfter
                + ", refusalCount=" + refusalCount
                + ", informed=True. This normalized pregnancy month is now resolved"
                + " and will not be penalized again after save/load.");
        }

        private int GetPregnantPrisonerRefusalCount(Hero prisoner)
        {
            if (prisoner == null)
            {
                return 0;
            }

            int count;
            return _pregnantPrisonerRefusalCountByMother.TryGetValue(HeroKey(prisoner), out count)
                ? count
                : 0;
        }

        private static PregnantPrisonerPetitionStyle SelectPregnantPrisonerPetitionStyle(
            Hero prisoner)
        {
            if (prisoner == null)
            {
                return PregnantPrisonerPetitionStyle.ProudNoble;
            }

            int charm = prisoner.GetSkillValue(DefaultSkills.Charm);
            int roguery = prisoner.GetSkillValue(DefaultSkills.Roguery);
            int leadership = prisoner.GetSkillValue(DefaultSkills.Leadership);
            int tactics = prisoner.GetSkillValue(DefaultSkills.Tactics);
            int honor = prisoner.GetTraitLevel(DefaultTraits.Honor);
            int mercy = prisoner.GetTraitLevel(DefaultTraits.Mercy);
            int generosity = prisoner.GetTraitLevel(DefaultTraits.Generosity);
            int valor = prisoner.GetTraitLevel(DefaultTraits.Valor);
            int calculating = prisoner.GetTraitLevel(DefaultTraits.Calculating);
            int age = (int)prisoner.Age;

            int charmingScore = (charm / 30)
                + PositiveTrait(mercy) * 2
                + PositiveTrait(generosity);
            int honorableScore = PositiveTrait(honor) * 5
                + PositiveTrait(mercy) * 4
                + PositiveTrait(generosity) * 2;
            int coerciveScore = PositiveTrait(calculating) * 5
                + (roguery / 40)
                + NegativeTrait(honor) * 2;
            int warriorScore = PositiveTrait(valor) * 6
                + (leadership / 60)
                + (tactics / 60);
            int youngScore = age <= 24 && valor <= 0
                ? 7 + (24 - age) / 3 + (charm / 80)
                : -1000;
            int proudScore = (prisoner.IsClanLeader ? 6 : 2)
                + (leadership / 80)
                + PositiveTrait(honor);

            PregnantPrisonerPetitionStyle style = PregnantPrisonerPetitionStyle.ProudNoble;
            int bestScore = proudScore;

            if (charmingScore > bestScore)
            {
                style = PregnantPrisonerPetitionStyle.CharmingDiplomat;
                bestScore = charmingScore;
            }

            if (honorableScore > bestScore)
            {
                style = PregnantPrisonerPetitionStyle.HonorableAppeal;
                bestScore = honorableScore;
            }

            if (coerciveScore > bestScore)
            {
                style = PregnantPrisonerPetitionStyle.CalculatingCoercive;
                bestScore = coerciveScore;
            }

            if (warriorScore > bestScore)
            {
                style = PregnantPrisonerPetitionStyle.DefiantWarrior;
                bestScore = warriorScore;
            }

            if (youngScore > bestScore)
            {
                style = PregnantPrisonerPetitionStyle.YoungVulnerable;
            }

            return style;
        }

        private static int PositiveTrait(int value)
        {
            return value > 0 ? value : 0;
        }

        private static int NegativeTrait(int value)
        {
            return value < 0 ? -value : 0;
        }

        private static string GetPlayerHonorificAddress()
        {
            Hero player = Hero.MainHero;
            if (player == null || player.Name == null)
            {
                return "my captor";
            }

            string displayName = player.Name.ToString();
            string[] recognizedTitles =
            {
                "High King ",
                "High Queen ",
                "Grand Prince ",
                "Grand Princess ",
                "King ",
                "Queen ",
                "Emperor ",
                "Empress ",
                "Lord ",
                "Lady ",
                "Duke ",
                "Duchess ",
                "Jarl ",
                "Prince ",
                "Princess ",
                "Khan ",
                "Khatun ",
                "Sultan ",
                "Sultana ",
                "Emir ",
                "Count ",
                "Countess ",
                "Baron ",
                "Baroness "
            };

            foreach (string title in recognizedTitles)
            {
                if (displayName.StartsWith(title, System.StringComparison.OrdinalIgnoreCase))
                {
                    return displayName;
                }
            }

            if (displayName.StartsWith("My Lord ", System.StringComparison.OrdinalIgnoreCase)
                || displayName.StartsWith("My Lady ", System.StringComparison.OrdinalIgnoreCase))
            {
                return displayName;
            }

            return (player.IsFemale ? "My Lady " : "My Lord ") + displayName;
        }

        private static string BuildPregnantPrisonerPetitionLine(
            Hero prisoner,
            PregnantPrisonerPetitionStyle style,
            string address,
            int priorRefusals)
        {
            int escalation = priorRefusals;
            if (escalation < 0)
            {
                escalation = 0;
            }
            else if (escalation > 4)
            {
                escalation = 4;
            }

            switch (style)
            {
                case PregnantPrisonerPetitionStyle.CharmingDiplomat:
                    switch (escalation)
                    {
                        case 0:
                            return address + ", surely there is little honor or advantage in keeping a woman with child confined. Grant me leave, and I will remember your courtesy.";
                        case 1:
                            return address + ", I ask you again as reasonably as I know how. Release me, and let this end as an act of courtesy between our houses.";
                        case 2:
                            return address + ", I have tried patience and good sense. There is still time for this to become a kindness remembered rather than a grievance endured.";
                        case 3:
                            return address + ", month after month I have appealed to your better judgment. Please do not turn stubbornness into a lasting quarrel between our families.";
                        default:
                            return address + ", you have refused every courteous appeal I have made. I can remain civil, but I will not pretend your continued refusal is either wise or gracious.";
                    }

                case PregnantPrisonerPetitionStyle.HonorableAppeal:
                    switch (escalation)
                    {
                        case 0:
                            return address + ", I ask you to show mercy, not only for my sake but for the innocent life I carry. Let me go with honor.";
                        case 1:
                            return address + ", I appeal to your honor again. Whatever quarrel lies between us, my unborn child has no part in it.";
                        case 2:
                            return address + ", I have asked twice for the mercy due to an innocent child. I ask you not to make continued confinement a stain upon this victory.";
                        case 3:
                            return address + ", I have borne your refusals with dignity. My patience remains, but my faith in your sense of honor is wearing thin.";
                        default:
                            return address + ", I will not beg again. If you still call yourself honorable, then prove it by ending this needless captivity before harm is done.";
                    }

                case PregnantPrisonerPetitionStyle.CalculatingCoercive:
                    switch (escalation)
                    {
                        case 0:
                            return address + ", you gain little by keeping me and risk much if my child suffers here. Release me now, and this need not become a debt between our houses.";
                        case 1:
                            return address + ", consider the arithmetic carefully. Every day you keep me buys you little and gives my clan another reason to remember your name.";
                        case 2:
                            return address + ", I have given you opportunities to settle this quietly. My family will hear exactly how long you knowingly kept me and my unborn child confined.";
                        case 3:
                            return address + ", this has ceased to be simple captivity. You are choosing a grievance that my clan can collect upon later.";
                        default:
                            return address + ", keep me if you insist. But when my clan settles accounts for what you knowingly risked, do not claim you were never warned.";
                    }

                case PregnantPrisonerPetitionStyle.DefiantWarrior:
                    switch (escalation)
                    {
                        case 0:
                            return address + ", release me and let this matter end with honor. I will not have my unborn child made part of our war.";
                        case 1:
                            return address + ", I asked you once as a prisoner. I ask again as a mother. Do not mistake my restraint for weakness.";
                        case 2:
                            return address + ", you have heard me twice. If harm comes to my child because you kept me here, my kin and I will remember who made that choice.";
                        case 3:
                            return address + ", I have endured your chains for months. Release me now, before this becomes a blood grievance rather than a battlefield victory.";
                        default:
                            return Hero.MainHero.Name + ", you mistake chains for power. If my child suffers because of your obstinacy, pray your walls are stronger than your pride.";
                    }

                case PregnantPrisonerPetitionStyle.YoungVulnerable:
                    switch (escalation)
                    {
                        case 0:
                            return address + ", please. Whatever quarrel exists between our houses, my child has no part in it. Let me go.";
                        case 1:
                            return address + ", I am asking again because I do not know what else to do. Please do not make my child endure this captivity with me.";
                        case 2:
                            return address + ", I have tried to be patient, but I am frightened for my child. I need you to understand that this is no longer a small thing to me.";
                        case 3:
                            return address + ", I have pleaded with you month after month. If you have any compassion left for my child, release me now.";
                        default:
                            return Hero.MainHero.Name + ", I have begged enough. If anything happens to my child after all these refusals, I will never forgive you.";
                    }

                default:
                    switch (escalation)
                    {
                        case 0:
                            return address + ", you hold a noblewoman and her unborn child. I ask that you release me and allow this matter to end with dignity.";
                        case 1:
                            return address + ", I renew my request. Continued captivity serves neither your reputation nor the standing of our houses.";
                        case 2:
                            return address + ", I have now asked more than once. Consider carefully what you gain by prolonging this indignity.";
                        case 3:
                            return address + ", your refusal has become an insult as well as a confinement. My house will remember how long you chose to continue it.";
                        default:
                            return Hero.MainHero.Name + ", you have had every chance to end this with dignity. Continue, and you may answer for the insult to my house as well as the danger to my child.";
                    }
            }
        }

        private void EnsurePregnantPrisonerCollections()
        {
            if (_pregnantPrisonerPetitionMonthByMother == null)
            {
                _pregnantPrisonerPetitionMonthByMother = new Dictionary<string, int>();
            }

            if (_pregnantPrisonerDetentionCaptorByMother == null)
            {
                _pregnantPrisonerDetentionCaptorByMother = new Dictionary<string, string>();
            }

            if (_pregnantPrisonerRefusalCountByMother == null)
            {
                _pregnantPrisonerRefusalCountByMother = new Dictionary<string, int>();
            }
        }

        private void ClearPregnantPrisonerState(Hero mother)
        {
            if (mother == null)
            {
                return;
            }

            string key = HeroKey(mother);
            _pregnantPrisonerPetitionMonthByMother.Remove(key);
            _pregnantPrisonerDetentionCaptorByMother.Remove(key);
            _pregnantPrisonerRefusalCountByMother.Remove(key);

            if (_activePregnantPrisonerPetitionMother == mother)
            {
                _pregnantPrisonerInquiryOpen = false;
                _activePregnantPrisonerPetitionMother = null;
                _activePregnantPrisonerPetitionMonth = 0;
            }
        }

        private void OnGameLoadFinished()
        {
            EnsurePregnantPrisonerCollections();
            _pregnantPrisonerInquiryOpen = false;
            _activePregnantPrisonerPetitionMother = null;
            _activePregnantPrisonerPetitionMonth = 0;
            _lastObservedState.Clear();
            _pendingCombatPregnancyWounds.Clear();
            _withdrawalDiagnostics.ResetSessionPrompts();
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "Pregnant Lords Expanded: combat pregnancy-loss staging is active with default 5% / 15% / 35% month-based risk; approved withdrawal travel, service restrictions, and monthly personality-driven pregnant-prisoner audience petitions (-2 relation per refused pregnancy month) are active."));

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

            ClearPregnantPrisonerState(mother);
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
            ClearPregnantPrisonerState(victim);
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
            ClearPregnantPrisonerState(mother);
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
                    ClearPregnantPrisonerState(hero);
                    _lastObservedState.Remove(heroId);
                }

                return;
            }

            if (!IsPregnantPlayerPrisoner(hero)
                && (_pregnantPrisonerPetitionMonthByMother.ContainsKey(heroId)
                    || _pregnantPrisonerDetentionCaptorByMother.ContainsKey(heroId)))
            {
                ClearPregnantPrisonerState(hero);
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
