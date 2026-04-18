using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.SceneViewCamera;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    [Category("Unit")]
    public class SceneViewCameraToolTests
    {
        [Test]
        public void SceneViewInfo_ReturnsData_WhenSceneViewOpen()
        {
            // SceneView.lastActiveSceneView may be null in batch mode
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Assert.Ignore("No active SceneView — test requires an open Scene window");
                return;
            }

            var result = SceneViewInfoTool.Info(new SceneViewInfoParams());

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data.Position);
            Assert.AreEqual(3, result.Data.Position.Length);
            Assert.IsNotNull(result.Data.Rotation);
            Assert.IsNotNull(result.Data.Pivot);
        }

        [Test]
        public void SceneViewSetCamera_UpdatesCamera_WhenSceneViewOpen()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Assert.Ignore("No active SceneView — test requires an open Scene window");
                return;
            }

            var result = SceneViewSetCameraTool.SetCamera(new SceneViewSetCameraParams
            {
                Pivot = new[] { 1f, 2f, 3f },
                Size = 15f
            });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(15f, result.Data.Size, 0.01f);
        }
    }
}
