using System;
using System.Collections.Generic;

namespace PregnantLordsExpanded.Withdrawal
{
    public static class FamilyReactionCalculator
    {
        public static IReadOnlyList<FamilyReaction> Calculate(
            string responsibleHeroId,
            IEnumerable<FamilyReactionCandidate> candidates,
            FamilyReactionSettings settings)
        {
            if (string.IsNullOrWhiteSpace(responsibleHeroId))
            {
                throw new ArgumentException(
                    "The responsible hero must have a stable identifier.",
                    nameof(responsibleHeroId));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var reactions = new List<FamilyReaction>();
            var indexByHeroId = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (FamilyReactionCandidate candidate in candidates)
            {
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.HeroId)
                    || string.Equals(
                        candidate.HeroId,
                        responsibleHeroId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int penalty = settings.GetPenalty(candidate.Role);
                if (penalty == 0)
                {
                    continue;
                }

                int existingIndex;
                if (!indexByHeroId.TryGetValue(candidate.HeroId, out existingIndex))
                {
                    indexByHeroId.Add(candidate.HeroId, reactions.Count);
                    reactions.Add(new FamilyReaction(
                        candidate.HeroId,
                        candidate.Role,
                        penalty));
                    continue;
                }

                FamilyReaction existing = reactions[existingIndex];
                if (penalty < existing.RelationChange)
                {
                    reactions[existingIndex] = new FamilyReaction(
                        candidate.HeroId,
                        candidate.Role,
                        penalty);
                }
            }

            return reactions;
        }
    }
}
