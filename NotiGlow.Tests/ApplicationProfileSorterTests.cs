using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ApplicationProfileSorterTests
    {
        private static AppProfile Profile(string name) => new() { Name = name };

        [TestMethod]
        public void AlphabeticalSortSupportsBothDirections()
        {
            var profiles = new[] { Profile("Charlie"), Profile("alpha"), Profile("Bravo") };
            CollectionAssert.AreEqual(new[] { "alpha", "Bravo", "Charlie" }, ApplicationProfileSorter.Sort(profiles, ApplicationSortCriterion.Alphabetical, false).Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Charlie", "Bravo", "alpha" }, ApplicationProfileSorter.Sort(profiles, ApplicationSortCriterion.Alphabetical, true).Select(p => p.Name).ToArray());
        }

        [TestMethod]
        public void RecentlyAddedSortUsesProfileInsertionOrderInBothDirections()
        {
            var profiles = new[] { Profile("oldest"), Profile("middle"), Profile("newest") };
            CollectionAssert.AreEqual(new[] { "newest", "middle", "oldest" }, ApplicationProfileSorter.Sort(profiles, ApplicationSortCriterion.RecentlyAdded, true).Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "oldest", "middle", "newest" }, ApplicationProfileSorter.Sort(profiles, ApplicationSortCriterion.RecentlyAdded, false).Select(p => p.Name).ToArray());
        }
    }
}
