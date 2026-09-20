namespace PregnantLordsExpanded.Withdrawal
{
    public enum WithdrawalMonthStage
    {
        None = 0,
        AdvanceWarning = 1,
        FormalPetition = 2,
        RenewedPetition = 3
    }

    public enum WithdrawalDecision
    {
        NoDecision = 0,
        Approve = 1,
        Deny = 2,
        ContinueVoluntarily = 3,
        ForcedDelay = 4
    }

    public enum WithdrawalResponsibility
    {
        None = 0,
        WithdrawalApproved = 1,
        CommanderOverride = 2,
        VoluntaryRefusal = 3,
        ForcedCircumstances = 4
    }

    public enum IndependentWithdrawalAuthorityMode
    {
        ClanLeader = 0,
        KingdomRuler = 1,
        Self = 2
    }

    public enum WithdrawalAuthorityKind
    {
        None = 0,
        Self = 1,
        ArmyLeader = 2,
        PartyLeader = 3,
        ClanLeader = 4,
        KingdomRuler = 5
    }

    public enum FamilyReactionRole
    {
        PregnantMother = 0,
        SpouseOrOtherParent = 1,
        MothersParent = 2,
        MothersAdultSibling = 3
    }

    public enum ProtectedRestState
    {
        Unknown = 0,
        ProtectedRest = 1,
        ProtectedDefense = 2,
        Campaigning = 3,
        Prisoner = 4,
        Unavailable = 5
    }

    public enum ProtectedRestTransitionKind
    {
        None = 0,
        ProtectedRestEstablished = 1,
        ReturnedToProtectedRest = 2,
        DefensiveMobilization = 3,
        PresumedVoluntaryDeparture = 4,
        ForcedRemoval = 5,
        UnresolvedDeparture = 6
    }

    public sealed class WithdrawalMonthResult
    {
        public WithdrawalMonthResult(int normalizedMonth, WithdrawalMonthStage stage)
        {
            NormalizedMonth = normalizedMonth;
            Stage = stage;
        }

        public int NormalizedMonth { get; }

        public WithdrawalMonthStage Stage { get; }

        public bool IsWarning => Stage == WithdrawalMonthStage.AdvanceWarning;

        public bool IsPetition => Stage == WithdrawalMonthStage.FormalPetition
            || Stage == WithdrawalMonthStage.RenewedPetition;
    }

    public sealed class WithdrawalAuthorityContext
    {
        public string MotherId { get; set; }

        public bool IsCampaigning { get; set; }

        public bool IsPrisoner { get; set; }

        public bool IsResting { get; set; }

        public bool IsArmyMember { get; set; }

        public string ArmyLeaderId { get; set; }

        public string PartyLeaderId { get; set; }

        public bool LeadsIndependentParty { get; set; }

        public string ClanLeaderId { get; set; }

        public string KingdomRulerId { get; set; }
    }

    public sealed class WithdrawalAuthorityResult
    {
        public WithdrawalAuthorityResult(
            WithdrawalAuthorityKind kind,
            string authorityId,
            string noDecisionReason)
        {
            Kind = kind;
            AuthorityId = authorityId ?? string.Empty;
            NoDecisionReason = noDecisionReason ?? string.Empty;
        }

        public WithdrawalAuthorityKind Kind { get; }

        public string AuthorityId { get; }

        public string NoDecisionReason { get; }

        public bool HasAuthority => Kind != WithdrawalAuthorityKind.None
            && !string.IsNullOrWhiteSpace(AuthorityId);
    }

    public sealed class FamilyReactionCandidate
    {
        public FamilyReactionCandidate(string heroId, FamilyReactionRole role)
        {
            HeroId = heroId ?? string.Empty;
            Role = role;
        }

        public string HeroId { get; }

        public FamilyReactionRole Role { get; }
    }

    public sealed class FamilyReaction
    {
        public FamilyReaction(string heroId, FamilyReactionRole role, int relationChange)
        {
            HeroId = heroId;
            Role = role;
            RelationChange = relationChange;
        }

        public string HeroId { get; }

        public FamilyReactionRole Role { get; }

        public int RelationChange { get; }
    }

    public sealed class AiWithdrawalDecisionInput
    {
        public int NormalizedMonth { get; set; }

        public bool IsSelfAuthority { get; set; }

        public int MercyLevel { get; set; }

        public int HonorLevel { get; set; }

        public int ValorLevel { get; set; }

        public int CalculatingLevel { get; set; }

        public int RelationWithMother { get; set; }

        public bool HasReplacement { get; set; }

        public bool HasNearbyFriendlyProtection { get; set; }

        public bool HasHighDynasticRisk { get; set; }

        public bool IsMilitaryEmergency { get; set; }
    }

    public sealed class AiWithdrawalDecisionResult
    {
        public AiWithdrawalDecisionResult(
            WithdrawalDecision decision,
            int approvalScore,
            string explanation)
        {
            Decision = decision;
            ApprovalScore = approvalScore;
            Explanation = explanation ?? string.Empty;
        }

        public WithdrawalDecision Decision { get; }

        public int ApprovalScore { get; }

        public string Explanation { get; }
    }

    public sealed class ProtectedRestTransitionInput
    {
        public ProtectedRestState PreviousState { get; set; }

        public ProtectedRestState ObservedState { get; set; }

        public string ProtectedSettlementId { get; set; }

        public string ObservedSettlementId { get; set; }

        public bool IsDefendingProtectedSettlement { get; set; }
    }

    public sealed class ProtectedRestTransitionResult
    {
        public ProtectedRestTransitionResult(
            ProtectedRestTransitionKind transition,
            ProtectedRestState nextState,
            string protectedSettlementId,
            WithdrawalResponsibility responsibility)
        {
            Transition = transition;
            NextState = nextState;
            ProtectedSettlementId = protectedSettlementId ?? string.Empty;
            Responsibility = responsibility;
        }

        public ProtectedRestTransitionKind Transition { get; }

        public ProtectedRestState NextState { get; }

        public string ProtectedSettlementId { get; }

        public WithdrawalResponsibility Responsibility { get; }

        public bool RequiresImmediatePetition =>
            Transition == ProtectedRestTransitionKind.PresumedVoluntaryDeparture;
    }
}
