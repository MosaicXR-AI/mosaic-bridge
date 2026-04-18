using System;
using NUnit.Framework;
using Mosaic.Bridge.Runtime;

namespace Mosaic.Bridge.Tests.Runtime
{
    [TestFixture]
    [Category("Unit")]
    public class RuntimeDispatcherTests
    {
        [Test]
        public void QueueCount_InitiallyZero()
        {
            var logger = new RuntimeLogger { MinimumLevel = RuntimeLogger.LogLevel.Error };
            var dispatcher = new RuntimeDispatcher(logger);
            Assert.AreEqual(0, dispatcher.QueueCount);
        }
    }

    [TestFixture]
    [Category("Unit")]
    public class RuntimeNonceCacheTests
    {
        [Test]
        public void TryConsume_FirstTime_ReturnsTrue()
        {
            var cache = new RuntimeNonceCache();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.IsTrue(cache.TryConsume("nonce-1", now));
        }

        [Test]
        public void TryConsume_Replay_ReturnsFalse()
        {
            var cache = new RuntimeNonceCache();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cache.TryConsume("nonce-2", now);
            Assert.IsFalse(cache.TryConsume("nonce-2", now));
        }

        [Test]
        public void TryConsume_EmptyNonce_ReturnsFalse()
        {
            var cache = new RuntimeNonceCache();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.IsFalse(cache.TryConsume("", now));
            Assert.IsFalse(cache.TryConsume(null, now));
        }

        [Test]
        public void CurrentCount_ReflectsConsumedNonces()
        {
            var cache = new RuntimeNonceCache();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.AreEqual(0, cache.CurrentCount);
            cache.TryConsume("a", now);
            Assert.AreEqual(1, cache.CurrentCount);
        }
    }

    [TestFixture]
    [Category("Unit")]
    public class MosaicRuntimeConfigTests
    {
        [Test]
        public void DefaultConfig_HasPort8300()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<MosaicRuntimeConfig>();
            Assert.AreEqual(8300, config.Port);
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void DefaultConfig_HasEnabledCategories()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<MosaicRuntimeConfig>();
            Assert.IsNotNull(config.EnabledCategories);
            Assert.IsTrue(config.EnabledCategories.Length > 0);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [TestFixture]
    [Category("Unit")]
    public class RuntimeHandlerRequestTests
    {
        [Test]
        public void Request_PropertiesSettable()
        {
            var req = new RuntimeHandlerRequest
            {
                Method = "POST",
                RawUrl = "/execute",
                Body = System.Text.Encoding.UTF8.GetBytes("{}"),
                ClientId = "test"
            };
            Assert.AreEqual("POST", req.Method);
            Assert.AreEqual("/execute", req.RawUrl);
            Assert.IsNotNull(req.Body);
        }
    }
}
