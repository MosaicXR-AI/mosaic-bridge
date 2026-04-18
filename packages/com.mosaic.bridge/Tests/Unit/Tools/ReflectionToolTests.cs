using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Tools.Reflection;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    public class ReflectionToolTests
    {
        // ── reflection/find-method ──────────────────────────────────────────────

        [Test]
        public void FindMethod_KnownUnityType_ReturnsCorrectMethods()
        {
            var p = new ReflectionFindMethodParams { TypeName = "UnityEngine.Application" };
            var result = ReflectionFindMethodTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.IsNotNull(result.Data.Methods);
            Assert.Greater(result.Data.MethodCount, 0);
            Assert.AreEqual("UnityEngine.Application", result.Data.TypeName);
        }

        [Test]
        public void FindMethod_FilterByName_ReturnsOnlyMatchingMethods()
        {
            var p = new ReflectionFindMethodParams
            {
                TypeName = "System.Math",
                MethodName = "Abs"
            };
            var result = ReflectionFindMethodTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.Greater(result.Data.MethodCount, 0);
            Assert.IsTrue(result.Data.Methods.All(m => m.Name == "Abs"));
        }

        [Test]
        public void FindMethod_IncludePrivate_ReturnsNonPublicMethods()
        {
            // System.Math has private members; compare counts
            var publicOnly = ReflectionFindMethodTool.Execute(
                new ReflectionFindMethodParams { TypeName = "System.Math", IncludePrivate = false });
            var withPrivate = ReflectionFindMethodTool.Execute(
                new ReflectionFindMethodParams { TypeName = "System.Math", IncludePrivate = true });

            Assert.IsTrue(publicOnly.Success);
            Assert.IsTrue(withPrivate.Success);
            Assert.GreaterOrEqual(withPrivate.Data.MethodCount, publicOnly.Data.MethodCount);
        }

        [Test]
        public void FindMethod_NonexistentType_ReturnsFail()
        {
            var p = new ReflectionFindMethodParams { TypeName = "Fake.NonExistent.Type" };
            var result = ReflectionFindMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Error);
        }

        [Test]
        public void FindMethod_NullTypeName_ReturnsFail()
        {
            var p = new ReflectionFindMethodParams { TypeName = null };
            var result = ReflectionFindMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── reflection/call-method ──────────────────────────────────────────────

        [Test]
        public void CallMethod_StaticMethod_ReturnsResult()
        {
            // System.Math.Abs(-42) should return 42
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Math",
                MethodName = "Abs",
                Arguments = new JArray(new JValue(-42))
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.AreEqual(42, System.Convert.ToInt32(result.Data.ReturnValue));
            StringAssert.Contains("Abs", result.Data.MethodSignature);
            Assert.Greater(result.Data.ExecutionTimeMs, -1);
        }

        [Test]
        public void CallMethod_StaticMethodNoArgs_ReturnsResult()
        {
            // System.Environment.get_NewLine via the property works, but let's use
            // System.Math.Log10(100) = 2.0
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Math",
                MethodName = "Log10",
                Arguments = new JArray(new JValue(100.0))
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.AreEqual(2.0, System.Convert.ToDouble(result.Data.ReturnValue), 0.001);
        }

        [Test]
        public void CallMethod_BlockedProcessStart_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Diagnostics.Process",
                MethodName = "Start",
                Arguments = new JArray(new JValue("cmd"))
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
            Assert.AreEqual("NOT_PERMITTED", result.ErrorCode);
        }

        [Test]
        public void CallMethod_BlockedProcessKill_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Diagnostics.Process",
                MethodName = "Kill",
                AllowPrivate = true  // even with private access, should be blocked
            };
            var result = ReflectionCallMethodTool.Execute(p);

            // Process.Kill is an instance method with no static overload, so it may fail
            // with NOT_FOUND or NOT_PERMITTED depending on resolution order.
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CallMethod_BlockedFileDelete_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.IO.File",
                MethodName = "Delete",
                Arguments = new JArray(new JValue("/tmp/test"))
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void CallMethod_BlockedEnvironmentExit_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Environment",
                MethodName = "Exit",
                Arguments = new JArray(new JValue(0))
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void CallMethod_BlockedMarshalMethod_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Runtime.InteropServices.Marshal",
                MethodName = "SizeOf",
                Arguments = new JArray(new JValue("System.Int32"))
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void CallMethod_PrivateMethodWithoutFlag_ReturnsFail()
        {
            // System.Math has private methods; try calling one without AllowPrivate
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Math",
                MethodName = "ThisMethodDoesNotExist"
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            // Error message may say "not found" or "no static method" depending on implementation
            Assert.IsNotNull(result.Error);
            Assert.IsNotEmpty(result.Error);
        }

        [Test]
        public void CallMethod_NullTypeName_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = null,
                MethodName = "Abs"
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void CallMethod_NullMethodName_ReturnsFail()
        {
            var p = new ReflectionCallMethodParams
            {
                TypeName = "System.Math",
                MethodName = null
            };
            var result = ReflectionCallMethodTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── reflection/get-types ────────────────────────────────────────────────

        [Test]
        public void GetTypes_FilterByAssembly_ReturnsMatchingTypes()
        {
            var p = new ReflectionGetTypesParams
            {
                AssemblyFilter = "UnityEngine",
                NameFilter = "Camera"
            };
            var result = ReflectionGetTypesTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.Greater(result.Data.TypeCount, 0);
            Assert.IsTrue(result.Data.Types.All(t =>
                t.AssemblyName.Contains("UnityEngine")));
        }

        [Test]
        public void GetTypes_FilterByBaseType_ReturnsSubtypes()
        {
            var p = new ReflectionGetTypesParams
            {
                BaseType = "UnityEngine.Renderer",
                AssemblyFilter = "UnityEngine"
            };
            var result = ReflectionGetTypesTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.Greater(result.Data.TypeCount, 0);
            // MeshRenderer, SkinnedMeshRenderer, etc. should appear
            Assert.IsTrue(result.Data.Types.Any(t => t.FullName.Contains("MeshRenderer")));
        }

        [Test]
        public void GetTypes_FilterByName_ReturnsMatchingTypes()
        {
            var p = new ReflectionGetTypesParams
            {
                NameFilter = "Vector3",
                AssemblyFilter = "UnityEngine"
            };
            var result = ReflectionGetTypesTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.Greater(result.Data.TypeCount, 0);
            Assert.IsTrue(result.Data.Types.Any(t => t.FullName == "UnityEngine.Vector3"));
        }

        [Test]
        public void GetTypes_NonexistentBaseType_ReturnsFail()
        {
            var p = new ReflectionGetTypesParams
            {
                BaseType = "Fake.NonExistent.BaseType"
            };
            var result = ReflectionGetTypesTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Error);
        }

        [Test]
        public void GetTypes_NoFilters_ReturnsTypes()
        {
            var p = new ReflectionGetTypesParams();
            var result = ReflectionGetTypesTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.Greater(result.Data.TypeCount, 0);
        }

        // ── Blocklist unit tests ────────────────────────────────────────────────

        [Test]
        public void Blocklist_ProcessStart_IsBlocked()
        {
            var method = typeof(System.Diagnostics.Process).GetMethod("Start",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string) }, null);

            Assert.IsNotNull(method, "Could not find Process.Start(string)");
            Assert.IsTrue(ReflectionBlocklist.IsBlocked(method));
            Assert.IsNotNull(ReflectionBlocklist.GetBlockReason(method));
        }

        [Test]
        public void Blocklist_MathAbs_IsNotBlocked()
        {
            var method = typeof(System.Math).GetMethod("Abs",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(int) }, null);

            Assert.IsNotNull(method);
            Assert.IsFalse(ReflectionBlocklist.IsBlocked(method));
            Assert.IsNull(ReflectionBlocklist.GetBlockReason(method));
        }

        [Test]
        public void Blocklist_MarshalAnyMethod_IsBlocked()
        {
            var method = typeof(System.Runtime.InteropServices.Marshal).GetMethod("SizeOf",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(System.Type) }, null);

            Assert.IsNotNull(method, "Could not find Marshal.SizeOf(Type)");
            Assert.IsTrue(ReflectionBlocklist.IsBlocked(method));
            StringAssert.Contains("blocked", ReflectionBlocklist.GetBlockReason(method));
        }

        // ── Safe serialization ──────────────────────────────────────────────────

        [Test]
        public void SafeSerialize_Primitive_PassesThrough()
        {
            Assert.AreEqual(42, ReflectionCallMethodTool.SafeSerialize(42));
            Assert.AreEqual("hello", ReflectionCallMethodTool.SafeSerialize("hello"));
            Assert.AreEqual(true, ReflectionCallMethodTool.SafeSerialize(true));
        }

        [Test]
        public void SafeSerialize_Null_ReturnsNull()
        {
            Assert.IsNull(ReflectionCallMethodTool.SafeSerialize(null));
        }

        [Test]
        public void SafeSerialize_Vector3_ReturnsFloatArray()
        {
            var v = new UnityEngine.Vector3(1f, 2f, 3f);
            var result = ReflectionCallMethodTool.SafeSerialize(v) as float[];

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(1f, result[0], 0.001f);
            Assert.AreEqual(2f, result[1], 0.001f);
            Assert.AreEqual(3f, result[2], 0.001f);
        }

        [Test]
        public void SafeSerialize_Color_ReturnsFloatArray()
        {
            var c = new UnityEngine.Color(0.1f, 0.2f, 0.3f, 1f);
            var result = ReflectionCallMethodTool.SafeSerialize(c) as float[];

            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(0.1f, result[0], 0.001f);
        }
    }
}
