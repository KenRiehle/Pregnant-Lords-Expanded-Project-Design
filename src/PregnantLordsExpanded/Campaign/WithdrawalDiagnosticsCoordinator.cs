using System;
using System.Collections.Generic;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Withdrawal;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Milestone 2B campaign adapter. It records warnings, petitions, authority,
    /// diagnostic AI decisions, and provisional liability. It never moves a hero
    /// or changes a relationship.
    /// </summary>
    internal sealed class WithdrawalDiagnosticsCoordinator
    {
        private const string SavePrefix = "PLE_M2B_";

        private Dictionary<string, int> _pregnancySequenceByMother =
            new Dictionary<string, int>();
        private Dictionary<string, string> _activePregnancyByMother =
            new Dictionary<string, string>();
        private Dictionary<string, int> _highestProcessedMonthByPregnancy =
            new Dictionary<string, int>();
        private Dictionary<string, int> _decisionByRequest =
            new Dictionary<string, int>();
        private Dictionary<string, string> _authorityByRequest =
            new Dictionary<string, string>();
        private Dictionary<string, int> _liabilityByPregnancyAndAuthority =
            new Dictionary<string, int>();
        private Dictionary<string, string> _lastResponsibleHeroByPregnancy =
            new Dictionary<string, string>();
        private Dictionary<string, int> _lastResponsibilityByPregnancy =
            new Dictionary<string, int>();

        public void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                SavePrefix + "PregnancySequenceByMother",
                ref _pregnancySequenceByMother);
            dataStore.SyncData(
                SavePrefix + "ActivePregnancyByMother",
                ref _activePregnancyByMother);
            dataStore.SyncData(
                SavePrefix + "HighestProcessedMonthByPregnancy",
                ref _highestProcessedMonthByPregnancy);
            dataStore.SyncData(
                SavePrefix + "DecisionByRequest",
                ref _decisionByRequest);
            dataStore.SyncData(
                SavePrefix + "AuthorityByRequest",
                ref _authorityByRequest);
            dataStore.SyncData(
                SavePrefix + "LiabilityByPregnancyAndAuthority",
                ref _liabilityByPregnancyAndAuthority);
            dataStore.SyncData(
                SavePrefix + "LastResponsibleHeroByPregnancy",
                ref _lastResponsibleHeroByPregnancy);
            dataStore.SyncData(
                SavePrefix + "LastResponsibilityByPregnancy",
                ref _lastResponsibilityByPregnancy);

            EnsureCollections();
        }

        public void Observe(Hero mother, int normalizedMonth)
        {
            if (mother == null)
            {
                return;
            }

            WithdrawalSettings settings = WithdrawalSettings.Default;
            WithdrawalMonthResult stage = WithdrawalMonthCalculator.Calculate(
                normalizedMonth,
                settings);
            if (stage.Stage == WithdrawalMonthStage.None)
            {
                return;
            }

            WithdrawalAuthorityContext authorityContext = CreateAuthorityContext(mother);
            if (!authorityContext.IsCampaigning
                || authorityContext.IsPrisoner
                || authorityContext.IsResting)
            {
                // A resting or captive hero has no withdrawal decision to make. Do not
                // mark the month as processed, because she may resume campaigning and
                // require the current month's petition later.
                return;
            }

            string motherId = HeroKey(mother);
            string pregnancyKey = GetOrCreatePregnancyKey(motherId);

            int highestProcessedMonth;
            if (_highestProcessedMonthByPregnancy.TryGetValue(
                    pregnancyKey,
                    out highestProcessedMonth)
                && highestProcessedMonth >= normalizedMonth)
            {
                return;
            }

            _highestProcessedMonthByPregnancy[pregnancyKey] = normalizedMonth;

            WithdrawalAuthorityResult authority = WithdrawalAuthorityResolver.Resolve(
                authorityContext,
                settings.IndependentAuthorityMode);

            if (stage.IsWarning)
            {
                LogWarning(mother, normalizedMonth, authority);
                return;
            }

            string requestKey = pregnancyKey + "|month:" + normalizedMonth;
            if (!authority.HasAuthority)
            {
                _decisionByRequest[requestKey] = (int)WithdrawalDecision.NoDecision;
                DiagnosticLog.Info(
                    mother.Name + " reached normalized month " + normalizedMonth
                    + " and requires a withdrawal petition, but no authority was resolved: "
                    + authority.NoDecisionReason);
                return;
            }

            _authorityByRequest[requestKey] = authority.AuthorityId;
            Hero authorityHero = ResolveAuthorityHero(mother, authority);
            if (authorityHero == null)
            {
                _decisionByRequest[requestKey] = (int)WithdrawalDecision.NoDecision;
                DiagnosticLog.Info(
                    mother.Name + " reached normalized month " + normalizedMonth
                    + "; authority id " + authority.AuthorityId
                    + " was calculated but could not be mapped to a living hero.");
                return;
            }

            if (authorityHero == Hero.MainHero)
            {
                _decisionByRequest[requestKey] = (int)WithdrawalDecision.NoDecision;
                DiagnosticLog.Info(
                    mother.Name + " requested withdrawal at normalized month "
                    + normalizedMonth + "; the player is the decision authority. "
                    + "Milestone 2B records the request without choosing for the player.");
                return;
            }

            bool selfAuthority = authority.Kind == WithdrawalAuthorityKind.Self;
            AiWithdrawalDecisionResult decisionResult =
                AiWithdrawalDecisionCalculator.Calculate(
                    CreateDecisionInput(mother, authorityHero, normalizedMonth, selfAuthority));
            WithdrawalResponsibility responsibility =
                WithdrawalResponsibilityCalculator.FromDecision(decisionResult.Decision);

            _decisionByRequest[requestKey] = (int)decisionResult.Decision;
            _lastResponsibilityByPregnancy[pregnancyKey] = (int)responsibility;

            if (responsibility == WithdrawalResponsibility.CommanderOverride
                || responsibility == WithdrawalResponsibility.VoluntaryRefusal)
            {
                _lastResponsibleHeroByPregnancy[pregnancyKey] = HeroKey(authorityHero);
            }

            int targetLiability = 0;
            int additionalLiability = 0;
            if (responsibility == WithdrawalResponsibility.CommanderOverride)
            {
                string liabilityKey = pregnancyKey + "|authority:" + HeroKey(authorityHero);
                int alreadyRecorded;
                _liabilityByPregnancyAndAuthority.TryGetValue(
                    liabilityKey,
                    out alreadyRecorded);

                targetLiability = CommanderLiabilityCalculator.GetDefaultCumulativePenalty(
                    normalizedMonth);
                additionalLiability = CommanderLiabilityCalculator.GetAdditionalPenalty(
                    alreadyRecorded,
                    targetLiability);

                if (additionalLiability < 0)
                {
                    _liabilityByPregnancyAndAuthority[liabilityKey] = targetLiability;
                }
            }

            DiagnosticLog.Info(
                mother.Name + " withdrawal petition at normalized month "
                + normalizedMonth + ": authority=" + authorityHero.Name
                + " (" + authority.Kind + "), decision=" + decisionResult.Decision
                + ", responsibility=" + responsibility
                + ", target liability=" + targetLiability
                + ", newly recorded liability=" + additionalLiability
                + "; " + decisionResult.Explanation + ".");
        }

        public void Close(Hero mother, string reason)
        {
            if (mother == null)
            {
                return;
            }

            string motherId = HeroKey(mother);
            string pregnancyKey;
            if (!_activePregnancyByMother.TryGetValue(motherId, out pregnancyKey))
            {
                return;
            }

            int responsibilityValue;
            string responsibleHeroId;
            bool hadResponsibility = _lastResponsibilityByPregnancy.TryGetValue(
                pregnancyKey,
                out responsibilityValue);
            _lastResponsibleHeroByPregnancy.TryGetValue(
                pregnancyKey,
                out responsibleHeroId);

            DiagnosticLog.Info(
                mother.Name + " withdrawal diagnostic state closed (" + reason + ")."
                + (hadResponsibility
                    ? " Last decision responsibility="
                        + (WithdrawalResponsibility)responsibilityValue
                        + "; most recent accountable hero="
                        + (string.IsNullOrWhiteSpace(responsibleHeroId)
                            ? "<none>"
                            : responsibleHeroId) + "."
                    : string.Empty));

            _activePregnancyByMother.Remove(motherId);
            _highestProcessedMonthByPregnancy.Remove(pregnancyKey);
            _lastResponsibleHeroByPregnancy.Remove(pregnancyKey);
            _lastResponsibilityByPregnancy.Remove(pregnancyKey);
            RemoveKeysWithPrefix(_decisionByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(_authorityByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(
                _liabilityByPregnancyAndAuthority,
                pregnancyKey + "|");
        }

        private static AiWithdrawalDecisionInput CreateDecisionInput(
            Hero mother,
            Hero authority,
            int normalizedMonth,
            bool selfAuthority)
        {
            MobileParty party = mother.PartyBelongedTo;
            bool hasReplacement = party != null
                && party.Army != null
                && party.Army.LeaderPartyAndAttachedPartiesCount > 1;

            return new AiWithdrawalDecisionInput
            {
                NormalizedMonth = normalizedMonth,
                IsSelfAuthority = selfAuthority,
                MercyLevel = authority.GetTraitLevel(DefaultTraits.Mercy),
                HonorLevel = authority.GetTraitLevel(DefaultTraits.Honor),
                ValorLevel = authority.GetTraitLevel(DefaultTraits.Valor),
                CalculatingLevel = authority.GetTraitLevel(DefaultTraits.Calculating),
                RelationWithMother = selfAuthority ? 0 : authority.GetRelation(mother),
                HasReplacement = hasReplacement,
                HasNearbyFriendlyProtection = party != null
                    && party.CurrentSettlement != null,
                HasHighDynasticRisk = mother.IsClanLeader,
                IsMilitaryEmergency = party != null
                    && party.BesiegedSettlement != null
            };
        }

        private static WithdrawalAuthorityContext CreateAuthorityContext(Hero mother)
        {
            MobileParty party = mother.PartyBelongedTo;
            Army army = party != null ? party.Army : null;
            Hero partyLeader = party != null ? party.LeaderHero : null;
            Hero armyLeader = army != null && army.LeaderParty != null
                ? army.LeaderParty.LeaderHero
                : null;
            Hero clanLeader = mother.Clan != null ? mother.Clan.Leader : null;
            Hero kingdomRuler = mother.Clan != null && mother.Clan.Kingdom != null
                ? mother.Clan.Kingdom.Leader
                : null;

            return new WithdrawalAuthorityContext
            {
                MotherId = HeroKey(mother),
                IsCampaigning = party != null,
                IsPrisoner = mother.IsPrisoner,
                IsResting = party == null && mother.CurrentSettlement != null,
                IsArmyMember = army != null,
                ArmyLeaderId = HeroKeyOrEmpty(armyLeader),
                PartyLeaderId = HeroKeyOrEmpty(partyLeader),
                LeadsIndependentParty = party != null
                    && army == null
                    && partyLeader == mother,
                ClanLeaderId = HeroKeyOrEmpty(clanLeader ?? mother),
                KingdomRulerId = HeroKeyOrEmpty(kingdomRuler)
            };
        }

        private static Hero ResolveAuthorityHero(
            Hero mother,
            WithdrawalAuthorityResult authority)
        {
            if (authority.Kind == WithdrawalAuthorityKind.Self)
            {
                return mother;
            }

            MobileParty party = mother.PartyBelongedTo;
            if (authority.Kind == WithdrawalAuthorityKind.ArmyLeader)
            {
                return party != null
                    && party.Army != null
                    && party.Army.LeaderParty != null
                    ? party.Army.LeaderParty.LeaderHero
                    : null;
            }

            if (authority.Kind == WithdrawalAuthorityKind.PartyLeader)
            {
                return party != null ? party.LeaderHero : null;
            }

            if (authority.Kind == WithdrawalAuthorityKind.ClanLeader)
            {
                return mother.Clan != null ? mother.Clan.Leader : mother;
            }

            if (authority.Kind == WithdrawalAuthorityKind.KingdomRuler)
            {
                return mother.Clan != null && mother.Clan.Kingdom != null
                    ? mother.Clan.Kingdom.Leader
                    : null;
            }

            return null;
        }

        private void LogWarning(
            Hero mother,
            int normalizedMonth,
            WithdrawalAuthorityResult authority)
        {
            string authorityText = authority.HasAuthority
                ? authority.AuthorityId + " (" + authority.Kind + ")"
                : "<unresolved: " + authority.NoDecisionReason + ">";

            DiagnosticLog.Info(
                mother.Name + " entered normalized month " + normalizedMonth
                + ": advance withdrawal warning; authority=" + authorityText + ".");
        }

        private string GetOrCreatePregnancyKey(string motherId)
        {
            string existing;
            if (_activePregnancyByMother.TryGetValue(motherId, out existing))
            {
                return existing;
            }

            int sequence;
            _pregnancySequenceByMother.TryGetValue(motherId, out sequence);
            sequence++;
            _pregnancySequenceByMother[motherId] = sequence;

            string pregnancyKey = motherId + "#" + sequence;
            _activePregnancyByMother[motherId] = pregnancyKey;
            return pregnancyKey;
        }

        private void EnsureCollections()
        {
            _pregnancySequenceByMother = _pregnancySequenceByMother
                ?? new Dictionary<string, int>();
            _activePregnancyByMother = _activePregnancyByMother
                ?? new Dictionary<string, string>();
            _highestProcessedMonthByPregnancy = _highestProcessedMonthByPregnancy
                ?? new Dictionary<string, int>();
            _decisionByRequest = _decisionByRequest
                ?? new Dictionary<string, int>();
            _authorityByRequest = _authorityByRequest
                ?? new Dictionary<string, string>();
            _liabilityByPregnancyAndAuthority = _liabilityByPregnancyAndAuthority
                ?? new Dictionary<string, int>();
            _lastResponsibleHeroByPregnancy = _lastResponsibleHeroByPregnancy
                ?? new Dictionary<string, string>();
            _lastResponsibilityByPregnancy = _lastResponsibilityByPregnancy
                ?? new Dictionary<string, int>();
        }

        private static void RemoveKeysWithPrefix<T>(
            Dictionary<string, T> dictionary,
            string prefix)
        {
            var keysToRemove = new List<string>();
            foreach (string key in dictionary.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (string key in keysToRemove)
            {
                dictionary.Remove(key);
            }
        }

        private static string HeroKeyOrEmpty(Hero hero)
        {
            return hero != null ? HeroKey(hero) : string.Empty;
        }

        private static string HeroKey(Hero hero)
        {
            if (hero != null && !string.IsNullOrEmpty(hero.StringId))
            {
                return hero.StringId;
            }

            return hero != null ? hero.GetHashCode().ToString() : string.Empty;
        }
    }
}
