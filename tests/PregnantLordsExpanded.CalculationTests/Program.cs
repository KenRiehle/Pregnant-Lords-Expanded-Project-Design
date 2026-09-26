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

            TestPlayerWithdrawalDecisions();

            Console.WriteLine("All Milestone 2D-B player decision tests passed.");

            TestPregnancyBattleRisk();

            Console.WriteLine("All Milestone 2D-C graduated consequence tests passed.");

            TestMonthlyDenialResentment();

            Console.WriteLine("All Milestone 2D-D monthly denial resentment tests passed.");

            TestApprovedWithdrawalExecution();
            TestPregnancyServiceRestriction();
            TestLeaderOnlyArmyCleanup();

            Console.WriteLine("All Milestone 2D-E approved withdrawal execution, service restriction, and PLE-only army cleanup tests passed.");

            TestWithdrawalEscortPlanning();

            Console.WriteLine("All Milestone 2D-F-A withdrawal planning and elite escort allocation tests passed.");
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
            int[] expected = { 0, 0, 0, -5, -5, -5, -5, -5, -5 };
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
                0,
                CommanderLiabilityCalculator.GetAdditionalPenalty(-5, -5),
                "healthy renewed petition does not stack resentment");
            AssertEqual(
                -5,
                CommanderLiabilityCalculator.GetAdditionalPenalty(0, -5),
                "new commander receives the minor denial target");
            AssertEqual(
                0,
                CommanderLiabilityCalculator.GetAdditionalPenalty(-5, -5),
                "same tier does not repeat");
            AssertEqual(
                0,
                CommanderLiabilityCalculator.GetAdditionalPenalty(-25, -5),
                "liability never reverses automatically");
        }

        private static void TestCommanderRelationPenalties()
        {
            AssertEqual(
                -5,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(5, 0),
                "fresh denial applies the minor healthy target");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(5, -5),
                "renewed petition does not stack healthy denial resentment");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(5, -25),
                "revised target never refunds an older stronger penalty");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(3, 0),
                "warning month creates no relationship penalty");
            AssertEqual(
                0,
                CommanderRelationPenaltyCalculator.GetPendingPenalty(10, 0),
                "invalid month creates no relationship penalty");
        }

        private static void TestMonthlyDenialResentment()
        {
            AssertEqual(
                0,
                MonthlyDenialResentmentCalculator.GetPendingPenalty(3, false),
                "month three has no routine denial resentment");
            AssertEqual(
                -5,
                MonthlyDenialResentmentCalculator.GetPendingPenalty(4, false),
                "month four fresh denial applies minus five");
            AssertEqual(
                0,
                MonthlyDenialResentmentCalculator.GetPendingPenalty(4, true),
                "same month cannot repeat after reload or another evaluation");
            AssertEqual(
                -5,
                MonthlyDenialResentmentCalculator.GetPendingPenalty(5, false),
                "separate month five denial applies another minus five");
            AssertEqual(
                -5,
                MonthlyDenialResentmentCalculator.GetPendingPenalty(9, false),
                "month nine remains eligible");
            AssertEqual(
                0,
                MonthlyDenialResentmentCalculator.GetPendingPenalty(10, false),
                "month ten is outside routine resentment");
            AssertEqual(
                -30,
                MonthlyDenialResentmentCalculator.GetMaximumRoutinePenalty(),
                "six denied months cap routine resentment at minus thirty");

            var appliedKeys = new HashSet<string>();
            int total = 0;
            for (int month = 4; month <= 9; month++)
            {
                string key = MonthlyDenialResentmentCalculator.GetLedgerKey(
                    "mother#1",
                    month);
                bool alreadyApplied = appliedKeys.Contains(key);
                int first = MonthlyDenialResentmentCalculator.GetPendingPenalty(
                    month,
                    alreadyApplied);
                total += first;
                appliedKeys.Add(key);

                AssertEqual(
                    0,
                    MonthlyDenialResentmentCalculator.GetPendingPenalty(
                        month,
                        appliedKeys.Contains(key)),
                    "same normalized month remains idempotent " + month);
            }

            AssertEqual(-30, total, "months four through nine total minus thirty");

            int parsedMonth;
            AssertEqual(
                true,
                MonthlyDenialResentmentCalculator.TryGetNormalizedMonthFromRequestKey(
                    "mother#1|month:4",
                    out parsedMonth),
                "ordinary request month parses");
            AssertEqual(4, parsedMonth, "ordinary request parsed month");
            AssertEqual(
                true,
                MonthlyDenialResentmentCalculator.TryGetNormalizedMonthFromRequestKey(
                    "mother#1|departure:2|month:6",
                    out parsedMonth),
                "departure request month parses");
            AssertEqual(6, parsedMonth, "departure request parsed month");
            AssertEqual(
                false,
                MonthlyDenialResentmentCalculator.TryGetNormalizedMonthFromRequestKey(
                    "mother#1|month:3",
                    out parsedMonth),
                "warning month is not a routine denial migration month");
            AssertEqual(
                false,
                MonthlyDenialResentmentCalculator.TryGetNormalizedMonthFromRequestKey(
                    "mother#1|month:not-a-number",
                    out parsedMonth),
                "invalid request month does not parse");
        }

        private static void TestApprovedWithdrawalExecution()
        {
            ApprovedWithdrawalExecutionResult fresh =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    ApprovedWithdrawalExecutionState.None,
                    false,
                    false,
                    true);
            AssertEqual(
                ApprovedWithdrawalExecutionState.Traveling,
                fresh.NextState,
                "fresh approval begins native travel");
            AssertEqual(true, fresh.ShouldBeginTravel,
                "fresh approval requests exactly one travel start");

            ApprovedWithdrawalExecutionResult blocked =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    ApprovedWithdrawalExecutionState.Pending,
                    false,
                    false,
                    false);
            AssertEqual(
                ApprovedWithdrawalExecutionState.Pending,
                blocked.NextState,
                "temporarily blocked approval remains pending");
            AssertEqual(false, blocked.ShouldBeginTravel,
                "blocked approval does not start travel");

            ApprovedWithdrawalExecutionResult reloadedTravel =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    ApprovedWithdrawalExecutionState.Traveling,
                    true,
                    false,
                    true);
            AssertEqual(
                ApprovedWithdrawalExecutionState.Traveling,
                reloadedTravel.NextState,
                "reload preserves an active native trip");
            AssertEqual(false, reloadedTravel.ShouldBeginTravel,
                "reload cannot start the same active trip twice");

            ApprovedWithdrawalExecutionResult arrived =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    ApprovedWithdrawalExecutionState.Traveling,
                    false,
                    true,
                    true);
            AssertEqual(
                ApprovedWithdrawalExecutionState.Completed,
                arrived.NextState,
                "arrival at protected settlement completes withdrawal");
            AssertEqual(false, arrived.ShouldBeginTravel,
                "completed withdrawal never restarts travel");

            ApprovedWithdrawalExecutionResult canceledRetry =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    ApprovedWithdrawalExecutionState.Traveling,
                    false,
                    false,
                    true);
            AssertEqual(
                ApprovedWithdrawalExecutionState.Traveling,
                canceledRetry.NextState,
                "canceled native trip may immediately retry");
            AssertEqual(true, canceledRetry.ShouldBeginTravel,
                "canceled native trip requests one retry");

            ApprovedWithdrawalExecutionResult completedStable =
                ApprovedWithdrawalExecutionCalculator.Calculate(
                    ApprovedWithdrawalExecutionState.Completed,
                    false,
                    false,
                    true);
            AssertEqual(
                ApprovedWithdrawalExecutionState.Completed,
                completedStable.NextState,
                "completed historical execution remains complete after later departure");
            AssertEqual(false, completedStable.ShouldBeginTravel,
                "completed historical execution cannot replay");
        }

        private static void TestPregnancyServiceRestriction()
        {
            AssertEqual(
                false,
                PregnancyServiceRestrictionCalculator.ShouldRestrict(
                    ApprovedWithdrawalExecutionState.None,
                    true),
                "pregnancy without approved withdrawal is not service restricted");

            AssertEqual(
                true,
                PregnancyServiceRestrictionCalculator.ShouldRestrict(
                    ApprovedWithdrawalExecutionState.Pending,
                    true),
                "approved pending withdrawal blocks field service");

            AssertEqual(
                true,
                PregnancyServiceRestrictionCalculator.ShouldRestrict(
                    ApprovedWithdrawalExecutionState.Traveling,
                    true),
                "traveling withdrawal blocks field service");

            AssertEqual(
                true,
                PregnancyServiceRestrictionCalculator.ShouldRestrict(
                    ApprovedWithdrawalExecutionState.Completed,
                    true),
                "completed travel remains service restricted during pregnancy");

            AssertEqual(
                false,
                PregnancyServiceRestrictionCalculator.ShouldRestrict(
                    ApprovedWithdrawalExecutionState.Completed,
                    false),
                "pregnancy ending clears service restriction");
        }

        private static void TestLeaderOnlyArmyCleanup()
        {
            AssertEqual(
                true,
                LeaderOnlyArmyCleanupCalculator.ShouldDisbandAfterPleRemoval(
                    true,
                    false,
                    0),
                "PLE removal that leaves only the army leader queues native army disband");

            AssertEqual(
                false,
                LeaderOnlyArmyCleanupCalculator.ShouldDisbandAfterPleRemoval(
                    true,
                    false,
                    1),
                "PLE removal does not disband an army that still has another attached party");

            AssertEqual(
                false,
                LeaderOnlyArmyCleanupCalculator.ShouldDisbandAfterPleRemoval(
                    false,
                    false,
                    0),
                "ordinary native one-party army is not disbanded by PLE");

            AssertEqual(
                false,
                LeaderOnlyArmyCleanupCalculator.ShouldDisbandAfterPleRemoval(
                    true,
                    true,
                    0),
                "army-leader withdrawal relies on Bannerlord native leader removal handling");
        }

        private static void TestPregnancyBattleRisk()
        {
            PregnancyBattleRiskResult unhurt = CalculateBattleRisk(
                true,
                false,
                100.0,
                75.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(false, unhurt.ShouldEvaluate,
                "seventy-five percent health does not qualify");
            AssertEqual(0, unhurt.PregnancyLossChancePercent,
                "unhurt pregnancy has no battle loss roll");

            PregnancyBattleRiskResult significant = CalculateBattleRisk(
                true,
                false,
                100.0,
                74.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(true, significant.ShouldEvaluate,
                "health below seventy-five percent qualifies");
            AssertEqual(PregnancyBattleInjurySeverity.Significant,
                significant.InjurySeverity,
                "fifty to seventy-four percent is significant");
            AssertEqual(10, significant.PregnancyLossChancePercent,
                "significant wound pregnancy-loss chance");
            AssertEqual(-10, significant.ResponsiblePartyRelationTarget,
                "significant wound commander relation target");

            PregnancyBattleRiskResult exactFifty = CalculateBattleRisk(
                true,
                false,
                100.0,
                50.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(PregnancyBattleInjurySeverity.Significant,
                exactFifty.InjurySeverity,
                "exactly fifty percent remains significant");
            AssertEqual(10, exactFifty.PregnancyLossChancePercent,
                "exactly fifty percent uses significant risk");

            PregnancyBattleRiskResult severe = CalculateBattleRisk(
                true,
                false,
                90.0,
                49.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(PregnancyBattleInjurySeverity.Severe,
                severe.InjurySeverity,
                "twenty-five to forty-nine percent is severe");
            AssertEqual(30, severe.PregnancyLossChancePercent,
                "severe wound pregnancy-loss chance");
            AssertEqual(-25, severe.ResponsiblePartyRelationTarget,
                "severe wound commander relation target");
            AssertEqual(true, severe.IsPregnancyLossRoll(29),
                "severe wound roll below thirty loses pregnancy");
            AssertEqual(false, severe.IsPregnancyLossRoll(30),
                "severe wound roll at thirty preserves pregnancy");

            PregnancyBattleRiskResult exactTwentyFive = CalculateBattleRisk(
                true,
                false,
                100.0,
                25.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(PregnancyBattleInjurySeverity.Severe,
                exactTwentyFive.InjurySeverity,
                "exactly twenty-five percent remains severe");
            AssertEqual(30, exactTwentyFive.PregnancyLossChancePercent,
                "exactly twenty-five percent uses severe risk");

            PregnancyBattleRiskResult critical = CalculateBattleRisk(
                true,
                false,
                60.0,
                24.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(PregnancyBattleInjurySeverity.Critical,
                critical.InjurySeverity,
                "below twenty-five percent is critical");
            AssertEqual(50, critical.PregnancyLossChancePercent,
                "critical wound pregnancy-loss chance");
            AssertEqual(-35, critical.ResponsiblePartyRelationTarget,
                "critical wound commander relation target");

            PregnancyBattleRiskResult voluntary = CalculateBattleRisk(
                true,
                false,
                100.0,
                40.0,
                WithdrawalResponsibility.VoluntaryRefusal);
            AssertEqual(30, voluntary.PregnancyLossChancePercent,
                "voluntary continuation does not remove physical risk");
            AssertEqual(0, voluntary.ResponsiblePartyRelationTarget,
                "voluntary continuation does not blame commander");

            PregnancyBattleRiskResult noNewDamage = CalculateBattleRisk(
                true,
                false,
                40.0,
                40.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(false, noNewDamage.ShouldEvaluate,
                "pre-existing low health without new damage cannot reroll");

            PregnancyBattleRiskResult processed = CalculateBattleRisk(
                true,
                true,
                100.0,
                20.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(false, processed.ShouldEvaluate,
                "processed battle cannot reroll after reload");

            PregnancyBattleRiskResult notPregnant = CalculateBattleRisk(
                false,
                false,
                100.0,
                20.0,
                WithdrawalResponsibility.CommanderOverride);
            AssertEqual(false, notPregnant.ShouldEvaluate,
                "non-pregnant hero never receives pregnancy battle roll");

            AssertThrows<ArgumentOutOfRangeException>(
                () => severe.IsPregnancyLossRoll(100),
                "pregnancy-loss roll validates range");
            AssertThrows<ArgumentOutOfRangeException>(
                () => CalculateBattleRisk(
                    true,
                    false,
                    101.0,
                    20.0,
                    WithdrawalResponsibility.CommanderOverride),
                "battle risk validates health percentage");
        }

        private static PregnancyBattleRiskResult CalculateBattleRisk(
            bool isPregnant,
            bool battleAlreadyProcessed,
            double healthBefore,
            double healthAfter,
            WithdrawalResponsibility responsibility)
        {
            return PregnancyBattleRiskCalculator.Calculate(
                new PregnancyBattleRiskInput
                {
                    IsPregnant = isPregnant,
                    BattleAlreadyProcessed = battleAlreadyProcessed,
                    HealthBeforeBattlePercent = healthBefore,
                    HealthAfterBattlePercent = healthAfter,
                    Responsibility = responsibility
                });
        }

        private static void TestPlayerWithdrawalDecisions()
        {
            PlayerWithdrawalResolution playerApproves =
                PlayerWithdrawalDecisionCalculator.ResolvePlayerAuthority(
                    PlayerWithdrawalChoice.ApprovePetition);
            AssertEqual(
                WithdrawalDecision.Approve,
                playerApproves.AuthorityDecision,
                "player authority approval");
            AssertEqual(
                WithdrawalResponsibility.WithdrawalApproved,
                playerApproves.Responsibility,
                "player approval responsibility");
            AssertEqual(false, playerApproves.ApplyCommanderRelationPenalty,
                "player approval has no commander penalty");

            PlayerWithdrawalResolution playerDenies =
                PlayerWithdrawalDecisionCalculator.ResolvePlayerAuthority(
                    PlayerWithdrawalChoice.DenyPetition);
            AssertEqual(
                WithdrawalDecision.Deny,
                playerDenies.FinalDecision,
                "player authority denial");
            AssertEqual(
                WithdrawalResponsibility.CommanderOverride,
                playerDenies.Responsibility,
                "player denial responsibility");
            AssertEqual(true, playerDenies.ApplyCommanderRelationPenalty,
                "player denial applies commander penalty");

            PlayerWithdrawalResolution selfWithdraws =
                PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    WithdrawalDecision.NoDecision,
                    PlayerWithdrawalChoice.Withdraw);
            AssertEqual(true, selfWithdraws.WithdrawalAuthorized,
                "self-authorized player withdraws");
            AssertEqual(
                WithdrawalResponsibility.WithdrawalApproved,
                selfWithdraws.Responsibility,
                "self withdrawal responsibility");

            PlayerWithdrawalResolution selfContinues =
                PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    WithdrawalDecision.NoDecision,
                    PlayerWithdrawalChoice.ContinueCampaigning);
            AssertEqual(
                WithdrawalDecision.ContinueVoluntarily,
                selfContinues.FinalDecision,
                "self-authorized player continues voluntarily");
            AssertEqual(
                WithdrawalResponsibility.VoluntaryRefusal,
                selfContinues.Responsibility,
                "self continuation responsibility");

            PlayerWithdrawalResolution continuesAfterApproval =
                PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    WithdrawalDecision.Approve,
                    PlayerWithdrawalChoice.ContinueCampaigning);
            AssertEqual(
                WithdrawalResponsibility.VoluntaryRefusal,
                continuesAfterApproval.Responsibility,
                "player owns decision to remain after approval");
            AssertEqual(false, continuesAfterApproval.ApplyCommanderRelationPenalty,
                "approved withdrawal has no commander penalty");

            PlayerWithdrawalResolution withdrawsDespiteDenial =
                PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    WithdrawalDecision.Deny,
                    PlayerWithdrawalChoice.Withdraw);
            AssertEqual(true, withdrawsDespiteDenial.WithdrawalAuthorized,
                "player may withdraw despite denial");
            AssertEqual(
                WithdrawalResponsibility.WithdrawalApproved,
                withdrawsDespiteDenial.Responsibility,
                "leaving despite denial avoids later commander loss responsibility");
            AssertEqual(true, withdrawsDespiteDenial.ApplyCommanderRelationPenalty,
                "denial still damages commander relationship");

            PlayerWithdrawalResolution remainsAsOrdered =
                PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    WithdrawalDecision.Deny,
                    PlayerWithdrawalChoice.ContinueCampaigning);
            AssertEqual(
                WithdrawalResponsibility.CommanderOverride,
                remainsAsOrdered.Responsibility,
                "commander owns ordered continuation");
            AssertEqual(true, remainsAsOrdered.ApplyCommanderRelationPenalty,
                "ordered continuation applies commander penalty");

            AssertThrows<ArgumentOutOfRangeException>(
                () => PlayerWithdrawalDecisionCalculator.ResolvePlayerAuthority(
                    PlayerWithdrawalChoice.Withdraw),
                "authority prompt rejects pregnant-player choice");
            AssertThrows<ArgumentOutOfRangeException>(
                () => PlayerWithdrawalDecisionCalculator.ResolvePregnantPlayer(
                    WithdrawalDecision.Approve,
                    PlayerWithdrawalChoice.DenyPetition),
                "pregnant-player prompt rejects authority choice");
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
            AssertReaction(commanderReactions, "mother", -75);
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


        private static void TestWithdrawalEscortPlanning()
        {
            WithdrawalEscortPlanDefinition minimal =
                WithdrawalEscortPlanner.GetDefinition(WithdrawalEscortPlan.Minimal);
            AssertEqual(5, minimal.RequestedEscortSize,
                "minimal escort requests five troops");
            AssertEqual(-25, minimal.RelationshipChangeWithMother,
                "minimal escort applies minus twenty-five relationship");
            AssertEqual(WithdrawalEscortSafetyLevel.SevereRisk, minimal.SafetyLevel,
                "minimal escort is severe welfare risk");
            AssertEqual(true, minimal.RequiresWelfareWarning,
                "minimal escort requires welfare warning");

            WithdrawalEscortPlanDefinition lean =
                WithdrawalEscortPlanner.GetDefinition(WithdrawalEscortPlan.Lean);
            AssertEqual(35, lean.RequestedEscortSize,
                "lean escort requests thirty-five troops");
            AssertEqual(0, lean.RelationshipChangeWithMother,
                "lean escort has no immediate relationship change");
            AssertEqual(WithdrawalEscortSafetyLevel.Standard, lean.SafetyLevel,
                "lean escort is standard protection");

            WithdrawalEscortPlanDefinition strong =
                WithdrawalEscortPlanner.GetDefinition(WithdrawalEscortPlan.Strong);
            AssertEqual(50, strong.RequestedEscortSize,
                "strong escort requests fifty troops");
            AssertEqual(5, strong.RelationshipChangeWithMother,
                "strong escort applies plus five relationship");
            AssertEqual(WithdrawalEscortSafetyLevel.High, strong.SafetyLevel,
                "strong escort is high protection");

            var exactFifty = new[]
            {
                EscortStack("tier6", 6, 31, 5),
                EscortStack("tier5", 5, 26, 10),
                EscortStack("tier4", 4, 21, 20),
                EscortStack("tier2", 2, 11, 15)
            };

            WithdrawalEscortAllocation strongFifty =
                WithdrawalEscortPlanner.Calculate(WithdrawalEscortPlan.Strong, exactFifty);
            AssertEqual(50, strongFifty.OriginalTroopCount,
                "strong fifty original count");
            AssertEqual(50, strongFifty.ActualEscortSize,
                "strong fifty retains all fifty");
            AssertEqual(0, strongFifty.SurplusTroopCount,
                "strong fifty has no surplus");
            AssertEqual(true, strongFifty.IsConserved,
                "strong fifty conserves all troops");

            WithdrawalEscortAllocation leanFifty =
                WithdrawalEscortPlanner.Calculate(WithdrawalEscortPlan.Lean, exactFifty);
            AssertEqual(35, leanFifty.ActualEscortSize,
                "lean fifty retains thirty-five");
            AssertEqual(15, leanFifty.SurplusTroopCount,
                "lean fifty identifies fifteen surplus");
            AssertEscortStack(leanFifty, "tier6", 5, 0);
            AssertEscortStack(leanFifty, "tier5", 10, 0);
            AssertEscortStack(leanFifty, "tier4", 20, 0);
            AssertEscortStack(leanFifty, "tier2", 0, 15);
            AssertEqual(true, leanFifty.IsConserved,
                "lean fifty conserves all troops");

            WithdrawalEscortAllocation minimalFifty =
                WithdrawalEscortPlanner.Calculate(WithdrawalEscortPlan.Minimal, exactFifty);
            AssertEqual(5, minimalFifty.ActualEscortSize,
                "minimal fifty retains five");
            AssertEqual(45, minimalFifty.SurplusTroopCount,
                "minimal fifty identifies forty-five surplus");
            AssertEscortStack(minimalFifty, "tier6", 5, 0);
            AssertEscortStack(minimalFifty, "tier5", 0, 10);
            AssertEscortStack(minimalFifty, "tier4", 0, 20);
            AssertEscortStack(minimalFifty, "tier2", 0, 15);
            AssertEqual(true, minimalFifty.IsConserved,
                "minimal fifty conserves all troops");

            WithdrawalEscortAllocation emptyStrong =
                WithdrawalEscortPlanner.Calculate(
                    WithdrawalEscortPlan.Strong,
                    new WithdrawalEscortTroopStack[0]);
            AssertEqual(0, emptyStrong.OriginalTroopCount,
                "empty strong original count");
            AssertEqual(0, emptyStrong.ActualEscortSize,
                "empty strong creates no troops");
            AssertEqual(0, emptyStrong.SurplusTroopCount,
                "empty strong has no surplus");
            AssertEqual(true, emptyStrong.IsConserved,
                "empty strong remains conserved");

            WithdrawalEscortAllocation strongFortyNine =
                WithdrawalEscortPlanner.Calculate(
                    WithdrawalEscortPlan.Strong,
                    new[] { EscortStack("troop", 3, 15, 49) });
            AssertEqual(49, strongFortyNine.ActualEscortSize,
                "strong escort never creates the missing fiftieth troop");
            AssertEqual(0, strongFortyNine.SurplusTroopCount,
                "strong forty-nine has no surplus");

            WithdrawalEscortAllocation leanThirtySix =
                WithdrawalEscortPlanner.Calculate(
                    WithdrawalEscortPlan.Lean,
                    new[] { EscortStack("troop", 3, 15, 36) });
            AssertEqual(35, leanThirtySix.ActualEscortSize,
                "lean thirty-six retains thirty-five");
            AssertEqual(1, leanThirtySix.SurplusTroopCount,
                "lean thirty-six identifies one surplus");

            WithdrawalEscortAllocation leanThirtyFour =
                WithdrawalEscortPlanner.Calculate(
                    WithdrawalEscortPlan.Lean,
                    new[] { EscortStack("troop", 3, 15, 34) });
            AssertEqual(34, leanThirtyFour.ActualEscortSize,
                "lean thirty-four retains every available troop");
            AssertEqual(0, leanThirtyFour.SurplusTroopCount,
                "lean thirty-four creates no troops and no surplus");

            var splitRoster = new[]
            {
                EscortStack("t6", 6, 31, 5),
                EscortStack("t5", 5, 26, 10),
                EscortStack("t4", 4, 21, 25),
                EscortStack("t3", 3, 16, 30)
            };
            WithdrawalEscortAllocation split =
                WithdrawalEscortPlanner.Calculate(WithdrawalEscortPlan.Lean, splitRoster);
            AssertEscortStack(split, "t6", 5, 0);
            AssertEscortStack(split, "t5", 10, 0);
            AssertEscortStack(split, "t4", 20, 5);
            AssertEscortStack(split, "t3", 0, 30);
            AssertEqual(70, split.OriginalTroopCount,
                "split cutoff original count");
            AssertEqual(35, split.ActualEscortSize,
                "split cutoff actual escort");
            AssertEqual(35, split.SurplusTroopCount,
                "split cutoff surplus");
            AssertEqual(true, split.IsConserved,
                "split cutoff conserves every troop");

            var deterministicTie = new[]
            {
                EscortStack("zeta", 6, 30, 5),
                EscortStack("alpha", 6, 30, 5)
            };
            WithdrawalEscortAllocation tie =
                WithdrawalEscortPlanner.Calculate(
                    WithdrawalEscortPlan.Minimal,
                    deterministicTie);
            AssertEscortStack(tie, "alpha", 5, 0);
            AssertEscortStack(tie, "zeta", 0, 5);

            AssertThrows<ArgumentNullException>(
                () => WithdrawalEscortPlanner.Calculate(
                    WithdrawalEscortPlan.Lean,
                    null),
                "null escort roster");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new WithdrawalEscortTroopStack("bad", 1, 1, 0),
                "zero-sized troop stack");
        }

        private static WithdrawalEscortTroopStack EscortStack(
            string troopId,
            int tier,
            int level,
            int count)
        {
            return new WithdrawalEscortTroopStack(troopId, tier, level, count);
        }

        private static void AssertEscortStack(
            WithdrawalEscortAllocation allocation,
            string troopId,
            int expectedRetained,
            int expectedSurplus)
        {
            foreach (WithdrawalEscortStackAllocation stack in allocation.Stacks)
            {
                if (stack.TroopId == troopId)
                {
                    AssertEqual(expectedRetained, stack.RetainedCount,
                        "escort retained count for " + troopId);
                    AssertEqual(expectedSurplus, stack.SurplusCount,
                        "escort surplus count for " + troopId);
                    AssertEqual(stack.OriginalCount,
                        stack.RetainedCount + stack.SurplusCount,
                        "escort stack conservation for " + troopId);
                    return;
                }
            }

            throw new InvalidOperationException(
                "No escort allocation found for " + troopId + ".");
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
