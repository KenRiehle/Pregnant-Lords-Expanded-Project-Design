using System;
using System.Collections.Generic;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Milestone 2D-F-A escort choices for an already-approved withdrawal.
    /// Remain-in-service is deliberately not represented here because it is a
    /// withdrawal decision, not an escort plan.
    /// </summary>
    public enum WithdrawalEscortPlan
    {
        Minimal = 0,
        Lean = 1,
        Strong = 2
    }

    public enum WithdrawalEscortSafetyLevel
    {
        SevereRisk = 0,
        Standard = 1,
        High = 2
    }

    public sealed class WithdrawalEscortPlanDefinition
    {
        public WithdrawalEscortPlanDefinition(
            WithdrawalEscortPlan plan,
            int requestedEscortSize,
            int relationshipChangeWithMother,
            WithdrawalEscortSafetyLevel safetyLevel)
        {
            Plan = plan;
            RequestedEscortSize = requestedEscortSize;
            RelationshipChangeWithMother = relationshipChangeWithMother;
            SafetyLevel = safetyLevel;
        }

        public WithdrawalEscortPlan Plan { get; }

        public int RequestedEscortSize { get; }

        public int RelationshipChangeWithMother { get; }

        public WithdrawalEscortSafetyLevel SafetyLevel { get; }

        public bool RequiresWelfareWarning => SafetyLevel == WithdrawalEscortSafetyLevel.SevereRisk;
    }

    /// <summary>
    /// Bannerlord-independent representation of one ordinary troop stack.
    /// The campaign adapter will create these from the party roster later.
    /// </summary>
    public sealed class WithdrawalEscortTroopStack
    {
        public WithdrawalEscortTroopStack(
            string troopId,
            int tier,
            int level,
            int count)
        {
            if (string.IsNullOrWhiteSpace(troopId))
            {
                throw new ArgumentException("Troop id is required.", nameof(troopId));
            }

            if (tier < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tier));
            }

            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            TroopId = troopId;
            Tier = tier;
            Level = level;
            Count = count;
        }

        public string TroopId { get; }

        public int Tier { get; }

        public int Level { get; }

        public int Count { get; }
    }

    public sealed class WithdrawalEscortStackAllocation
    {
        public WithdrawalEscortStackAllocation(
            string troopId,
            int tier,
            int level,
            int originalCount,
            int retainedCount,
            int surplusCount)
        {
            TroopId = troopId ?? string.Empty;
            Tier = tier;
            Level = level;
            OriginalCount = originalCount;
            RetainedCount = retainedCount;
            SurplusCount = surplusCount;
        }

        public string TroopId { get; }

        public int Tier { get; }

        public int Level { get; }

        public int OriginalCount { get; }

        public int RetainedCount { get; }

        public int SurplusCount { get; }
    }

    public sealed class WithdrawalEscortAllocation
    {
        public WithdrawalEscortAllocation(
            WithdrawalEscortPlanDefinition definition,
            int originalTroopCount,
            int actualEscortSize,
            IReadOnlyList<WithdrawalEscortStackAllocation> stacks)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            OriginalTroopCount = originalTroopCount;
            ActualEscortSize = actualEscortSize;
            Stacks = stacks ?? throw new ArgumentNullException(nameof(stacks));

            int retained = 0;
            int surplus = 0;
            int stackOriginal = 0;
            foreach (WithdrawalEscortStackAllocation stack in Stacks)
            {
                if (stack == null)
                {
                    continue;
                }

                retained = checked(retained + stack.RetainedCount);
                surplus = checked(surplus + stack.SurplusCount);
                stackOriginal = checked(stackOriginal + stack.OriginalCount);
            }

            RetainedTroopCount = retained;
            SurplusTroopCount = surplus;
            StackOriginalTroopCount = stackOriginal;
        }

        public WithdrawalEscortPlanDefinition Definition { get; }

        public WithdrawalEscortPlan Plan => Definition.Plan;

        public int RequestedEscortSize => Definition.RequestedEscortSize;

        public int RelationshipChangeWithMother => Definition.RelationshipChangeWithMother;

        public WithdrawalEscortSafetyLevel SafetyLevel => Definition.SafetyLevel;

        public bool RequiresWelfareWarning => Definition.RequiresWelfareWarning;

        public int OriginalTroopCount { get; }

        public int ActualEscortSize { get; }

        public int RetainedTroopCount { get; }

        public int SurplusTroopCount { get; }

        public int StackOriginalTroopCount { get; }

        public IReadOnlyList<WithdrawalEscortStackAllocation> Stacks { get; }

        public bool IsConserved =>
            OriginalTroopCount == StackOriginalTroopCount
            && OriginalTroopCount == RetainedTroopCount + SurplusTroopCount
            && ActualEscortSize == RetainedTroopCount;
    }

    /// <summary>
    /// Pure Milestone 2D-F-A planning layer. It never changes a Bannerlord roster.
    /// It only determines the elite escort that should be retained and which
    /// ordinary troops become surplus candidates for the later handoff milestone.
    /// </summary>
    public static class WithdrawalEscortPlanner
    {
        public const int MinimalEscortSize = 5;
        public const int LeanEscortSize = 35;
        public const int StrongEscortSize = 50;

        public const int MinimalRelationshipChange = -25;
        public const int LeanRelationshipChange = 0;
        public const int StrongRelationshipChange = 5;

        public static WithdrawalEscortPlanDefinition GetDefinition(WithdrawalEscortPlan plan)
        {
            switch (plan)
            {
                case WithdrawalEscortPlan.Minimal:
                    return new WithdrawalEscortPlanDefinition(
                        WithdrawalEscortPlan.Minimal,
                        MinimalEscortSize,
                        MinimalRelationshipChange,
                        WithdrawalEscortSafetyLevel.SevereRisk);

                case WithdrawalEscortPlan.Lean:
                    return new WithdrawalEscortPlanDefinition(
                        WithdrawalEscortPlan.Lean,
                        LeanEscortSize,
                        LeanRelationshipChange,
                        WithdrawalEscortSafetyLevel.Standard);

                case WithdrawalEscortPlan.Strong:
                    return new WithdrawalEscortPlanDefinition(
                        WithdrawalEscortPlan.Strong,
                        StrongEscortSize,
                        StrongRelationshipChange,
                        WithdrawalEscortSafetyLevel.High);

                default:
                    throw new ArgumentOutOfRangeException(nameof(plan));
            }
        }

        public static WithdrawalEscortAllocation Calculate(
            WithdrawalEscortPlan plan,
            IEnumerable<WithdrawalEscortTroopStack> troopStacks)
        {
            if (troopStacks == null)
            {
                throw new ArgumentNullException(nameof(troopStacks));
            }

            WithdrawalEscortPlanDefinition definition = GetDefinition(plan);
            var indexed = new List<IndexedTroopStack>();
            int originalTroopCount = 0;
            int originalIndex = 0;

            foreach (WithdrawalEscortTroopStack stack in troopStacks)
            {
                if (stack == null)
                {
                    throw new ArgumentException(
                        "Troop stacks cannot contain null entries.",
                        nameof(troopStacks));
                }

                originalTroopCount = checked(originalTroopCount + stack.Count);
                indexed.Add(new IndexedTroopStack(stack, originalIndex));
                originalIndex++;
            }

            indexed.Sort(CompareTroopPriority);

            int escortSlotsRemaining = Math.Min(
                definition.RequestedEscortSize,
                originalTroopCount);
            int actualEscortSize = escortSlotsRemaining;
            var allocations = new List<WithdrawalEscortStackAllocation>(indexed.Count);

            foreach (IndexedTroopStack entry in indexed)
            {
                WithdrawalEscortTroopStack stack = entry.Stack;
                int retainedCount = Math.Min(stack.Count, escortSlotsRemaining);
                int surplusCount = stack.Count - retainedCount;
                escortSlotsRemaining -= retainedCount;

                allocations.Add(new WithdrawalEscortStackAllocation(
                    stack.TroopId,
                    stack.Tier,
                    stack.Level,
                    stack.Count,
                    retainedCount,
                    surplusCount));
            }

            return new WithdrawalEscortAllocation(
                definition,
                originalTroopCount,
                actualEscortSize,
                allocations);
        }

        private static int CompareTroopPriority(IndexedTroopStack left, IndexedTroopStack right)
        {
            int tier = right.Stack.Tier.CompareTo(left.Stack.Tier);
            if (tier != 0)
            {
                return tier;
            }

            int level = right.Stack.Level.CompareTo(left.Stack.Level);
            if (level != 0)
            {
                return level;
            }

            int troopId = string.Compare(
                left.Stack.TroopId,
                right.Stack.TroopId,
                StringComparison.Ordinal);
            if (troopId != 0)
            {
                return troopId;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private sealed class IndexedTroopStack
        {
            public IndexedTroopStack(WithdrawalEscortTroopStack stack, int originalIndex)
            {
                Stack = stack;
                OriginalIndex = originalIndex;
            }

            public WithdrawalEscortTroopStack Stack { get; }

            public int OriginalIndex { get; }
        }
    }
}
