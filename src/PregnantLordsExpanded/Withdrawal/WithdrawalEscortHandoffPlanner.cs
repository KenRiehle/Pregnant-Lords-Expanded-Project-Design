using System;
using System.Collections.Generic;

namespace PregnantLordsExpanded.Withdrawal
{
    /// <summary>
    /// Bannerlord-independent capacity description for one party that can receive
    /// surplus troops from an approved withdrawal escort allocation.
    /// </summary>
    public sealed class WithdrawalEscortRecipientCapacity
    {
        public WithdrawalEscortRecipientCapacity(string recipientId, int availableCapacity)
        {
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                throw new ArgumentException("Recipient id is required.", nameof(recipientId));
            }

            if (availableCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(availableCapacity));
            }

            RecipientId = recipientId;
            AvailableCapacity = availableCapacity;
        }

        public string RecipientId { get; }

        public int AvailableCapacity { get; }
    }

    public sealed class WithdrawalEscortRecipientShare
    {
        public WithdrawalEscortRecipientShare(
            string recipientId,
            int availableCapacity,
            int assignedTroops)
        {
            RecipientId = recipientId ?? string.Empty;
            AvailableCapacity = availableCapacity;
            AssignedTroops = assignedTroops;
        }

        public string RecipientId { get; }

        public int AvailableCapacity { get; }

        public int AssignedTroops { get; }
    }

    public sealed class WithdrawalEscortHandoffAllocation
    {
        public WithdrawalEscortHandoffAllocation(
            int surplusTroopCount,
            int transferredTroopCount,
            IReadOnlyList<WithdrawalEscortRecipientShare> recipients)
        {
            if (surplusTroopCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(surplusTroopCount));
            }

            if (transferredTroopCount < 0 || transferredTroopCount > surplusTroopCount)
            {
                throw new ArgumentOutOfRangeException(nameof(transferredTroopCount));
            }

            SurplusTroopCount = surplusTroopCount;
            TransferredTroopCount = transferredTroopCount;
            RemainingWithMother = surplusTroopCount - transferredTroopCount;
            Recipients = recipients ?? throw new ArgumentNullException(nameof(recipients));

            int assigned = 0;
            foreach (WithdrawalEscortRecipientShare recipient in Recipients)
            {
                if (recipient != null)
                {
                    assigned = checked(assigned + recipient.AssignedTroops);
                }
            }

            AssignedTroopCount = assigned;
        }

        public int SurplusTroopCount { get; }

        public int TransferredTroopCount { get; }

        public int RemainingWithMother { get; }

        public int AssignedTroopCount { get; }

        public IReadOnlyList<WithdrawalEscortRecipientShare> Recipients { get; }

        public bool IsConserved =>
            SurplusTroopCount == TransferredTroopCount + RemainingWithMother
            && TransferredTroopCount == AssignedTroopCount;
    }

    /// <summary>
    /// Pure 2D-F-C handoff planner. Recipients with the most legal party capacity
    /// are filled first. Equal-capacity ties are resolved by ordinal recipient id,
    /// keeping allocation deterministic across save/reload.
    /// </summary>
    public static class WithdrawalEscortHandoffPlanner
    {
        public static WithdrawalEscortHandoffAllocation Calculate(
            int surplusTroopCount,
            IEnumerable<WithdrawalEscortRecipientCapacity> recipientCapacities)
        {
            if (surplusTroopCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(surplusTroopCount));
            }

            if (recipientCapacities == null)
            {
                throw new ArgumentNullException(nameof(recipientCapacities));
            }

            var capacities = new List<WithdrawalEscortRecipientCapacity>();
            foreach (WithdrawalEscortRecipientCapacity capacity in recipientCapacities)
            {
                if (capacity == null)
                {
                    throw new ArgumentException(
                        "Recipient capacities cannot contain null entries.",
                        nameof(recipientCapacities));
                }

                capacities.Add(capacity);
            }

            capacities.Sort(CompareRecipients);

            int remaining = surplusTroopCount;
            int transferred = 0;
            var shares = new List<WithdrawalEscortRecipientShare>(capacities.Count);

            foreach (WithdrawalEscortRecipientCapacity capacity in capacities)
            {
                int assigned = Math.Min(remaining, capacity.AvailableCapacity);
                remaining -= assigned;
                transferred = checked(transferred + assigned);

                shares.Add(new WithdrawalEscortRecipientShare(
                    capacity.RecipientId,
                    capacity.AvailableCapacity,
                    assigned));
            }

            return new WithdrawalEscortHandoffAllocation(
                surplusTroopCount,
                transferred,
                shares);
        }

        private static int CompareRecipients(
            WithdrawalEscortRecipientCapacity left,
            WithdrawalEscortRecipientCapacity right)
        {
            int capacity = right.AvailableCapacity.CompareTo(left.AvailableCapacity);
            if (capacity != 0)
            {
                return capacity;
            }

            return string.Compare(
                left.RecipientId,
                right.RecipientId,
                StringComparison.Ordinal);
        }
    }
}
