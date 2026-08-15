using Mosaic.Bridge.Tools.GameObjects;
using NUnit.Framework;
using UnityEngine;

namespace Mosaic.Bridge.Tests.GameObjects
{
    /// <summary>
    /// There was no way to rename a GameObject at all. `create`, `delete`, `duplicate`,
    /// `reparent`, `set_active` and `set_transform` all existed; rename did not, and it could not
    /// be reached through `component/set_property` either — GameObject does not derive from
    /// Component, and Transform has no `name` property.
    ///
    /// That gap blocks one of the most common instructions a course gives. Walking a real Unity
    /// course through the Editor, "Rename it to Camera" was the first step that could not be
    /// performed at all.
    /// </summary>
    [TestFixture]
    public class GameObjectRenameToolTests
    {
        private GameObject _go;
        private GameObject _other;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("MosaicRenameProbe");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_other != null) Object.DestroyImmediate(_other);
        }

        private static Mosaic.Bridge.Contracts.Envelopes.ToolResult<GameObjectRenameResult>
            Rename(string from, string to)
        {
            return GameObjectRenameTool.Rename(
                new GameObjectRenameParams { Name = from, NewName = to });
        }

        [Test]
        public void Renames_AndReportsBothNames()
        {
            var r = Rename("MosaicRenameProbe", "MosaicRenamed");
            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual("MosaicRenameProbe", r.Data.PreviousName);
            Assert.AreEqual("MosaicRenamed", r.Data.Name);
            Assert.AreEqual("MosaicRenamed", _go.name);
        }

        [Test]
        public void RenamesInactiveObjects()
        {
            // A scene search that skips inactive objects would refuse to rename something that is
            // plainly there in the Hierarchy.
            _go.SetActive(false);
            var r = Rename("MosaicRenameProbe", "MosaicRenamedWhileInactive");
            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual("MosaicRenamedWhileInactive", _go.name);
        }

        [Test]
        public void MissingObject_FailsRatherThanCreatingOne()
        {
            var r = Rename("MosaicNoSuchObject_zzz", "Anything");
            Assert.IsFalse(r.Success);
            StringAssert.Contains("not found", r.Error);
        }

        [Test]
        public void DuplicateName_IsRefused()
        {
            // Every other tool here addresses objects BY NAME, so two sharing one makes the next
            // lookup pick an arbitrary winner — and a step that renames onto an existing name
            // would silently start operating on the wrong object.
            _other = new GameObject("MosaicRenameTaken");
            var r = Rename("MosaicRenameProbe", "MosaicRenameTaken");
            Assert.IsFalse(r.Success);
            StringAssert.Contains("already exists", r.Error);
            Assert.AreEqual("MosaicRenameProbe", _go.name, "the original must be untouched");
        }

        [Test]
        public void EmptyNewName_IsRefused()
        {
            var r = Rename("MosaicRenameProbe", "   ");
            Assert.IsFalse(r.Success);
            StringAssert.Contains("must not be empty", r.Error);
        }

        [Test]
        public void RenamingToItsOwnName_Succeeds()
        {
            // Idempotent: a course step re-run must not fail merely because it already happened.
            var r = Rename("MosaicRenameProbe", "MosaicRenameProbe");
            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual("MosaicRenameProbe", _go.name);
        }
    }
}
