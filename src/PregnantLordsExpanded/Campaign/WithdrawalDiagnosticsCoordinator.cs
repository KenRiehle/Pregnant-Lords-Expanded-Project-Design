using System;
using System.Collections.Generic;
using PregnantLordsExpanded.Diagnostics;
using PregnantLordsExpanded.Withdrawal;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace PregnantLordsExpanded.Campaign
{
    /// <summary>
    /// Milestone 2B-2D-E campaign adapter. It records warnings, petitions, authority,
    /// AI decisions, responsibility, and transitions into or out of protected rest.
    /// Milestone 2D-E-03e converts an approved withdrawal into native Bannerlord travel
    /// and keeps the approved NPC out of field service until that pregnancy ends.
    /// Ordinary NPC party members use delayed hero travel; an NPC who leads her own party
    /// keeps that party for the withdrawal trip, leaves any army, physically travels to
    /// a safe friendly fortification, and then uses Bannerlord's disband-to-fortification
    /// lifecycle so troops, wounded troops, XP, prisoners, and living heroes are resolved
    /// natively. Execution state and destination are persisted so save/load cannot start
    /// the same trip twice. Player-character automatic movement remains deferred, and the
    /// graduated battle-risk calculator is still not invoked by a campaign hook.
    /// </summary>
    internal sealed class WithdrawalDiagnosticsCoordinator
    {
        private const string SavePrefix = "PLE_M2B_";
        private const string ProtectedRestSavePrefix = "PLE_M2C_";
        private const string RelationshipSavePrefix = "PLE_M2D_";
        private const string PlayerDecisionSavePrefix = "PLE_M2DB_";
        private const string MonthlyDenialSavePrefix = "PLE_M2DD_";
        private const string ApprovedWithdrawalSavePrefix = "PLE_M2DE_";
        private const int CurrentMonthlyDenialLedgerVersion = 1;

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
        private Dictionary<string, int> _finalDecisionByRequest =
            new Dictionary<string, int>();
        private Dictionary<string, int> _appliedRoutineDenialPenaltyByPregnancyAndMonth =
            new Dictionary<string, int>();
        private Dictionary<string, int> _approvedWithdrawalStateByPregnancy =
            new Dictionary<string, int>();
        private Dictionary<string, string> _approvedWithdrawalDestinationByPregnancy =
            new Dictionary<string, string>();
        private Dictionary<string, string> _approvedWithdrawalRequestByPregnancy =
            new Dictionary<string, string>();
        private int _monthlyDenialLedgerVersion;

        private readonly Queue<PlayerPromptRequest> _pendingPlayerPrompts =
            new Queue<PlayerPromptRequest>();
        private readonly HashSet<string> _queuedPlayerPromptKeys =
            new HashSet<string>();
        private readonly HashSet<Army> _armiesPendingSafeDisband =
            new HashSet<Army>();
        private bool _playerInquiryOpen;

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
            dataStore.SyncData(
                PlayerDecisionSavePrefix + "FinalDecisionByRequest",
                ref _finalDecisionByRequest);
            dataStore.SyncData(
                MonthlyDenialSavePrefix + "AppliedRoutineDenialPenaltyByPregnancyAndMonth",
                ref _appliedRoutineDenialPenaltyByPregnancyAndMonth);
            dataStore.SyncData(
                MonthlyDenialSavePrefix + "LedgerVersion",
                ref _monthlyDenialLedgerVersion);
            dataStore.SyncData(
                ApprovedWithdrawalSavePrefix + "StateByPregnancy",
                ref _approvedWithdrawalStateByPregnancy);
            dataStore.SyncData(
                ApprovedWithdrawalSavePrefix + "DestinationByPregnancy",
                ref _approvedWithdrawalDestinationByPregnancy);
            dataStore.SyncData(
                ApprovedWithdrawalSavePrefix + "RequestByPregnancy",
                ref _approvedWithdrawalRequestByPregnancy);

            EnsureCollections();
            MigrateLegacyMonthlyDenialLedger();
        }

        public void ResetSessionPrompts()
        {
            _pendingPlayerPrompts.Clear();
            _queuedPlayerPromptKeys.Clear();
            _armiesPendingSafeDisband.Clear();
            _playerInquiryOpen = false;
        }

        public bool IsPregnancyServiceRestricted(Hero mother)
        {
            if (mother == null || mother == Hero.MainHero)
            {
                return false;
            }

            string pregnancyKey;
            if (!_activePregnancyByMother.TryGetValue(
                    HeroKey(mother),
                    out pregnancyKey))
            {
                return false;
            }

            int executionState;
            if (!_approvedWithdrawalStateByPregnancy.TryGetValue(
                    pregnancyKey,
                    out executionState))
            {
                return false;
            }

            return PregnancyServiceRestrictionCalculator.ShouldRestrict(
                (ApprovedWithdrawalExecutionState)executionState,
                mother.IsPregnant);
        }

        public void OnRestrictedPartyJoinedArmy(MobileParty party)
        {
            if (party == null || !party.IsActive || party.Army == null)
            {
                return;
            }

            Hero mother = party.LeaderHero;
            if (!IsPregnancyServiceRestricted(mother))
            {
                return;
            }

            DetachRestrictedPartyFromArmy(
                mother,
                party,
                "rejoin enforcement");

            DiagnosticLog.Info(
                mother.Name + " was added back to an army while under an approved"
                + " pregnancy withdrawal; PLE removed the party from the army and"
                + " restored the active service restriction. Withdrawal travel will"
                + " be reasserted on the next safe campaign party tick.");

            // IMPORTANT: do not execute or retire the withdrawal party from this
            // callback. Bannerlord can raise OnPartyJoinedArmyEvent while a native
            // conversation/army action is still executing. Destroying the party here
            // invalidates the conversation's live party reference and can crash its
            // ConversationItemVM when control returns. The hourly party tick below
            // performs route reassertion, any PLE-caused leader-only army cleanup, and
            // destination retirement outside that UI action stack.
        }

        public void OnRestrictedPartyHourlyTick(MobileParty party)
        {
            // The callback above may have queued an army that became leader-only only
            // because PLE removed the restricted pregnant party. Process that cleanup
            // on a safe campaign tick, never from the native join/conversation stack.
            ProcessPendingPleArmyDisbands();

            if (party == null || !party.IsActive)
            {
                return;
            }

            Hero mother = party.LeaderHero;
            if (!IsPregnancyServiceRestricted(mother))
            {
                return;
            }

            string pregnancyKey;
            if (!_activePregnancyByMother.TryGetValue(
                    HeroKey(mother),
                    out pregnancyKey))
            {
                return;
            }

            int normalizedMonth;
            if (!_highestProcessedMonthByPregnancy.TryGetValue(
                    pregnancyKey,
                    out normalizedMonth)
                || normalizedMonth < 1)
            {
                normalizedMonth = WithdrawalSettings.Default.FormalPetitionMonth;
            }

            // If another system managed to attach the party between the join event
            // and this safe tick, enforce the restriction before restoring travel.
            if (party.Army != null)
            {
                DetachRestrictedPartyFromArmy(
                    mother,
                    party,
                    "safe-tick enforcement");
            }
            else if (party.AttachedTo != null)
            {
                // Army removal should clear AttachedTo natively. If a UI/direct-action
                // path left only the attachment behind, clear it through Bannerlord's
                // public attachment property so the leader's AttachedParties collection
                // and campaign-map strength cannot retain a ghost party.
                MobileParty staleLeader = party.AttachedTo;
                party.AttachedTo = null;
                DiagnosticLog.Info(
                    mother.Name + " had a stale army attachment after approved"
                    + " pregnancy-withdrawal enforcement; PLE cleared AttachedTo="
                    + (staleLeader != null ? staleLeader.Name.ToString() : "<none>")
                    + " on the safe hourly tick before restoring withdrawal travel.");
            }

            ObserveApprovedWithdrawalExecution(
                mother,
                normalizedMonth,
                pregnancyKey);
        }

        private void DetachRestrictedPartyFromArmy(
            Hero mother,
            MobileParty party,
            string reason)
        {
            if (mother == null || party == null || party.Army == null)
            {
                return;
            }

            Army army = party.Army;
            MobileParty leader = army.LeaderParty;
            bool removedPartyWasArmyLeader = leader == party;
            int armyCountBefore = army.LeaderPartyAndAttachedPartiesCount;
            int attachedBefore = leader != null ? leader.AttachedParties.Count : -1;
            string attachedToBefore = party.AttachedTo != null
                ? party.AttachedTo.Name.ToString()
                : "<none>";

            // Native Army removal is authoritative. If this is the army leader party,
            // Bannerlord performs its own army-dispersion handling.
            party.Army = null;

            // The Army setter normally clears AttachedTo through native removal. Direct
            // conversation/army actions can transiently leave an attachment reference,
            // which is the source of the observed ghost troop count. Use the public
            // Bannerlord attachment property to finish that native bookkeeping if needed.
            if (party.AttachedTo != null)
            {
                party.AttachedTo = null;
            }

            int armyCountAfter = !removedPartyWasArmyLeader
                && leader != null
                && leader.IsActive
                    ? army.LeaderPartyAndAttachedPartiesCount
                    : 0;
            int attachedAfter = !removedPartyWasArmyLeader
                && leader != null
                && leader.IsActive
                    ? leader.AttachedParties.Count
                    : 0;
            string attachedToAfter = party.AttachedTo != null
                ? party.AttachedTo.Name.ToString()
                : "<none>";

            bool queuedLeaderOnlyDisband = false;
            if (!removedPartyWasArmyLeader
                && leader != null
                && leader.IsActive
                && LeaderOnlyArmyCleanupCalculator.ShouldDisbandAfterPleRemoval(
                    true,
                    false,
                    attachedAfter))
            {
                _armiesPendingSafeDisband.Add(army);
                queuedLeaderOnlyDisband = true;
            }

            DiagnosticLog.Info(
                "PLE restricted-party army detach: mother=" + mother.Name
                + ", reason=" + reason
                + ", army leader=" + (leader != null ? leader.Name.ToString() : "<none>")
                + ", removed party was leader=" + removedPartyWasArmyLeader
                + ", army parties before=" + armyCountBefore
                + ", army parties after=" + armyCountAfter
                + ", leader attached before=" + attachedBefore
                + ", leader attached after=" + attachedAfter
                + ", party AttachedTo before=" + attachedToBefore
                + ", party AttachedTo after=" + attachedToAfter
                + ", queued PLE leader-only army disband=" + queuedLeaderOnlyDisband
                + ".");
        }

        private void ProcessPendingPleArmyDisbands()
        {
            if (_armiesPendingSafeDisband.Count == 0)
            {
                return;
            }

            Army[] pending = new Army[_armiesPendingSafeDisband.Count];
            _armiesPendingSafeDisband.CopyTo(pending);

            foreach (Army army in pending)
            {
                _armiesPendingSafeDisband.Remove(army);
                if (army == null)
                {
                    continue;
                }

                MobileParty leader = army.LeaderParty;
                if (leader == null || !leader.IsActive)
                {
                    continue;
                }

                int attachedCount = leader.AttachedParties.Count;
                if (!LeaderOnlyArmyCleanupCalculator.ShouldDisbandAfterPleRemoval(
                        true,
                        false,
                        attachedCount))
                {
                    DiagnosticLog.Info(
                        "PLE canceled a queued pregnancy-withdrawal army disband for "
                        + leader.Name + " because the army gained " + attachedCount
                        + " attached party/parties before the safe cleanup tick.");
                    continue;
                }

                try
                {
                    int partyCountBefore = army.LeaderPartyAndAttachedPartiesCount;
                    DisbandArmyAction.ApplyByUnknownReason(army);
                    DiagnosticLog.Info(
                        "PLE safely disbanded " + leader.Name
                        + "'s leader-only army after an approved pregnancy withdrawal"
                        + " removed its final attached party; parties before native"
                        + " disband=" + partyCountBefore + ".");
                }
                catch (Exception exception)
                {
                    // Retry later rather than leaving a PLE-created leader-only army
                    // permanently alive because of a transient native campaign state.
                    _armiesPendingSafeDisband.Add(army);
                    DiagnosticLog.WarnOnce(
                        "m2de-army-disband:" + leader.GetHashCode(),
                        "Could not safely disband " + leader.Name
                        + "'s PLE-created leader-only army; cleanup will retry later. "
                        + exception.GetType().Name + ": " + exception.Message);
                }
            }
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

            if (ObserveApprovedWithdrawalExecution(
                mother,
                normalizedMonth,
                pregnancyKey))
            {
                return;
            }

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

            WithdrawalAuthorityResult authority = WithdrawalAuthorityResolver.Resolve(
                authorityContext,
                settings.IndependentAuthorityMode);

            if (stage.IsWarning)
            {
                if (monthAlreadyProcessed)
                {
                    return;
                }

                _highestProcessedMonthByPregnancy[pregnancyKey] = normalizedMonth;
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

            bool requestHasFinalPlayerDecision =
                HasFinalPlayerDecision(requestKey);
            if (!authority.HasAuthority)
            {
                if (monthAlreadyProcessed && !departureRequiresImmediatePetition)
                {
                    return;
                }

                _highestProcessedMonthByPregnancy[pregnancyKey] = normalizedMonth;
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
                if (monthAlreadyProcessed && !departureRequiresImmediatePetition)
                {
                    return;
                }

                _highestProcessedMonthByPregnancy[pregnancyKey] = normalizedMonth;
                _decisionByRequest[requestKey] = (int)WithdrawalDecision.NoDecision;
                DiagnosticLog.Info(
                    mother.Name + " reached normalized month " + normalizedMonth
                    + "; authority id " + authority.AuthorityId
                    + " was calculated but could not be mapped to a living hero.");
                return;
            }

            bool playerInvolved = authorityHero == Hero.MainHero
                || mother == Hero.MainHero;
            if (monthAlreadyProcessed
                && !departureRequiresImmediatePetition
                && (!playerInvolved || requestHasFinalPlayerDecision))
            {
                return;
            }

            if (!monthAlreadyProcessed)
            {
                _highestProcessedMonthByPregnancy[pregnancyKey] = normalizedMonth;
            }

            if (playerInvolved)
            {
                QueuePlayerDecision(
                    mother,
                    authorityHero,
                    authority,
                    normalizedMonth,
                    pregnancyKey,
                    requestKey);
                return;
            }

            bool selfAuthority = authority.Kind == WithdrawalAuthorityKind.Self;
            AiWithdrawalDecisionResult decisionResult =
                AiWithdrawalDecisionCalculator.Calculate(
                    CreateDecisionInput(mother, authorityHero, normalizedMonth, selfAuthority));
            var resolution = new PlayerWithdrawalResolution(
                decisionResult.Decision,
                decisionResult.Decision,
                WithdrawalResponsibilityCalculator.FromDecision(decisionResult.Decision),
                decisionResult.Decision == WithdrawalDecision.Deny);
            RecordResolution(
                mother,
                authorityHero,
                authority,
                normalizedMonth,
                pregnancyKey,
                requestKey,
                resolution,
                decisionResult.Explanation,
                false);
        }

        private void QueuePlayerDecision(
            Hero mother,
            Hero authorityHero,
            WithdrawalAuthorityResult authority,
            int normalizedMonth,
            string pregnancyKey,
            string requestKey)
        {
            if (HasFinalPlayerDecision(requestKey)
                || _queuedPlayerPromptKeys.Contains(requestKey))
            {
                return;
            }

            bool pregnantPlayer = mother == Hero.MainHero;
            WithdrawalDecision authorityDecision = WithdrawalDecision.NoDecision;
            string explanation = "player authority decision";

            if (pregnantPlayer && authorityHero != mother)
            {
                int savedDecision;
                if (_decisionByRequest.TryGetValue(requestKey, out savedDecision)
                    && savedDecision != (int)WithdrawalDecision.NoDecision)
                {
                    authorityDecision = (WithdrawalDecision)savedDecision;
                    explanation = "restored AI authority decision";
                }
                else
                {
                    AiWithdrawalDecisionResult aiDecision =
                        AiWithdrawalDecisionCalculator.Calculate(
                            CreateDecisionInput(
                                mother,
                                authorityHero,
                                normalizedMonth,
                                false));
                    authorityDecision = aiDecision.Decision;
                    explanation = aiDecision.Explanation;
                    _decisionByRequest[requestKey] = (int)authorityDecision;
                }
            }
            else
            {
                _decisionByRequest[requestKey] = (int)WithdrawalDecision.NoDecision;
            }

            _queuedPlayerPromptKeys.Add(requestKey);
            _pendingPlayerPrompts.Enqueue(
                new PlayerPromptRequest(
                    mother,
                    authorityHero,
                    authority,
                    normalizedMonth,
                    pregnancyKey,
                    requestKey,
                    authorityDecision,
                    explanation,
                    pregnantPlayer));

            DiagnosticLog.Info(
                mother.Name + " withdrawal petition at normalized month "
                + normalizedMonth + " queued for player decision; authority="
                + authorityHero.Name + " (" + authority.Kind + "), authority decision="
                + authorityDecision + ".");
            TryShowNextPlayerPrompt();
        }

        private void TryShowNextPlayerPrompt()
        {
            if (_playerInquiryOpen || _pendingPlayerPrompts.Count == 0)
            {
                return;
            }

            PlayerPromptRequest request = _pendingPlayerPrompts.Dequeue();
            if (HasFinalPlayerDecision(request.RequestKey))
            {
                _queuedPlayerPromptKeys.Remove(request.RequestKey);
                TryShowNextPlayerPrompt();
                return;
            }

            _playerInquiryOpen = true;
            try
            {
                MultiSelectionInquiryData inquiry = request.PregnantPlayer
                    ? CreatePregnantPlayerInquiry(request)
                    : CreatePlayerAuthorityInquiry(request);

                // isExitShown=false on the inquiry makes this a required selection.
                // Escape is no longer mapped to an implicit denial.
                MBInformationManager.ShowMultiSelectionInquiry(
                    inquiry,
                    true,
                    true);
            }
            catch (Exception exception)
            {
                _playerInquiryOpen = false;
                _queuedPlayerPromptKeys.Remove(request.RequestKey);
                DiagnosticLog.WarnOnce(
                    "m2db-inquiry:" + request.RequestKey,
                    "Could not display the withdrawal decision for "
                    + request.Mother.Name + "; the request remains pending and will be retried. "
                    + exception.GetType().Name + ": " + exception.Message);
                TryShowNextPlayerPrompt();
            }
        }

        private MultiSelectionInquiryData CreatePlayerAuthorityInquiry(
            PlayerPromptRequest request)
        {
            int pendingPenalty = GetPendingRelationshipPenalty(
                request.PregnancyKey,
                request.Authority,
                request.NormalizedMonth);
            string penaltyText = pendingPenalty < 0
                ? " Ordering her to remain will cause an additional relationship loss of "
                    + Math.Abs(pendingPenalty) + "."
                : string.Empty;

            string text = request.Mother.Name + " is in normalized pregnancy month "
                + request.NormalizedMonth
                + " and requests permission to withdraw from field service."
                + penaltyText
                + " If approved, an eligible NPC will begin native Bannerlord travel"
                + " toward a safe friendly fortification.";

            List<InquiryElement> choices = new List<InquiryElement>
            {
                new InquiryElement(
                    PlayerWithdrawalChoice.ApprovePetition,
                    "Approve Withdrawal",
                    null,
                    true,
                    "Authorize her to leave field service and travel toward protection."),
                new InquiryElement(
                    PlayerWithdrawalChoice.DenyPetition,
                    "Order Her to Remain",
                    null,
                    true,
                    pendingPenalty < 0
                        ? "Keep her in field service. Relationship change: "
                            + pendingPenalty + "."
                        : "Keep her in field service.")
            };

            return new MultiSelectionInquiryData(
                "Withdrawal Petition",
                text,
                choices,
                false,
                1,
                1,
                "Confirm Decision",
                string.Empty,
                selected => ResolvePlayerAuthoritySelection(request, selected),
                null);
        }

        private void ResolvePlayerAuthoritySelection(
            PlayerPromptRequest request,
            List<InquiryElement> selected)
        {
            if (selected == null || selected.Count != 1
                || !(selected[0].Identifier is PlayerWithdrawalChoice))
            {
                RequeueInvalidPlayerSelection(request);
                return;
            }

            PlayerWithdrawalChoice choice =
                (PlayerWithdrawalChoice)selected[0].Identifier;
            if (choice != PlayerWithdrawalChoice.ApprovePetition
                && choice != PlayerWithdrawalChoice.DenyPetition)
            {
                RequeueInvalidPlayerSelection(request);
                return;
            }

            ResolvePlayerPrompt(
                request,
                PlayerWithdrawalDecisionCalculator.ResolvePlayerAuthority(choice),
                choice);
        }

        private MultiSelectionInquiryData CreatePregnantPlayerInquiry(
            PlayerPromptRequest request)
        {
            string authorityText;
            string withdrawText;
            string remainText;

            if (request.Authority == request.Mother)
            {
                authorityText = "You are your own authority and must decide whether to withdraw.";
                withdrawText = "Choose Withdrawal";
                remainText = "Remain in the Field";
            }
            else if (request.AuthorityDecision == WithdrawalDecision.Deny)
            {
                int pendingPenalty = GetPendingRelationshipPenalty(
                    request.PregnancyKey,
                    request.Authority,
                    request.NormalizedMonth);
                authorityText = request.Authority.Name
                    + " has ordered you to remain in the field."
                    + (pendingPenalty < 0
                        ? " The order will cause an additional relationship loss of "
                            + Math.Abs(pendingPenalty) + "."
                        : string.Empty);
                withdrawText = "Withdraw Anyway";
                remainText = "Remain as Ordered";
            }
            else
            {
                authorityText = request.Authority.Name
                    + " has approved your withdrawal request.";
                withdrawText = "Choose Withdrawal";
                remainText = "Remain in the Field";
            }

            string text = "You are in normalized pregnancy month "
                + request.NormalizedMonth + ". " + authorityText
                + " Your decision is recorded now. Automatic player-character travel"
                + " remains deferred so the mod does not seize control of the player party.";

            List<InquiryElement> choices = new List<InquiryElement>
            {
                new InquiryElement(
                    PlayerWithdrawalChoice.Withdraw,
                    withdrawText,
                    null,
                    true,
                    "Withdraw from field service."),
                new InquiryElement(
                    PlayerWithdrawalChoice.ContinueCampaigning,
                    remainText,
                    null,
                    true,
                    "Continue campaigning despite the pregnancy withdrawal decision.")
            };

            return new MultiSelectionInquiryData(
                "Pregnancy Withdrawal",
                text,
                choices,
                false,
                1,
                1,
                "Confirm Decision",
                string.Empty,
                selected => ResolvePregnantPlayerSelection(request, selected),
                null);
        }

        private void ResolvePregnantPlayerSelection(
            PlayerPromptRequest request,
            List<InquiryElement> selected)
        {
            if (selected == null || selected.Count != 1
                || !(selected[0].Identifier is PlayerWithdrawalChoice))
            {
                RequeueInvalidPlayerSelection(request);
                return;
            }

            PlayerWithdrawalChoice choice =
                (PlayerWithdrawalChoice)selected[0].Identifier;
            if (choice != PlayerWithdrawalChoice.Withdraw
                && choice != PlayerWithdrawalChoice.ContinueCampaigning)
            {
                RequeueInvalidPlayerSelection(request);
                return;
            }

            ResolvePlayerPrompt(
                request,
                PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    request.AuthorityDecision,
                    choice),
                choice);
        }

        private void RequeueInvalidPlayerSelection(PlayerPromptRequest request)
        {
            _playerInquiryOpen = false;
            _queuedPlayerPromptKeys.Remove(request.RequestKey);
            DiagnosticLog.WarnOnce(
                "m2db-invalid-selection:" + request.RequestKey,
                "Withdrawal decision for " + request.Mother.Name
                + " closed without one valid explicit selection; the request remains pending.");
            QueuePlayerDecision(
                request.Mother,
                request.Authority,
                request.AuthorityResult,
                request.NormalizedMonth,
                request.PregnancyKey,
                request.RequestKey);
        }

        private void ResolvePlayerPrompt(
            PlayerPromptRequest request,
            PlayerWithdrawalResolution resolution,
            PlayerWithdrawalChoice playerChoice)
        {
            try
            {
                _finalDecisionByRequest[request.RequestKey] =
                    (int)resolution.FinalDecision;
                RecordResolution(
                    request.Mother,
                    request.Authority,
                    request.AuthorityResult,
                    request.NormalizedMonth,
                    request.PregnancyKey,
                    request.RequestKey,
                    resolution,
                    request.Explanation + "; player choice=" + playerChoice,
                    true);
            }
            finally
            {
                _queuedPlayerPromptKeys.Remove(request.RequestKey);
                _playerInquiryOpen = false;
                TryShowNextPlayerPrompt();
            }
        }

        private void RecordResolution(
            Hero mother,
            Hero authorityHero,
            WithdrawalAuthorityResult authority,
            int normalizedMonth,
            string pregnancyKey,
            string requestKey,
            PlayerWithdrawalResolution resolution,
            string explanation,
            bool playerResolved)
        {
            _decisionByRequest[requestKey] = (int)resolution.AuthorityDecision;
            _lastResponsibilityByPregnancy[pregnancyKey] =
                (int)resolution.Responsibility;

            if (resolution.Responsibility == WithdrawalResponsibility.CommanderOverride)
            {
                _lastResponsibleHeroByPregnancy[pregnancyKey] = HeroKey(authorityHero);
            }
            else if (resolution.Responsibility == WithdrawalResponsibility.VoluntaryRefusal)
            {
                _lastResponsibleHeroByPregnancy[pregnancyKey] = HeroKey(mother);
            }
            else
            {
                _lastResponsibleHeroByPregnancy.Remove(pregnancyKey);
            }

            int targetLiability = 0;
            int additionalLiability = 0;
            int relationshipChangeApplied = 0;
            int appliedCumulativeRelationshipPenalty = 0;
            if (resolution.ApplyCommanderRelationPenalty && authorityHero != mother)
            {
                ApplyCommanderDenialConsequence(
                    mother,
                    authorityHero,
                    normalizedMonth,
                    pregnancyKey,
                    out targetLiability,
                    out additionalLiability,
                    out relationshipChangeApplied,
                    out appliedCumulativeRelationshipPenalty);
            }

            if (resolution.WithdrawalAuthorized)
            {
                AuthorizeApprovedWithdrawalExecution(
                    mother,
                    normalizedMonth,
                    pregnancyKey,
                    requestKey);
            }

            DiagnosticLog.Info(
                mother.Name + " withdrawal petition at normalized month "
                + normalizedMonth + ": authority=" + authorityHero.Name
                + " (" + authority.Kind + "), authority decision="
                + resolution.AuthorityDecision + ", final decision="
                + resolution.FinalDecision + ", responsibility="
                + resolution.Responsibility + ", player resolved=" + playerResolved
                + ", target liability=" + targetLiability
                + ", newly recorded liability=" + additionalLiability
                + ", relationship change applied=" + relationshipChangeApplied
                + ", accounted routine denial penalty total="
                + appliedCumulativeRelationshipPenalty
                + "; " + explanation + ".");
        }

        private void ApplyCommanderDenialConsequence(
            Hero mother,
            Hero authorityHero,
            int normalizedMonth,
            string pregnancyKey,
            out int targetLiability,
            out int additionalLiability,
            out int relationshipChangeApplied,
            out int appliedCumulativeRelationshipPenalty)
        {
            string liabilityKey = pregnancyKey + "|authority:" + HeroKey(authorityHero);
            int alreadyRecorded;
            _liabilityByPregnancyAndAuthority.TryGetValue(liabilityKey, out alreadyRecorded);

            targetLiability = CommanderLiabilityCalculator.GetDefaultCumulativePenalty(
                normalizedMonth);
            additionalLiability = CommanderLiabilityCalculator.GetAdditionalPenalty(
                alreadyRecorded,
                targetLiability);
            if (additionalLiability < 0)
            {
                _liabilityByPregnancyAndAuthority[liabilityKey] = targetLiability;
            }

            string monthlyPenaltyKey =
                MonthlyDenialResentmentCalculator.GetLedgerKey(
                    pregnancyKey,
                    normalizedMonth);
            bool monthAlreadyApplied =
                _appliedRoutineDenialPenaltyByPregnancyAndMonth.ContainsKey(
                    monthlyPenaltyKey);
            int pendingRelationshipPenalty =
                MonthlyDenialResentmentCalculator.GetPendingPenalty(
                    normalizedMonth,
                    monthAlreadyApplied);

            relationshipChangeApplied = 0;
            appliedCumulativeRelationshipPenalty =
                GetAccountedRoutineDenialPenaltyTotal(pregnancyKey);
            if (pendingRelationshipPenalty < 0
                && TryApplyCommanderRelationshipPenalty(
                    mother,
                    authorityHero,
                    monthlyPenaltyKey,
                    pendingRelationshipPenalty,
                    pendingRelationshipPenalty))
            {
                relationshipChangeApplied = pendingRelationshipPenalty;
                _appliedRoutineDenialPenaltyByPregnancyAndMonth[monthlyPenaltyKey] =
                    pendingRelationshipPenalty;

                int legacyAppliedTotal;
                _appliedRelationPenaltyByPregnancyAndAuthority.TryGetValue(
                    liabilityKey,
                    out legacyAppliedTotal);
                _appliedRelationPenaltyByPregnancyAndAuthority[liabilityKey] =
                    legacyAppliedTotal + pendingRelationshipPenalty;

                appliedCumulativeRelationshipPenalty =
                    GetAccountedRoutineDenialPenaltyTotal(pregnancyKey);
            }
        }

        private int GetPendingRelationshipPenalty(
            string pregnancyKey,
            Hero authorityHero,
            int normalizedMonth)
        {
            string monthlyPenaltyKey =
                MonthlyDenialResentmentCalculator.GetLedgerKey(
                    pregnancyKey,
                    normalizedMonth);
            bool monthAlreadyApplied =
                _appliedRoutineDenialPenaltyByPregnancyAndMonth.ContainsKey(
                    monthlyPenaltyKey);
            return MonthlyDenialResentmentCalculator.GetPendingPenalty(
                normalizedMonth,
                monthAlreadyApplied);
        }

        private int GetAccountedRoutineDenialPenaltyTotal(string pregnancyKey)
        {
            int total = 0;
            string prefix = pregnancyKey + "|month:";
            foreach (KeyValuePair<string, int> pair
                in _appliedRoutineDenialPenaltyByPregnancyAndMonth)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    total += pair.Value;
                }
            }

            return total;
        }

        private void MigrateLegacyMonthlyDenialLedger()
        {
            if (_monthlyDenialLedgerVersion >= CurrentMonthlyDenialLedgerVersion)
            {
                return;
            }

            int migratedMonths = 0;
            foreach (KeyValuePair<string, int> decision in _decisionByRequest)
            {
                if (decision.Value != (int)WithdrawalDecision.Deny)
                {
                    continue;
                }

                int normalizedMonth;
                if (!MonthlyDenialResentmentCalculator.TryGetNormalizedMonthFromRequestKey(
                        decision.Key,
                        out normalizedMonth))
                {
                    continue;
                }

                int pregnancySeparator = decision.Key.IndexOf('|');
                if (pregnancySeparator <= 0)
                {
                    continue;
                }

                string pregnancyKey = decision.Key.Substring(0, pregnancySeparator);
                string authorityId;
                if (!_authorityByRequest.TryGetValue(decision.Key, out authorityId)
                    || string.IsNullOrWhiteSpace(authorityId))
                {
                    continue;
                }

                string legacyLiabilityKey =
                    pregnancyKey + "|authority:" + authorityId;
                int legacyAppliedPenalty;
                if (!_appliedRelationPenaltyByPregnancyAndAuthority.TryGetValue(
                        legacyLiabilityKey,
                        out legacyAppliedPenalty)
                    || legacyAppliedPenalty >= 0)
                {
                    continue;
                }

                string monthlyPenaltyKey =
                    MonthlyDenialResentmentCalculator.GetLedgerKey(
                        pregnancyKey,
                        normalizedMonth);
                if (!_appliedRoutineDenialPenaltyByPregnancyAndMonth.ContainsKey(
                        monthlyPenaltyKey))
                {
                    _appliedRoutineDenialPenaltyByPregnancyAndMonth[monthlyPenaltyKey] =
                        MonthlyDenialResentmentCalculator.RoutineDenialPenalty;
                    migratedMonths++;
                }
            }

            _monthlyDenialLedgerVersion = CurrentMonthlyDenialLedgerVersion;
            if (migratedMonths > 0)
            {
                DiagnosticLog.Info(
                    "Milestone 2D-D migration accounted for "
                    + migratedMonths
                    + " previously denied normalized month(s) without replaying "
                    + "relationship penalties.");
            }
        }

        private bool HasFinalPlayerDecision(string requestKey)
        {
            int finalDecision;
            return _finalDecisionByRequest.TryGetValue(requestKey, out finalDecision)
                && finalDecision != (int)WithdrawalDecision.NoDecision;
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
            _approvedWithdrawalStateByPregnancy.Remove(pregnancyKey);
            _approvedWithdrawalDestinationByPregnancy.Remove(pregnancyKey);
            _approvedWithdrawalRequestByPregnancy.Remove(pregnancyKey);
            RemoveKeysWithPrefix(_decisionByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(_finalDecisionByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(_authorityByRequest, pregnancyKey + "|");
            RemoveKeysWithPrefix(
                _liabilityByPregnancyAndAuthority,
                pregnancyKey + "|");
            RemoveKeysWithPrefix(
                _appliedRelationPenaltyByPregnancyAndAuthority,
                pregnancyKey + "|");
            RemoveKeysWithPrefix(
                _appliedRoutineDenialPenaltyByPregnancyAndMonth,
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
                    + relationshipChange + ", target relationship penalty="
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

        private void AuthorizeApprovedWithdrawalExecution(
            Hero mother,
            int normalizedMonth,
            string pregnancyKey,
            string requestKey)
        {
            if (mother == Hero.MainHero)
            {
                DiagnosticLog.Info(
                    mother.Name + " has an approved withdrawal at normalized month "
                    + normalizedMonth
                    + ", but automatic player-character movement is deferred.");
                return;
            }

            if (!_approvedWithdrawalStateByPregnancy.ContainsKey(pregnancyKey))
            {
                _approvedWithdrawalStateByPregnancy[pregnancyKey] =
                    (int)ApprovedWithdrawalExecutionState.Pending;
                _approvedWithdrawalRequestByPregnancy[pregnancyKey] = requestKey;
            }

            ObserveApprovedWithdrawalExecution(
                mother,
                normalizedMonth,
                pregnancyKey);
        }

        private bool ObserveApprovedWithdrawalExecution(
            Hero mother,
            int normalizedMonth,
            string pregnancyKey)
        {
            int stateValue;
            if (!_approvedWithdrawalStateByPregnancy.TryGetValue(
                    pregnancyKey,
                    out stateValue))
            {
                return false;
            }

            ApprovedWithdrawalExecutionState previousState =
                (ApprovedWithdrawalExecutionState)stateValue;
            if (previousState == ApprovedWithdrawalExecutionState.Completed)
            {
                return false;
            }

            string destinationId;
            _approvedWithdrawalDestinationByPregnancy.TryGetValue(
                pregnancyKey,
                out destinationId);
            Settlement destination = !string.IsNullOrWhiteSpace(destinationId)
                ? Settlement.Find(destinationId)
                : null;

            MobileParty currentParty = mother.PartyBelongedTo;
            bool leadsCurrentParty = currentParty != null
                && currentParty.IsActive
                && currentParty.LeaderHero == mother;

            if (leadsCurrentParty
                && destination != null
                && currentParty.CurrentSettlement == destination)
            {
                if (TryCompleteLeaderPartyWithdrawalAtDestination(
                        mother,
                        currentParty,
                        destination,
                        pregnancyKey,
                        normalizedMonth))
                {
                    MarkApprovedWithdrawalCompleted(
                        mother,
                        destination,
                        pregnancyKey);
                    return true;
                }

                currentParty = mother.PartyBelongedTo;
                leadsCurrentParty = currentParty != null
                    && currentParty.IsActive
                    && currentParty.LeaderHero == mother;
            }

            bool restingAtDestination = destination != null
                && mother.PartyBelongedTo == null
                && mother.CurrentSettlement == destination;
            bool leaderPartyTravelActive = leadsCurrentParty
                && destination != null
                && currentParty.TargetSettlement == destination
                && currentParty.CurrentSettlement != destination;
            bool nativeTravelActive = mother.IsTraveling
                || leaderPartyTravelActive;

            bool canBeginTravel = false;
            if (!nativeTravelActive && !restingAtDestination)
            {
                if (!IsValidWithdrawalDestination(mother, destination))
                {
                    destination = FindApprovedWithdrawalDestination(mother);
                    if (destination == null)
                    {
                        _approvedWithdrawalDestinationByPregnancy.Remove(pregnancyKey);
                    }
                    else
                    {
                        _approvedWithdrawalDestinationByPregnancy[pregnancyKey] =
                            destination.StringId;
                    }
                }

                canBeginTravel = CanBeginApprovedWithdrawalTravel(
                    mother,
                    destination);
            }

            ApprovedWithdrawalExecutionResult result =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    previousState,
                    nativeTravelActive,
                    restingAtDestination,
                    canBeginTravel);

            if (result.ShouldBeginTravel)
            {
                try
                {
                    MobileParty party = mother.PartyBelongedTo;
                    if (party != null && party.IsActive && party.LeaderHero == mother)
                    {
                        StartLeaderPartyWithdrawalTravel(
                            mother,
                            party,
                            destination,
                            normalizedMonth);
                    }
                    else
                    {
                        TeleportHeroAction.ApplyDelayedTeleportToSettlement(
                            mother,
                            destination);
                        DiagnosticLog.Info(
                            mother.Name + " began approved pregnancy withdrawal travel toward "
                            + destination.Name + " during normalized month "
                            + normalizedMonth
                            + " using Bannerlord's native delayed hero travel.");
                    }

                    _approvedWithdrawalStateByPregnancy[pregnancyKey] =
                        (int)ApprovedWithdrawalExecutionState.Traveling;
                    return true;
                }
                catch (Exception exception)
                {
                    MobileParty party = mother.PartyBelongedTo;
                    if (party != null && party.IsActive && party.LeaderHero == mother)
                    {
                        try
                        {
                            party.Ai.SetDoNotMakeNewDecisions(false);
                        }
                        catch
                        {
                            // Preserve the original travel failure below.
                        }
                    }

                    _approvedWithdrawalStateByPregnancy[pregnancyKey] =
                        (int)ApprovedWithdrawalExecutionState.Pending;
                    DiagnosticLog.WarnOnce(
                        "m2de-travel:" + pregnancyKey + ":"
                            + (destination != null ? destination.StringId : "none"),
                        "Could not start approved withdrawal travel for "
                        + mother.Name + " toward "
                        + (destination != null ? destination.Name.ToString() : "<no destination>")
                        + "; the approval remains pending and will retry later. "
                        + exception.GetType().Name + ": " + exception.Message);
                    return true;
                }
            }

            _approvedWithdrawalStateByPregnancy[pregnancyKey] =
                (int)result.NextState;

            if (result.NextState == ApprovedWithdrawalExecutionState.Completed)
            {
                MarkApprovedWithdrawalCompleted(
                    mother,
                    destination,
                    pregnancyKey);

                DiagnosticLog.Info(
                    mother.Name + " completed approved pregnancy withdrawal and"
                    + " established protected rest at "
                    + (destination != null
                        ? destination.Name.ToString()
                        : "<resolved destination>")
                    + " during normalized month " + normalizedMonth + ".");
                return true;
            }

            if (result.NextState == ApprovedWithdrawalExecutionState.Traveling)
            {
                return true;
            }

            if (result.NextState == ApprovedWithdrawalExecutionState.Pending)
            {
                DiagnosticLog.WarnOnce(
                    "m2de-pending:" + pregnancyKey + ":" + normalizedMonth,
                    mother.Name + " has an approved withdrawal, but no safe native"
                    + " departure can begin yet. The approval remains pending.");
                return true;
            }

            return false;
        }

        private void MarkApprovedWithdrawalCompleted(
            Hero mother,
            Settlement destination,
            string pregnancyKey)
        {
            _approvedWithdrawalStateByPregnancy[pregnancyKey] =
                (int)ApprovedWithdrawalExecutionState.Completed;
            _protectedRestStateByPregnancy[pregnancyKey] =
                (int)ProtectedRestState.ProtectedRest;

            if (destination != null)
            {
                _protectedSettlementByPregnancy[pregnancyKey] =
                    destination.StringId;
            }

            _lastResponsibilityByPregnancy[pregnancyKey] =
                (int)WithdrawalResponsibility.WithdrawalApproved;
            _lastResponsibleHeroByPregnancy.Remove(pregnancyKey);
        }

        private static bool CanBeginApprovedWithdrawalTravel(
            Hero mother,
            Settlement destination)
        {
            if (mother == null
                || destination == null
                || mother == Hero.MainHero
                || mother.IsPrisoner
                || mother.IsTraveling)
            {
                return false;
            }

            MobileParty party = mother.PartyBelongedTo;
            if (party == null
                || !party.IsActive
                || party.MapEvent != null
                || party.BesiegedSettlement != null)
            {
                return false;
            }

            if (party.LeaderHero == mother)
            {
                return party.CurrentSettlement != destination;
            }

            return mother.CanMoveToSettlement();
        }

        private void StartLeaderPartyWithdrawalTravel(
            Hero mother,
            MobileParty party,
            Settlement destination,
            int normalizedMonth)
        {
            if (party.Army != null)
            {
                DetachRestrictedPartyFromArmy(
                    mother,
                    party,
                    "approved withdrawal departure");
            }

            // Keep the pregnant lord as leader of her existing party during the trip.
            // Prevent normal campaign AI from replacing the withdrawal destination.
            // NavigationType.All permits Bannerlord to choose land, naval, or mixed
            // routing instead of forcing the withdrawal party onto a land-only route.
            party.Ai.SetDoNotMakeNewDecisions(true);
            SetPartyAiAction.GetActionForVisitingSettlement(
                party,
                destination,
                MobileParty.NavigationType.All,
                false,
                false);

            DiagnosticLog.Info(
                mother.Name + " began approved pregnancy withdrawal as party leader toward "
                + destination.Name + " during normalized month " + normalizedMonth
                + "; her existing party was detached from its army and ordered to travel"
                + " physically to the protected fortification.");
        }

        private static bool TryCompleteLeaderPartyWithdrawalAtDestination(
            Hero mother,
            MobileParty party,
            Settlement destination,
            string pregnancyKey,
            int normalizedMonth)
        {
            if (mother == null
                || party == null
                || destination == null
                || !party.IsActive
                || party.LeaderHero != mother
                || party.CurrentSettlement != destination
                || !destination.IsFortification
                || destination.Town == null)
            {
                return false;
            }

            try
            {
                if (destination.Town.GarrisonParty == null)
                {
                    destination.AddGarrisonParty();
                }

                if (destination.Town.GarrisonParty == null
                    || destination.Town.GarrisonParty.MapEvent != null)
                {
                    DiagnosticLog.WarnOnce(
                        "m2de-garrison-busy:" + pregnancyKey + ":" + destination.StringId,
                        mother.Name + " reached " + destination.Name
                        + " for approved pregnancy withdrawal, but its garrison is not"
                        + " currently available for Bannerlord's native disband merge."
                        + " The party will remain intact and retry later.");
                    return false;
                }

                int regularsBefore = party.MemberRoster.TotalRegulars;
                int woundedBefore = party.MemberRoster.TotalWoundedRegulars;
                int garrisonBefore =
                    destination.Town.GarrisonParty.MemberRoster.TotalManCount;

                // This is Bannerlord's disbanding-specific retirement action, not the
                // generic DestroyPartyAction.Apply path. It first dispatches
                // OnPartyDisbanded, allowing vanilla DisbandPartyCampaignBehavior to
                // merge troops/wounded/XP/prisoners and place living heroes in the
                // fortification; only then does Bannerlord retire the mobile party.
                DestroyPartyAction.ApplyForDisbanding(party, destination);

                int garrisonAfter = destination.Town.GarrisonParty != null
                    ? destination.Town.GarrisonParty.MemberRoster.TotalManCount
                    : -1;

                DiagnosticLog.Info(
                    mother.Name + " reached " + destination.Name
                    + " and completed the native party-leader withdrawal retirement"
                    + " during normalized month " + normalizedMonth
                    + "; departing regulars=" + regularsBefore
                    + ", wounded regulars=" + woundedBefore
                    + ", garrison before=" + garrisonBefore
                    + ", garrison after=" + garrisonAfter + ".");

                // The native disband event places living heroes into the fortification,
                // but CurrentSettlement can lag the party retirement by a frame. Treat
                // successful native retirement as completion so the execution ledger
                // cannot fall back to Pending immediately after the merge.
                return !party.IsActive
                    || mother.PartyBelongedTo != party;
            }
            catch (Exception exception)
            {
                DiagnosticLog.WarnOnce(
                    "m2de-arrival:" + pregnancyKey + ":" + destination.StringId,
                    "Could not complete native party-leader withdrawal retirement for "
                    + mother.Name + " at " + destination.Name
                    + "; the party remains for a later retry. "
                    + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        private static Settlement FindApprovedWithdrawalDestination(Hero mother)
        {
            if (mother == null)
            {
                return null;
            }

            Settlement home = mother.HomeSettlement;
            if (IsValidWithdrawalDestination(mother, home))
            {
                return home;
            }

            MobileParty party = mother.PartyBelongedTo;
            if (party == null)
            {
                return null;
            }

            Settlement nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            foreach (Settlement settlement in TaleWorlds.CampaignSystem.Campaign.Current.Settlements)
            {
                if (!IsValidWithdrawalDestination(mother, settlement))
                {
                    continue;
                }

                float distanceSquared =
                    settlement.GetPosition2D.DistanceSquared(party.GetPosition2D);
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = settlement;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private static bool IsValidWithdrawalDestination(
            Hero mother,
            Settlement settlement)
        {
            if (mother == null
                || settlement == null
                || !settlement.IsFortification
                || settlement.IsUnderSiege
                || settlement.MapFaction == null
                || mother.MapFaction == null)
            {
                return false;
            }

            return settlement.MapFaction == mother.MapFaction
                && !FactionManager.IsAtWarAgainstFaction(
                    mother.MapFaction,
                    settlement.MapFaction);
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
            _finalDecisionByRequest = _finalDecisionByRequest
                ?? new Dictionary<string, int>();
            _appliedRoutineDenialPenaltyByPregnancyAndMonth =
                _appliedRoutineDenialPenaltyByPregnancyAndMonth
                ?? new Dictionary<string, int>();
            _approvedWithdrawalStateByPregnancy =
                _approvedWithdrawalStateByPregnancy
                ?? new Dictionary<string, int>();
            _approvedWithdrawalDestinationByPregnancy =
                _approvedWithdrawalDestinationByPregnancy
                ?? new Dictionary<string, string>();
            _approvedWithdrawalRequestByPregnancy =
                _approvedWithdrawalRequestByPregnancy
                ?? new Dictionary<string, string>();
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

        private sealed class PlayerPromptRequest
        {
            public PlayerPromptRequest(
                Hero mother,
                Hero authority,
                WithdrawalAuthorityResult authorityResult,
                int normalizedMonth,
                string pregnancyKey,
                string requestKey,
                WithdrawalDecision authorityDecision,
                string explanation,
                bool pregnantPlayer)
            {
                Mother = mother;
                Authority = authority;
                AuthorityResult = authorityResult;
                NormalizedMonth = normalizedMonth;
                PregnancyKey = pregnancyKey;
                RequestKey = requestKey;
                AuthorityDecision = authorityDecision;
                Explanation = explanation ?? string.Empty;
                PregnantPlayer = pregnantPlayer;
            }

            public Hero Mother { get; }

            public Hero Authority { get; }

            public WithdrawalAuthorityResult AuthorityResult { get; }

            public int NormalizedMonth { get; }

            public string PregnancyKey { get; }

            public string RequestKey { get; }

            public WithdrawalDecision AuthorityDecision { get; }

            public string Explanation { get; }

            public bool PregnantPlayer { get; }
        }
    }
}
