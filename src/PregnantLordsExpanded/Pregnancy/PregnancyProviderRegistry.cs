using System;
using System.Collections.Generic;

namespace PregnantLordsExpanded.Pregnancy
{
    public sealed class PregnancyProviderRegistry
    {
        private readonly List<IPregnancyDataProvider> _providers = new List<IPregnancyDataProvider>();

        public void Register(IPregnancyDataProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            for (int index = _providers.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_providers[index].Id, provider.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _providers.RemoveAt(index);
                }
            }

            _providers.Add(provider);
            _providers.Sort(CompareProviders);
        }

        public IList<IPregnancyDataProvider> GetProviders()
        {
            return _providers.ToArray();
        }

        private static int CompareProviders(IPregnancyDataProvider left, IPregnancyDataProvider right)
        {
            return right.Priority.CompareTo(left.Priority);
        }
    }
}

