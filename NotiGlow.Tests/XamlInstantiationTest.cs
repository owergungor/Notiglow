using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class XamlInstantiationTest
    {
        [TestMethod]
        public void TestReleaseVersionAssertions()
        {
            var version = ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            Assert.AreEqual("4.2", version);
            ReleaseVersionAssertions.AssertInstallerVersionMetadata(version);
            ReleaseVersionAssertions.AssertPackageManifestVersionMetadata(version);
            ReleaseVersionAssertions.AssertAdvancedViewUsesCurrentVersion();
        }
    }
}
