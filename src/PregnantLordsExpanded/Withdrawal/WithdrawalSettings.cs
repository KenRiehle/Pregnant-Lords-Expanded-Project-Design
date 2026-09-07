using System;

namespace PregnantLordsExpanded.Withdrawal
{
    public sealed class WithdrawalSettings
    {
        public WithdrawalSettings(
            int warningMonth,
            int formalPetitionMonth,
            IndependentWithdrawalAuthorityMode independentAuthorityMode)
        {
            if (warningMonth < 1 || warningMonth > 8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warningMonth),
                    "The warning month must be between 1 and 8.");
            }

            if (formalPetitionMonth < 2 || formalPetitionMonth > 9)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(formalPetitionMonth),
                    "The formal petition month must be between 2 and 9.");
            }

            if (formalPetitionMonth <= warningMonth)
            {
                throw new ArgumentException(
                    "The formal petition month must occur after the warning month.",
                    nameof(formalPetitionMonth));
            }

            WarningMonth = warningMonth;
            FormalPetitionMonth = formalPetitionMonth;
            IndependentAuthorityMode = independentAuthorityMode;
        }

        public int WarningMonth { get; }

        public int FormalPetitionMonth { get; }

        public IndependentWithdrawalAuthorityMode IndependentAuthorityMode { get; }

        public static WithdrawalSettings Default => new WithdrawalSettings(
            3,
            4,
            IndependentWithdrawalAuthorityMode.ClanLeader);
    }

    public sealed class FamilyReactionSettings
    {
        public FamilyReactionSettings(
            int pregnantMotherPenalty,
            int spouseOrOtherParentPenalty,
            int mothersParentPenalty,
            int mothersAdultSiblingPenalty)
        {
            PregnantMotherPenalty = ValidatePenalty(
                pregnantMotherPenalty,
                nameof(pregnantMotherPenalty));
            SpouseOrOtherParentPenalty = ValidatePenalty(
                spouseOrOtherParentPenalty,
                nameof(spouseOrOtherParentPenalty));
            MothersParentPenalty = ValidatePenalty(
                mothersParentPenalty,
                nameof(mothersParentPenalty));
            MothersAdultSiblingPenalty = ValidatePenalty(
                mothersAdultSiblingPenalty,
                nameof(mothersAdultSiblingPenalty));
        }

        public int PregnantMotherPenalty { get; }

        public int SpouseOrOtherParentPenalty { get; }

        public int MothersParentPenalty { get; }

        public int MothersAdultSiblingPenalty { get; }

        public static FamilyReactionSettings Default => new FamilyReactionSettings(
            -50,
            -50,
            -10,
            -5);

        public int GetPenalty(FamilyReactionRole role)
        {
            switch (role)
            {
                case FamilyReactionRole.PregnantMother:
                    return PregnantMotherPenalty;
                case FamilyReactionRole.SpouseOrOtherParent:
                    return SpouseOrOtherParentPenalty;
                case FamilyReactionRole.MothersParent:
                    return MothersParentPenalty;
                case FamilyReactionRole.MothersAdultSibling:
                    return MothersAdultSiblingPenalty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        private static int ValidatePenalty(int value, string parameterName)
        {
            if (value < -100 || value > 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "A family relation penalty must be between -100 and 0.");
            }

            return value;
        }
    }
}
