using System;
using System.Collections.Generic;
using System.Linq;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public enum ApplicationSortCriterion
    {
        Alphabetical,
        RecentlyAdded
    }

    public static class ApplicationProfileSorter
    {
        public static IReadOnlyList<AppProfile> Sort(IEnumerable<AppProfile> profiles, ApplicationSortCriterion criterion, bool descending)
        {
            var indexed = profiles.Select((profile, index) => (profile, index));
            return criterion switch
            {
                ApplicationSortCriterion.Alphabetical => (descending
                    ? indexed.OrderByDescending(x => x.profile.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.index)
                    : indexed.OrderBy(x => x.profile.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.index))
                    .Select(x => x.profile).ToList(),
                _ => (descending
                    ? indexed.OrderByDescending(x => x.index)
                    : indexed.OrderBy(x => x.index))
                    .Select(x => x.profile).ToList()
            };
        }
    }
}
