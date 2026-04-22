using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Unit.Tools.Physics
{
    [TestFixture]
    [Category("Unit")]
    public class NBodyTests
    {
        private GameObject _createdGo;

        [TearDown]
        public void TearDown()
        {
            if (_createdGo != null)
            {
                Object.DestroyImmediate(_createdGo);
                _createdGo = null;
            }

            var genPath = "Assets/Generated/Physics/";
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullGenPath = Path.Combine(projectRoot, genPath);
            if (Directory.Exists(fullGenPath))
            {
                Directory.Delete(fullGenPath, true);
                var metaPath = fullGenPath.TrimEnd('/') + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                AssetDatabase.Refresh();
            }
        }

        private static PhysicsNBodyParams.Body MakeBody(float x, float y, float z, float mass, float[] velocity = null)
        {
            return new PhysicsNBodyParams.Body
            {
                Position = new[] { x, y, z },
                Mass     = mass,
                Velocity = velocity
            };
        }

        [Test]
        public void Create_TwoBodySystem_ReturnsValidScript()
        {
            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>
                {
                    MakeBody(0f, 0f, 0f, 1000f),
                    MakeBody(10f, 0f, 0f, 1f, new[] { 0f, 0f, 5f })
                },
                Name = "TwoBody"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(2, result.Data.BodyCount);
            Assert.AreEqual("leapfrog", result.Data.Integrator);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Script file does not exist at {fullPath}");

            _createdGo = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_TenBodySystem_ReturnsValidScript()
        {
            var bodies = new List<PhysicsNBodyParams.Body>();
            for (int i = 0; i < 10; i++)
            {
                bodies.Add(MakeBody(i * 2f, 0f, 0f, 1f + i));
            }

            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies     = bodies,
                Integrator = "rk4",
                Name       = "TenBody"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(10, result.Data.BodyCount);
            Assert.AreEqual("rk4", result.Data.Integrator);

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath));

            _createdGo = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_InvalidIntegrator_ReturnsError()
        {
            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>
                {
                    MakeBody(0f, 0f, 0f, 1f),
                    MakeBody(1f, 0f, 0f, 1f)
                },
                Integrator = "euler_supreme",
                Name       = "BadIntegrator"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_EmptyBodies_ReturnsError()
        {
            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>(),
                Name   = "EmptyBodies"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_NullBodies_ReturnsError()
        {
            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = null,
                Name   = "NullBodies"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_BodyWithZeroMass_ReturnsError()
        {
            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>
                {
                    MakeBody(0f, 0f, 0f, 1f),
                    MakeBody(1f, 0f, 0f, 0f)
                },
                Name = "ZeroMass"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_BodyWithNegativeMass_ReturnsError()
        {
            var result = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>
                {
                    MakeBody(0f, 0f, 0f, 1f),
                    MakeBody(1f, 0f, 0f, -5f)
                },
                Name = "NegativeMass"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_ThetaClampedToValidRange()
        {
            var highTheta = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>
                {
                    MakeBody(0f, 0f, 0f, 1f),
                    MakeBody(1f, 0f, 0f, 1f)
                },
                Theta = 50f,
                Name  = "HighTheta"
            });

            Assert.IsTrue(highTheta.Success, highTheta.Error);
            Assert.LessOrEqual(highTheta.Data.Theta, 2f);
            Assert.GreaterOrEqual(highTheta.Data.Theta, 0f);

            var go1 = Resources.EntityIdToObject(highTheta.Data.InstanceId) as GameObject;
            if (go1 != null) Object.DestroyImmediate(go1);

            var negTheta = PhysicsNBodyTool.Execute(new PhysicsNBodyParams
            {
                Bodies = new List<PhysicsNBodyParams.Body>
                {
                    MakeBody(0f, 0f, 0f, 1f),
                    MakeBody(1f, 0f, 0f, 1f)
                },
                Theta = -3f,
                Name  = "NegTheta"
            });

            Assert.IsTrue(negTheta.Success, negTheta.Error);
            Assert.GreaterOrEqual(negTheta.Data.Theta, 0f);
            Assert.LessOrEqual(negTheta.Data.Theta, 2f);

            _createdGo = Resources.EntityIdToObject(negTheta.Data.InstanceId) as GameObject;
        }
    }
}
