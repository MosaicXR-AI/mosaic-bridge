using Mosaic.Bridge.Core.Knowledge;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public class KnowledgeHelpersTests
    {
        // Story 5.3 tests
        [Test]
        public void KnowledgeReference_Create_WithSource()
        {
            var reference = KnowledgeReference.Create("physics", "steel", "NIST CODATA 2022");
            Assert.AreEqual("physics/steel [NIST CODATA 2022]", reference);
        }

        [Test]
        public void KnowledgeReference_Create_WithoutSource()
        {
            var reference = KnowledgeReference.Create("rendering", "wood_oak");
            Assert.AreEqual("rendering/wood_oak", reference);
        }

        // Story 5.4 tests
        [Test]
        public void NoDataAvailable_WithDefaultValue()
        {
            var warning = KnowledgeWarnings.NoDataAvailable("physics", "mythril", "1000 kg/m3");
            Assert.That(warning, Does.Contain("No knowledge base data"));
            Assert.That(warning, Does.Contain("mythril"));
            Assert.That(warning, Does.Contain("1000 kg/m3"));
            Assert.That(warning, Does.Contain("verify this is appropriate"));
        }

        [Test]
        public void NoDataAvailable_WithoutDefaultValue()
        {
            var warning = KnowledgeWarnings.NoDataAvailable("rendering", "alien_skin");
            Assert.That(warning, Does.Contain("No knowledge base data"));
            Assert.That(warning, Does.Contain("alien_skin"));
            Assert.That(warning, Does.Not.Contain("default value"));
        }
    }
}
