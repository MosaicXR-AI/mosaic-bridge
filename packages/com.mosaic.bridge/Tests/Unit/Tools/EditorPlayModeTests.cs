using NUnit.Framework;
using Mosaic.Bridge.Tools.PlayMode;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    public class EditorPlayModeTests
    {
        // ── Invalid action tests ─────────────────────────────────────────────

        [Test]
        public void Execute_InvalidAction_ReturnsFail()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "restart" });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Invalid action", result.Error);
            StringAssert.Contains("play, pause, stop, step", result.Error);
        }

        [Test]
        public void Execute_EmptyAction_ReturnsFail()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "" });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Invalid action", result.Error);
        }

        [Test]
        public void Execute_NullAction_ReturnsFail()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = null });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Invalid action", result.Error);
        }

        // ── Valid action tests (EditMode — play mode does not actually start) ─

        [Test]
        public void Execute_Stop_ReturnsOkWithStoppedState()
        {
            // "stop" sets isPlaying = false; in EditMode it's already false,
            // so this is safe and idempotent.
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "stop" });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("stop", result.Data.RequestedAction);
            Assert.AreEqual("Stopped", result.Data.State);
            Assert.IsFalse(result.Data.IsPlaying);
        }

        [Test]
        public void Execute_ActionIsCaseInsensitive()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "STOP" });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("stop", result.Data.RequestedAction);
        }

        [Test]
        public void Execute_ActionTrimsWhitespace()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "  stop  " });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("stop", result.Data.RequestedAction);
        }

        [Test]
        public void Execute_Play_ReturnsOk()
        {
            // NOTE: In EditMode test runner, setting isPlaying = true triggers
            // an async state change that may not complete during the test.
            // We verify the tool returns Ok and reports the requested action.
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "play" });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("play", result.Data.RequestedAction);
        }

        [Test]
        public void Execute_Pause_ReturnsOk()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "pause" });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("pause", result.Data.RequestedAction);
        }

        [Test]
        public void Execute_Step_ReturnsOk()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "step" });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("step", result.Data.RequestedAction);
        }

        // ── Result shape tests ───────────────────────────────────────────────

        [Test]
        public void Execute_Result_ContainsAllFields()
        {
            var result = EditorPlayModeTool.Execute(new EditorPlayModeParams { Action = "stop" });

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data.RequestedAction);
            Assert.IsNotNull(result.Data.State);
            // IsPlaying and IsPaused are booleans — always present
        }
    }
}
