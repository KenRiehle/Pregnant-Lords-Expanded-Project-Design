using System;
using System.Collections.Generic;
using PregnantLordsExpanded.Pregnancy;
using PregnantLordsExpanded.Withdrawal;

namespace PregnantLordsExpanded.CalculationTests
{
    internal static class Program
    {
        private static int Main()
        {
            AssertKnown(100.0, 100.0, 190.0, 0.0, 1, "start of pregnancy");
            AssertKnown(145.0, 100.0, 190.0, 0.5, 5, "exactly fifty percent");
            AssertKnown(190.0, 100.0, 190.0, 1.0, 9, "due time");
            AssertKnown(90.0, 100.0, 190.0, 0.0, 1, "before conception clamps");
            AssertKnown(200.0, 100.0, 190.0, 1.0, 9, "after due time clamps");

            AssertKnown(18.0, 0.0, 36.0, 0.5, 5, "36-day normalized duration");
            AssertKnown(36.0, 0.0, 72.0, 0.5, 5, "72-day normalized duration");

            AssertInvalid(10.0, 20.0, 20.0, "zero duration");
            AssertInvalid(10.0, 20.0, 19.0, "reversed duration");
            AssertInvalid(double.NaN, 0.0, 10.0, "non-finite value");

            Console.WriteLine("All normalized pregnancy progress tests passed.");

            TestDefaultWithdrawalSchedule();
            TestCustomWithdrawalSchedule();
            TestResponsibilityMapping();
            TestCommanderLiability();
            TestAuthorityResolution();
            TestFamilyReactions();
            TestFamilyReactionSettings();

            Console.WriteLine("All Milestone 2A withdrawal calculation tests passed.");

            TestAiWithdrawalDecisions();

            Console.WriteLine("All Milestone 2B diagnostic decision tests passed.");

            TestProtectedRestTransitions();

            Console.WriteLine("All Milestone 2C protected-rest transition tests passed.");

            TestCommanderRelationPenalties();

            Console.WriteLine("All Milestone 2D commander relationship penalty tests passed.");
            return 0;
        }

        private static void TestDefaultWithdrawalSchedule()
        {
            WithdrawalSettings settings = WithdrawalSettings.Default;

            AssertStage(1, settings, WithdrawalMonthStage.None);
            AssertStage(2, settings, WithdrawalMonthStage.None);
            AssertStage(3, settings, WithdrawalMonthStage.AdvanceWarning);
            AssertStage(4, settings, WithdrawalMonthStage.FormalPetition);

            for (int month = 5; month <= 9; month++)
            {
                AssertStage(month, settings, WithdrawalMonthStage.RenewedPetition);
            }

            AssertStage(0, settings, WithdrawalMonthStage.None);
            AssertStage(10, settings, WithdrawalMonthStage.None);
        }

        private static void TestCustomWithdrawalSchedule()
        {
            var settings = new WithdrawalSettings(
                2,
                5,
                IndependentWithdrawalAuthorityMode.Self);

            AssertStage(1, settings, WithdrawalMonthStage.None);
            AssertStage(2, settings, WithdrawalMonthStage.AdvanceWarning);
            AssertStage(3, settings, WithdrawalMonthStage.None);
            AssertStage(4, settings, WithdrawalMonthStage.None);
            AssertStage(5, settings, WithdrawalMonthStage.FormalPetition);
            AssertStage(6, settings, WithdrawalMonthStage.RenewedPetition);

            AssertThrows<ArgumentOutOfRangeException>(
                () => new WithdrawalSettings(
                    0,
                    4,
                    IndependentWithdrawalAuthorityMode.ClanLeader),
                "warning month below range");
            AssertThrows<ArgumentException>(
                () => new WithdrawalSettings(
                    4,
                    4,
                    IndependentWithdrawalAuthorityMode.ClanLeader),
                "petition must follow warning");
        }

        private static void TestResponsibilityMapping()
        {
            AssertEqual(
                WithdrawalResponsibility.WithdrawalApproved,
                WithdrawalResponsibilityCalculator.FromDecision(WithdrawalDecision.Approve),
                "approved responsibility");
            AssertEqual(
                WithdrawalResponsibility.CommanderOverride,
                WithdrawalResponsibilityCalculator.FromDecision(WithdrawalDecision.Deny),
                "denied responsibility");
            AssertEqual(
                WithdrawalResponsibility.VoluntaryRefusal,
                WithdrawalResponsibilityCalculator.FromDecision(
                    WithdrawalDecision.ContinueVoluntarily),
                "voluntary responsibility");
            AssertEqual(
                WithdrawalResponsibility.ForcedCircumstances,
                WithdrawalResponsibilityCalculator.FromDecision(WithdrawalDecision.ForcedDelay),
                "forced-delay responsibility");
            AssertEqual(
                WithdrawalResponsibility.None,
                WithdrawalResponsibilityCalculator.FromDecision(WithdrawalDecision.NoDecision),
                "no-decision responsibility");
        }

        private static void TestCommanderLiability()
        {
            int[] expected = { 0, 0, 0, -25, -35, -45, -55, -65, -75 };
            for (int month = 1; month <= 9; month++)
            {
                AssertEqual(
                    expected[month - 1],
                    CommanderLiabilityCalculator.GetDefaultCumulativePenalty(month),
                    "commander liability month " + month);
            }

            AssertEqual(
                0,
                CommanderLiabilityCalculator.GetDefaultCumulativePenalty(10),
                "invalid normalized month does not create liability");

            AssertEqual(
                -10,
                CommanderLiabilityCalculator.GetAdditionalPenalty(-25, -35),
                "month 4 to month 5 delta");
            AssertEqual(
                -55,
                CommanderLiabilityCalculator.GetAdditionalPenalty(0, -55),
                "new commander receives current target");
            AssertEqual(
                0,
                CommanderLiabilityCalculator.GetAdditionalPenalty(-55, -55),
                "same tier does not repeat");
            AssertEqual(
                0,
                CommanderLiabilityCalculator.GetAdditionalPenalty(-65, -55),
                "liability never reverses automatically");
        }

        private static void TestCommanderRelationPenalties()
        {
            AssertEqual(
                -35,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(5, 0),
                "upgraded save applies the full current tier from a fresh relationship ledger");
            AssertEqual(
                -10,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(5, -25),
                "month 5 applies only the difference after a month 4 relationship penalty");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(5, -35),
                "same denial tier cannot repeat its relationship penalty");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(3, 0),
                "warning month creates no relationship penalty");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(10, 0),
                "invalid month creates no relationship penalty");
        }

        private static void TestAuthorityResolution()
        {
            var armyMember = CampaigningMother();
            armyMember.IsArmyMember = true;
            armyMember.ArmyLeaderId = "commander";
            AssertAuthority(
                armyMember,
                IndependentWithdrawalAuthorityMode.ClanLeader,
                WithdrawalAuthorityKind.ArmyLeader,
                "commander");

            var armyLeader = CampaigningMother();
            armyLeader.IsArmyMember = true;
            armyLeader.ArmyLeaderId = "mother";
            AssertAuthority(
                armyLeader,
                IndependentWithdrawalAuthorityMode.ClanLeader,
                WithdrawalAuthorityKind.Self,
                "mother");

            var partyMember = CampaigningMother();
            partyMember.PartyLeaderId = "party_leader";
            AssertAuthority(
                partyMember,
                IndependentWithdrawalAuthorityMode.ClanLeader,
                WithdrawalAuthorityKind.PartyLeader,
                "party_leader");

            var independent = CampaigningMother();
            independent.LeadsIndependentParty = true;
            independent.PartyLeaderId = "mother";
            independent.ClanLeaderId = "clan_leader";
            independent.KingdomRulerId = "ruler";
            AssertAuthority(
                independent,
                IndependentWithdrawalAuthorityMode.ClanLeader,
                WithdrawalAuthorityKind.ClanLeader,
                "clan_leader");
            AssertAuthority(
                independent,
                IndependentWithdrawalAuthorityMode.KingdomRuler,
                WithdrawalAuthorityKind.KingdomRuler,
                "ruler");
            AssertAuthority(
                independent,
                IndependentWithdrawalAuthorityMode.Self,
                WithdrawalAuthorityKind.Self,
                "mother");

            independent.ClanLeaderId = "mother";
            AssertAuthority(
                independent,
                IndependentWithdrawalAuthorityMode.ClanLeader,
                WithdrawalAuthorityKind.Self,
                "mother");

            independent.KingdomRulerId = string.Empty;
            AssertAuthority(
                independent,
                IndependentWithdrawalAuthorityMode.KingdomRuler,
                WithdrawalAuthorityKind.Self,
                "mother");

            var prisoner = CampaigningMother();
            prisoner.IsPrisoner = true;
            AssertNoAuthority(prisoner, "prisoner has no withdrawal authority");

            var resting = CampaigningMother();
            resting.IsResting = true;
            AssertNoAuthority(resting, "resting hero has no withdrawal petition");
        }

        private static void TestFamilyReactions()
        {
            var candidates = new List<FamilyReactionCandidate>
            {
                new FamilyReactionCandidate("mother", FamilyReactionRole.PregnantMother),
                new FamilyReactionCandidate("husband", FamilyReactionRole.SpouseOrOtherParent),
                new FamilyReactionCandidate("parent_one", FamilyReactionRole.MothersParent),
                new FamilyReactionCandidate("parent_two", FamilyReactionRole.MothersParent),
                new FamilyReactionCandidate("sibling_one", FamilyReactionRole.MothersAdultSibling),
                new FamilyReactionCandidate("sibling_two", FamilyReactionRole.MothersAdultSibling)
            };

            IReadOnlyList<FamilyReaction> commanderReactions =
                FamilyReactionCalculator.Calculate(
                    "commander",
                    candidates,
                    FamilyReactionSettings.Default);

            AssertEqual(6, commanderReactions.Count, "all family roles react to commander");
            AssertReaction(commanderReactions, "mother", -50);
            AssertReaction(commanderReactions, "husband", -50);
            AssertReaction(commanderReactions, "parent_one", -10);
            AssertReaction(commanderReactions, "parent_two", -10);
            AssertReaction(commanderReactions, "sibling_one", -5);
            AssertReaction(commanderReactions, "sibling_two", -5);

            IReadOnlyList<FamilyReaction> motherResponsibleReactions =
                FamilyReactionCalculator.Calculate(
                    "mother",
                    candidates,
                    FamilyReactionSettings.Default);
            AssertEqual(
                5,
                motherResponsibleReactions.Count,
                "mother-to-self reaction is skipped");

            var overlappingRoles = new List<FamilyReactionCandidate>
            {
                new FamilyReactionCandidate("same_hero", FamilyReactionRole.MothersAdultSibling),
                new FamilyReactionCandidate("same_hero", FamilyReactionRole.SpouseOrOtherParent)
            };
            IReadOnlyList<FamilyReaction> deduplicated = FamilyReactionCalculator.Calculate(
                "responsible",
                overlappingRoles,
                FamilyReactionSettings.Default);
            AssertEqual(1, deduplicated.Count, "overlapping family roles are deduplicated");
            AssertReaction(deduplicated, "same_hero", -50);
        }

        private static void TestAiWithdrawalDecisions()
        {
            var neutralMonthFour = new AiWithdrawalDecisionInput
            {
                NormalizedMonth = 4
            };
            AiWithdrawalDecisionResult monthFourResult =
                AiWithdrawalDecisionCalculator.Calculate(neutralMonthFour);
            AssertEqual(
                WithdrawalDecision.Deny,
                monthFourResult.Decision,
                "neutral authority may deny at month 4");

            var neutralMonthFive = new AiWithdrawalDecisionInput
            {
                NormalizedMonth = 5
            };
            AiWithdrawalDecisionResult monthFiveResult =
                AiWithdrawalDecisionCalculator.Calculate(neutralMonthFive);
            AssertEqual(
                WithdrawalDecision.Approve,
                monthFiveResult.Decision,
                "neutral authority approves at month 5");

            var cruelMartialMonthEight = new AiWithdrawalDecisionInput
            {
                NormalizedMonth = 8,
                MercyLevel = -2,
                HonorLevel = -2,
                ValorLevel = 2
            };
            AiWithdrawalDecisionResult monthEightResult =
                AiWithdrawalDecisionCalculator.Calculate(cruelMartialMonthEight);
            AssertEqual(
                WithdrawalDecision.Deny,
                monthEightResult.Decision,
                "extreme personality can still deny at month 8");

            cruelMartialMonthEight.NormalizedMonth = 9;
            AiWithdrawalDecisionResult monthNineResult =
                AiWithdrawalDecisionCalculator.Calculate(cruelMartialMonthEight);
            AssertEqual(
                WithdrawalDecision.Approve,
                monthNineResult.Decision,
                "birth-imminent month overcomes even extreme personality");

            var selfRefusal = new AiWithdrawalDecisionInput
            {
                NormalizedMonth = 4,
                IsSelfAuthority = true
            };
            AssertEqual(
                WithdrawalDecision.ContinueVoluntarily,
                AiWithdrawalDecisionCalculator.Calculate(selfRefusal).Decision,
                "self authority owns voluntary refusal");

            AssertEqual(
                WithdrawalDecision.NoDecision,
                AiWithdrawalDecisionCalculator.Calculate(
                    new AiWithdrawalDecisionInput { NormalizedMonth = 3 }).Decision,
                "warning month has no decision");
        }

        private static void TestFamilyReactionSettings()
        {
            var disabled = new FamilyReactionSettings(0, 0, 0, 0);
            IReadOnlyList<FamilyReaction> reactions = FamilyReactionCalculator.Calculate(
                "responsible",
                new[]
                {
                    new FamilyReactionCandidate("mother", FamilyReactionRole.PregnantMother)
                },
                disabled);
            AssertEqual(0, reactions.Count, "zero disables a family reaction");

            AssertThrows<ArgumentOutOfRangeException>(
                () => new FamilyReactionSettings(-101, -50, -10, -5),
                "family penalty below slider range");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new FamilyReactionSettings(1, -50, -10, -5),
                "family penalty above slider range");
        }

        private static void TestProtectedRestTransitions()
        {
            ProtectedRestTransitionResult established =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.Unknown,
                        ProtectedRestState.ProtectedRest,
                        string.Empty,
                        "town_a"));
            AssertEqual(
                ProtectedRestTransitionKind.ProtectedRestEstablished,
                established.Transition,
                "initial protected rest is established");
            AssertEqual(
                ProtectedRestState.ProtectedRest,
                established.NextState,
                "protected rest state persists");
            AssertEqual("town_a", established.ProtectedSettlementId, "rest settlement stored");

            ProtectedRestTransitionResult unchanged =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.ProtectedRest,
                        ProtectedRestState.ProtectedRest,
                        "town_a",
                        "town_a"));
            AssertEqual(
                ProtectedRestTransitionKind.None,
                unchanged.Transition,
                "daily rest observation does not repeat event");

            ProtectedRestTransitionResult voluntaryDeparture =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.ProtectedRest,
                        ProtectedRestState.Campaigning,
                        "town_a",
                        string.Empty));
            AssertEqual(
                ProtectedRestTransitionKind.PresumedVoluntaryDeparture,
                voluntaryDeparture.Transition,
                "leaving rest for field is presumed voluntary");
            AssertEqual(
                WithdrawalResponsibility.VoluntaryRefusal,
                voluntaryDeparture.Responsibility,
                "voluntary departure assigns mother responsibility");
            AssertEqual(
                true,
                voluntaryDeparture.RequiresImmediatePetition,
                "voluntary departure requests immediate petition");

            ProtectedRestTransitionInput defenseInput = RestTransition(
                ProtectedRestState.ProtectedRest,
                ProtectedRestState.Campaigning,
                "town_a",
                "town_a");
            defenseInput.IsDefendingProtectedSettlement = true;
            ProtectedRestTransitionResult defense =
                ProtectedRestTransitionCalculator.Calculate(defenseInput);
            AssertEqual(
                ProtectedRestTransitionKind.DefensiveMobilization,
                defense.Transition,
                "defense of resting settlement is permitted");
            AssertEqual(
                ProtectedRestState.ProtectedDefense,
                defense.NextState,
                "settlement protection remains during defense");
            AssertEqual(
                WithdrawalResponsibility.None,
                defense.Responsibility,
                "settlement defense assigns no blame");

            ProtectedRestTransitionInput differentSettlementDefense = RestTransition(
                ProtectedRestState.ProtectedRest,
                ProtectedRestState.Campaigning,
                "town_a",
                "castle_b");
            differentSettlementDefense.IsDefendingProtectedSettlement = true;
            AssertEqual(
                ProtectedRestTransitionKind.PresumedVoluntaryDeparture,
                ProtectedRestTransitionCalculator.Calculate(
                    differentSettlementDefense).Transition,
                "defense exception applies only to the protected settlement");

            defenseInput.PreviousState = ProtectedRestState.ProtectedDefense;
            ProtectedRestTransitionResult continuedDefense =
                ProtectedRestTransitionCalculator.Calculate(defenseInput);
            AssertEqual(
                ProtectedRestTransitionKind.None,
                continuedDefense.Transition,
                "continued settlement defense does not repeat event");

            ProtectedRestTransitionResult leavesAfterDefense =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.ProtectedDefense,
                        ProtectedRestState.Campaigning,
                        "town_a",
                        string.Empty));
            AssertEqual(
                ProtectedRestTransitionKind.PresumedVoluntaryDeparture,
                leavesAfterDefense.Transition,
                "leaving after local defense resumes voluntary field responsibility");

            ProtectedRestTransitionResult capture =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.ProtectedRest,
                        ProtectedRestState.Prisoner,
                        "town_a",
                        string.Empty));
            AssertEqual(
                ProtectedRestTransitionKind.ForcedRemoval,
                capture.Transition,
                "capture from protected rest is forced");
            AssertEqual(
                WithdrawalResponsibility.ForcedCircumstances,
                capture.Responsibility,
                "capture does not blame mother");

            ProtectedRestTransitionResult unresolved =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.ProtectedRest,
                        ProtectedRestState.Unavailable,
                        "town_a",
                        string.Empty));
            AssertEqual(
                ProtectedRestTransitionKind.UnresolvedDeparture,
                unresolved.Transition,
                "unknown removal fails without voluntary blame");
            AssertEqual(
                WithdrawalResponsibility.ForcedCircumstances,
                unresolved.Responsibility,
                "unknown removal remains forced pending evidence");

            ProtectedRestTransitionResult returned =
                ProtectedRestTransitionCalculator.Calculate(
                    RestTransition(
                        ProtectedRestState.Campaigning,
                        ProtectedRestState.ProtectedRest,
                        string.Empty,
                        "castle_b"));
            AssertEqual(
                ProtectedRestTransitionKind.ReturnedToProtectedRest,
                returned.Transition,
                "campaigner can return to protected rest");

            AssertThrows<ArgumentNullException>(
                () => ProtectedRestTransitionCalculator.Calculate(null),
                "null protected-rest transition input");
        }

        private static ProtectedRestTransitionInput RestTransition(
            ProtectedRestState previous,
            ProtectedRestState observed,
            string protectedSettlementId,
            string observedSettlementId)
        {
            return new ProtectedRestTransitionInput
            {
                PreviousState = previous,
                ObservedState = observed,
                ProtectedSettlementId = protectedSettlementId,
                ObservedSettlementId = observedSettlementId
            };
        }

        private static WithdrawalAuthorityContext CampaigningMother()
        {
            return new WithdrawalAuthorityContext
            {
                MotherId = "mother",
                IsCampaigning = true
            };
        }

        private static void AssertStage(
            int month,
            WithdrawalSettings settings,
            WithdrawalMonthStage expected)
        {
            WithdrawalMonthResult result = WithdrawalMonthCalculator.Calculate(month, settings);
            AssertEqual(expected, result.Stage, "withdrawal stage month " + month);
        }

        private static void AssertAuthority(
            WithdrawalAuthorityContext context,
            IndependentWithdrawalAuthorityMode mode,
            WithdrawalAuthorityKind expectedKind,
            string expectedHeroId)
        {
            WithdrawalAuthorityResult result = WithdrawalAuthorityResolver.Resolve(context, mode);
            if (!result.HasAuthority)
            {
                throw new InvalidOperationException(
                    "Expected authority but received: " + result.NoDecisionReason);
            }

            AssertEqual(expectedKind, result.Kind, "authority kind");
            AssertEqual(expectedHeroId, result.AuthorityId, "authority hero");
        }

        private static void AssertNoAuthority(
            WithdrawalAuthorityContext context,
            string name)
        {
            WithdrawalAuthorityResult result = WithdrawalAuthorityResolver.Resolve(
                context,
                IndependentWithdrawalAuthorityMode.ClanLeader);
            if (result.HasAuthority || string.IsNullOrWhiteSpace(result.NoDecisionReason))
            {
                throw new InvalidOperationException(name + " failed.");
            }
        }

        private static void AssertReaction(
            IReadOnlyList<FamilyReaction> reactions,
            string heroId,
            int expectedChange)
        {
            foreach (FamilyReaction reaction in reactions)
            {
                if (reaction.HeroId == heroId)
                {
                    AssertEqual(expectedChange, reaction.RelationChange, "reaction for " + heroId);
                    return;
                }
            }

            throw new InvalidOperationException("No family reaction found for " + heroId + ".");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    name + " failed. Expected=" + expected + ", actual=" + actual + ".");
            }
        }

        private static void AssertThrows<TException>(Action action, string name)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(name + " should throw " + typeof(TException).Name + ".");
        }

        private static void AssertKnown(
            double currentDay,
            double conceptionDay,
            double dueDay,
            double expectedProgress,
            int expectedMonth,
            string name)
        {
            double progress;
            int month;
            string failure;
            bool success = PregnancyProgressCalculator.TryCalculate(
                currentDay,
                conceptionDay,
                dueDay,
                out progress,
                out month,
                out failure);

            if (!success
                || Math.Abs(progress - expectedProgress) > 0.000001
                || month != expectedMonth)
            {
                throw new InvalidOperationException(
                    name + " failed. Success=" + success
                    + ", progress=" + progress
                    + ", month=" + month
                    + ", reason=" + failure);
            }
        }

        private static void AssertInvalid(
            double currentDay,
            double conceptionDay,
            double dueDay,
            string name)
        {
            double progress;
            int month;
            string failure;
            bool success = PregnancyProgressCalculator.TryCalculate(
                currentDay,
                conceptionDay,
                dueDay,
                out progress,
                out month,
                out failure);

            if (success || string.IsNullOrEmpty(failure))
            {
                throw new InvalidOperationException(name + " should fail safely.");
            }
        }
    }
}
