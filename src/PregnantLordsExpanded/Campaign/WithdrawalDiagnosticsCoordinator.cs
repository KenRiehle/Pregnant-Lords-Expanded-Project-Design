using System;
using System.Collections.Generic;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Withdrawal;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Milestone 2B-2D campaign adapter. It records warnings, petitions, authority,
    /// AI decisions, responsibility, and transitions into or out of protected rest.
    /// Milestone 2D applies an AI commander's cumulative denial penalty exactly once;
    /// it still never moves a hero or performs a withdrawal action.
    /// </summary>
    internal sealed class WithdrawalDiagnosticsCoordinator
    {
        private const string SavePrefix = "PLE_M2B_";
        private const string ProtectedRestSavePrefix = "PLE_M2C_";
        private const string RelationshipSavePrefix = "PLE_M2D_";

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
        private Dictionary<string, int> _appliedRelationPenaltyByPregnancyAndAuthority =
            new Dictionary<string, int>();
        private Dictionary<string, string> _lastResponsibleHeroByPregnancy =
            new Dictionary<string, string>();
        private Dictionary<string, int> _lastResponsibilityByPregnancy =
            new Dictionary<string, int>();
        private Dictionary<string, int> _protectedRestStateByPregnancy =
            new Dictionary<string, int>();
        private Dictionary<string, string> _protectedSettlementByPregnancy =
            new Dictionary<string, string>();
        private Dictionary<string, int> _departureSequenceByPregnancy =
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
            dataStore.SyncData(
                ProtectedRestSavePrefix + "StateByPregnancy",
                ref _protectedRestStateByPregnancy);
            dataStore.SyncData(
                ProtectedRestSavePrefix + "SettlementByPregnancy",
                ref _protectedSettlementByPregnancy);
            dataStore.SyncData(
                ProtectedRestSavePrefix + "DepartureSequenceByPregnancy",
                ref _departureSequenceByPregnancy);
            dataStore.SyncData(
                RelationshipSavePrefix + "AppliedRelationPenaltyByPregnancyAndAuthority",
                ref _appliedRelationPenaltyByPregnancyAndAuthority);

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
            string motherId = HeroKey(mother);
            string pregnancyKey = GetOrCreatePregnancyKey(motherId);
            ProtectedRestTransitionResult protectedRestTransition =
                ObserveProtectedRestTransition(
                    mother,
                    normalizedMonth,
                    pregnancyKey,
                    authorityContext);

            if (!authorityContext.IsCampaigning
                || authorityContext.IsPrisoner
                || authorityContext.IsResting
                || protectedRestTransition.NextState == ProtectedRestState.ProtectedDefense)
            {
                // A resting or captive hero has no withdrawal decision to make. Do not
                // mark the month as processed, because she may resume campaigning and
                // require the current month's petition later. Defending the protected
                // settlement is permitted and is not treated as resumed campaigning.
                return;
            }

            int highestProcessedMonth;
            bool monthAlreadyProcessed = _highestProcessedMonthByPregnancy.TryGetValue(
                    pregnancyKey,
                    out highestProcessedMonth)
                && highestProcessedMonth >= normalizedMonth;
            bool departureRequiresImmediatePetition =
                protectedRestTransition.RequiresImmediatePetition
                && normalizedMonth >= settings.FormalPetitionMonth;

            if (monthAlreadyProcessed && !departureRequiresImmediatePetition)
            {
                return;
            }

            if (!monthAlreadyProcessed)
            {
                _highestProcessedMonthByPregnancy[pregnancyKey] = normalizedMonth;
            }

            WithdrawalAuthorityResult authority = WithdrawalAuthorityResolver.Resolve(
                authorityContext,
                settings.IndependentAuthorityMode);

            if (stage.IsWarning)
            {
                LogWarning(mother, normalizedMonth, authority);
                return;
            }

            int departureSequence;
            _departureSequenceByPregnancy.TryGetValue(
                pregnancyKey,
                out departureSequence);
            string requestKey = departureRequiresImmediatePetition
                ? pregnancyKey + "|departure:" + departureSequence
                    + "|month:" + normalizedMonth
                : pregnancyKey + "|month:" + normalizedMonth;
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
            int relationshipChangeApplied = 0;
            int appliedCumulativeRelationshipPenalty = 0;
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

                int alreadyAppliedRelationPenalty;
                _appliedRelationPenaltyByPregnancyAndAuthority.TryGetValue(
                    liabilityKey,
                    out alreadyAppliedRelationPenalty);
                int pendingRelationshipPenalty =
                    CommanderRelationPenaltyCalculator.GetPendingPenalty(
                        normalizedMonth,
                        alreadyAppliedRelationPenalty);

                appliedCumulativeRelationshipPenalty = alreadyAppliedRelationPenalty;
                if (pendingRelationshipPenalty < 0
                    && TryApplyCommanderRelationshipPenalty(
                        mother,
                        authorityHero,
                        liabilityKey,
                        pendingRelationshipPenalty,
                        targetLiability))
                {
                    relationshipChangeApplied = pendingRelationshipPenalty;
                    appliedCumulativeRelationshipPenalty = targetLiability;
                    _appliedRelationPenaltyByPregnancyAndAuthority[liabilityKey] =
                        targetLiability;
                }
            }

            DiagnosticLog.Info(
                mother.Name + " withdrawal petition at normalized month "
                + normalizedMonth + ": authority=" + authorityHero.Name
                + " (" + authority.Kind + "), decision=" + decisionResult.Decision
                + ", responsibility=" + responsibility
                + ", target liability=" + targetLiability
                + ", newly recorded liability=" + additionalLiability
                + ", relationship change applied=" + relationshipChangeApplied
                + ", applied cumulative relationship penalty="
                + appliedCumulativeRelationshipPenalty
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
            _protectedRestStateByPregnancy.Remove(pregnancyKey);
            _protectedSettlementByPregnancy.Remove(pregnancyKey);
            _departureSequenceByPregnancy.Remove(pregnancyKey);
            RemoveKeysWithPrefix(_decisionByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(_authorityByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(
                _liabilityByPregnancyAndAuthority,
                pregnancyKey + "|");
            RemoveKeysWithPrefix(
                _appliedRelationPenaltyByPregnancyAndAuthority,
                pregnancyKey + "|");
        }

        private static bool TryApplyCommanderRelationshipPenalty(
            Hero mother,
            Hero authority,
            string liabilityKey,
            int relationshipChange,
            int targetCumulativePenalty)
        {
            try
            {
                int before = mother.GetRelation(authority);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    mother,
                    authority,
                    relationshipChange,
                    false);
                int after = mother.GetRelation(authority);

                DiagnosticLog.Info(
                    mother.Name + " relationship consequence applied against "
                    + authority.Name + " for a denied withdrawal petition: requested change="
                    + relationshipChange + ", target cumulative penalty="
                    + targetCumulativePenalty + ", effective relation before=" + before
                    + ", effective relation after=" + after + ".");
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.WarnOnce(
                    "m2d-relation:" + liabilityKey + ":" + targetCumulativePenalty,
                    "Could not apply the commander relationship consequence for "
                    + mother.Name + " and " + authority.Name
                    + "; the campaign will continue without recording it as applied. "
                    + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
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

        private ProtectedRestTransitionResult ObserveProtectedRestTransition(
            Hero mother,
            int normalizedMonth,
            string pregnancyKey,
            WithdrawalAuthorityContext authorityContext)
        {
            int previousStateValue;
            _protectedRestStateByPregnancy.TryGetValue(
                pregnancyKey,
                out previousStateValue);

            string protectedSettlementId;
            _protectedSettlementByPregnancy.TryGetValue(
                pregnancyKey,
                out protectedSettlementId);

            string observedSettlementId = CurrentSettlementId(mother);
            MobileParty party = mother.PartyBelongedTo;
            bool isDefendingProtectedSettlement = party != null
                && party.BesiegedSettlement != null
                && !string.IsNullOrWhiteSpace(observedSettlementId);

            ProtectedRestState observedState = authorityContext.IsPrisoner
                ? ProtectedRestState.Prisoner
                : authorityContext.IsResting
                    ? ProtectedRestState.ProtectedRest
                    : authorityContext.IsCampaigning
                        ? ProtectedRestState.Campaigning
                        : ProtectedRestState.Unavailable;

            ProtectedRestTransitionResult result =
                ProtectedRestTransitionCalculator.Calculate(
                    new ProtectedRestTransitionInput
                    {
                        PreviousState = (ProtectedRestState)previousStateValue,
                        ObservedState = observedState,
                        ProtectedSettlementId = protectedSettlementId,
                        ObservedSettlementId = observedSettlementId,
                        IsDefendingProtectedSettlement = isDefendingProtectedSettlement
                    });

            _protectedRestStateByPregnancy[pregnancyKey] = (int)result.NextState;
            if (string.IsNullOrWhiteSpace(result.ProtectedSettlementId))
            {
                _protectedSettlementByPregnancy.Remove(pregnancyKey);
            }
            else
            {
                _protectedSettlementByPregnancy[pregnancyKey] =
                    result.ProtectedSettlementId;
            }

            if (result.Transition == ProtectedRestTransitionKind.None)
            {
                return result;
            }

            if (result.Transition
                == ProtectedRestTransitionKind.PresumedVoluntaryDeparture)
            {
                int sequence;
                _departureSequenceByPregnancy.TryGetValue(pregnancyKey, out sequence);
                sequence++;
                _departureSequenceByPregnancy[pregnancyKey] = sequence;
                _lastResponsibilityByPregnancy[pregnancyKey] =
                    (int)WithdrawalResponsibility.VoluntaryRefusal;
                _lastResponsibleHeroByPregnancy[pregnancyKey] = HeroKey(mother);
            }
            else if (result.Responsibility
                == WithdrawalResponsibility.ForcedCircumstances)
            {
                _lastResponsibilityByPregnancy[pregnancyKey] =
                    (int)WithdrawalResponsibility.ForcedCircumstances;
                _lastResponsibleHeroByPregnancy.Remove(pregnancyKey);
            }

            LogProtectedRestTransition(
                mother,
                normalizedMonth,
                protectedSettlementId,
                observedSettlementId,
                result);
            return result;
        }

        private static void LogProtectedRestTransition(
            Hero mother,
            int normalizedMonth,
            string previousSettlementId,
            string observedSettlementId,
            ProtectedRestTransitionResult result)
        {
            string previousSettlement = string.IsNullOrWhiteSpace(previousSettlementId)
                ? "<unknown settlement>"
                : previousSettlementId;
            string currentSettlement = string.IsNullOrWhiteSpace(observedSettlementId)
                ? "<unknown settlement>"
                : observedSettlementId;

            switch (result.Transition)
            {
                case ProtectedRestTransitionKind.ProtectedRestEstablished:
                    DiagnosticLog.Info(
                        mother.Name + " established protected pregnancy rest at "
                        + currentSettlement + " during normalized month "
                        + normalizedMonth + "; no withdrawal petition is required.");
                    break;
                case ProtectedRestTransitionKind.ReturnedToProtectedRest:
                    DiagnosticLog.Info(
                        mother.Name + " returned to protected pregnancy rest at "
                        + currentSettlement + " during normalized month "
                        + normalizedMonth + ".");
                    break;
                case ProtectedRestTransitionKind.DefensiveMobilization:
                    DiagnosticLog.Info(
                        mother.Name + " mobilized to defend protected settlement "
                        + previousSettlement + " during normalized month "
                        + normalizedMonth
                        + "; this is lawful local defense, not voluntary campaigning,"
                        + " and no blame is assigned.");
                    break;
                case ProtectedRestTransitionKind.PresumedVoluntaryDeparture:
                    DiagnosticLog.Info(
                        mother.Name + " left protected pregnancy rest at "
                        + previousSettlement + " and resumed campaigning during normalized month "
                        + normalizedMonth + "; provisional responsibility="
                        + WithdrawalResponsibility.VoluntaryRefusal
                        + ", responsible hero=" + HeroKey(mother)
                        + ". No relationship change was applied.");
                    break;
                case ProtectedRestTransitionKind.ForcedRemoval:
                    DiagnosticLog.Info(
                        mother.Name + " was removed from protected pregnancy rest at "
                        + previousSettlement + " into captivity during normalized month "
                        + normalizedMonth + "; responsibility="
                        + WithdrawalResponsibility.ForcedCircumstances
                        + ", with no voluntary blame assigned.");
                    break;
                case ProtectedRestTransitionKind.UnresolvedDeparture:
                    DiagnosticLog.Info(
                        mother.Name + " left protected pregnancy rest at "
                        + previousSettlement + " during normalized month "
                        + normalizedMonth
                        + ", but no campaigning or captivity state was available;"
                        + " responsibility=" + WithdrawalResponsibility.ForcedCircumstances
                        + " pending better evidence, with no voluntary blame assigned.");
                    break;
            }
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
            _appliedRelationPenaltyByPregnancyAndAuthority =
                _appliedRelationPenaltyByPregnancyAndAuthority
                ?? new Dictionary<string, int>();
            _lastResponsibleHeroByPregnancy = _lastResponsibleHeroByPregnancy
                ?? new Dictionary<string, string>();
            _lastResponsibilityByPregnancy = _lastResponsibilityByPregnancy
                ?? new Dictionary<string, int>();
            _protectedRestStateByPregnancy = _protectedRestStateByPregnancy
                ?? new Dictionary<string, int>();
            _protectedSettlementByPregnancy = _protectedSettlementByPregnancy
                ?? new Dictionary<string, string>();
            _departureSequenceByPregnancy = _departureSequenceByPregnancy
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

        private static string CurrentSettlementId(Hero mother)
        {
            if (mother.CurrentSettlement != null)
            {
                return mother.CurrentSettlement.StringId;
            }

            MobileParty party = mother.PartyBelongedTo;
            return party != null && party.CurrentSettlement != null
                ? party.CurrentSettlement.StringId
                : string.Empty;
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
