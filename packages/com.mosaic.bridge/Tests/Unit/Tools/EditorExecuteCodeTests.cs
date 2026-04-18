using NUnit.Framework;
using Mosaic.Bridge.Tools.EditorOps;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    public class EditorExecuteCodeTests
    {
        // ── Expression parsing ──────────────────────────────────────────────────

        [Test]
        public void ParseExpression_PropertyAccess_ParsesCorrectly()
        {
            var parsed = EditorExecuteCodeTool.ParseExpression("UnityEngine.Application.dataPath");

            Assert.IsNull(parsed.Error);
            Assert.AreEqual("UnityEngine.Application", parsed.FullTypeName);
            Assert.AreEqual("dataPath", parsed.MemberName);
            Assert.IsFalse(parsed.IsMethodCall);
        }

        [Test]
        public void ParseExpression_MethodNoArgs_ParsesCorrectly()
        {
            var parsed = EditorExecuteCodeTool.ParseExpression("UnityEditor.AssetDatabase.Refresh()");

            Assert.IsNull(parsed.Error);
            Assert.AreEqual("UnityEditor.AssetDatabase", parsed.FullTypeName);
            Assert.AreEqual("Refresh", parsed.MemberName);
            Assert.IsTrue(parsed.IsMethodCall);
            Assert.AreEqual(0, parsed.Arguments.Length);
        }

        [Test]
        public void ParseExpression_MethodWithArgs_ParsesCorrectly()
        {
            var parsed = EditorExecuteCodeTool.ParseExpression("UnityEngine.Screen.SetResolution(1920, 1080, false)");

            Assert.IsNull(parsed.Error);
            Assert.AreEqual("UnityEngine.Screen", parsed.FullTypeName);
            Assert.AreEqual("SetResolution", parsed.MemberName);
            Assert.IsTrue(parsed.IsMethodCall);
            Assert.AreEqual(3, parsed.Arguments.Length);
            Assert.AreEqual("1920", parsed.Arguments[0]);
            Assert.AreEqual("1080", parsed.Arguments[1]);
            Assert.AreEqual("false", parsed.Arguments[2]);
        }

        [Test]
        public void ParseExpression_NoMember_ReturnsError()
        {
            var parsed = EditorExecuteCodeTool.ParseExpression("JustATypeName");
            Assert.IsNotNull(parsed.Error);
            StringAssert.Contains("Cannot parse", parsed.Error);
        }

        [Test]
        public void ParseExpression_StringArgWithComma_SplitsCorrectly()
        {
            var args = EditorExecuteCodeTool.SplitArguments("\"hello, world\", 42");
            Assert.AreEqual(2, args.Length);
            Assert.AreEqual("\"hello, world\"", args[0]);
            Assert.AreEqual("42", args[1]);
        }

        // ── Security blocklist ──────────────────────────────────────────────────

        [Test]
        public void CheckSecurity_BlockedType_ReturnsError()
        {
            string err = EditorExecuteCodeTool.CheckSecurity("System.Diagnostics.Process", "Start");
            Assert.IsNotNull(err);
            StringAssert.Contains("blocked", err);
        }

        [Test]
        public void CheckSecurity_BlockedMember_ReturnsError()
        {
            string err = EditorExecuteCodeTool.CheckSecurity("System.IO.File", "Delete");
            Assert.IsNotNull(err);
            StringAssert.Contains("blocked", err);
        }

        [Test]
        public void CheckSecurity_AllowedMember_ReturnsNull()
        {
            string err = EditorExecuteCodeTool.CheckSecurity("UnityEngine.Application", "dataPath");
            Assert.IsNull(err);
        }

        [Test]
        public void Execute_BlockedType_ReturnsFail()
        {
            var p = new EditorExecuteCodeParams { Code = "System.Diagnostics.Process.Start(\"cmd\")" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void Execute_FileDelete_Blocked()
        {
            var p = new EditorExecuteCodeParams { Code = "System.IO.File.Delete(\"/tmp/test\")" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void Execute_DirectoryDelete_Blocked()
        {
            var p = new EditorExecuteCodeParams { Code = "System.IO.Directory.Delete(\"/tmp/test\")" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void Execute_AssemblyLoad_Blocked()
        {
            var p = new EditorExecuteCodeParams { Code = "System.Reflection.Assembly.Load(\"evil\")" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        [Test]
        public void Execute_EnvironmentExit_Blocked()
        {
            var p = new EditorExecuteCodeParams { Code = "System.Environment.Exit(0)" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("blocked", result.Error);
        }

        // ── Argument conversion ─────────────────────────────────────────────────

        [Test]
        public void ConvertArgument_QuotedString_ReturnsUnquoted()
        {
            var result = EditorExecuteCodeTool.ConvertArgument("\"hello world\"", typeof(string));
            Assert.AreEqual("hello world", result);
        }

        [Test]
        public void ConvertArgument_Integer_Converts()
        {
            var result = EditorExecuteCodeTool.ConvertArgument("42", typeof(int));
            Assert.AreEqual(42, result);
        }

        [Test]
        public void ConvertArgument_Float_Converts()
        {
            var result = EditorExecuteCodeTool.ConvertArgument("3.14", typeof(float));
            Assert.AreEqual(3.14f, (float)result, 0.001f);
        }

        [Test]
        public void ConvertArgument_BoolTrue_Converts()
        {
            var result = EditorExecuteCodeTool.ConvertArgument("true", typeof(bool));
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ConvertArgument_BoolFalse_Converts()
        {
            var result = EditorExecuteCodeTool.ConvertArgument("false", typeof(bool));
            Assert.AreEqual(false, result);
        }

        // ── Type resolution ─────────────────────────────────────────────────────

        [Test]
        public void ResolveType_UnityEngineApplication_ReturnsType()
        {
            var t = EditorExecuteCodeTool.ResolveType("UnityEngine.Application");
            Assert.IsNotNull(t);
            Assert.AreEqual("Application", t.Name);
        }

        [Test]
        public void ResolveType_UnityEditorAssetDatabase_ReturnsType()
        {
            var t = EditorExecuteCodeTool.ResolveType("UnityEditor.AssetDatabase");
            Assert.IsNotNull(t);
            Assert.AreEqual("AssetDatabase", t.Name);
        }

        [Test]
        public void ResolveType_Nonexistent_ReturnsNull()
        {
            var t = EditorExecuteCodeTool.ResolveType("Fake.Namespace.DoesNotExist");
            Assert.IsNull(t);
        }

        // ── Integration: actual execution ───────────────────────────────────────

        [Test]
        public void Execute_ApplicationDataPath_ReturnsValidPath()
        {
            var p = new EditorExecuteCodeParams { Code = "UnityEngine.Application.dataPath" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.AreEqual("UnityEngine.Application.dataPath", result.Data.Expression);
            Assert.AreEqual("System.String", result.Data.ResultType);
            Assert.IsNotNull(result.Data.ResultValue);
            StringAssert.Contains("Assets", result.Data.ResultValue.ToString());
        }

        [Test]
        public void Execute_ApplicationPlatform_ReturnsEnum()
        {
            var p = new EditorExecuteCodeParams { Code = "UnityEngine.Application.platform" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.IsNotNull(result.Data.ResultValue);
        }

        [Test]
        public void Execute_NonexistentType_ReturnsFail()
        {
            var p = new EditorExecuteCodeParams { Code = "NonExistent.Type.Property" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Error);
        }

        [Test]
        public void Execute_NonexistentMember_ReturnsFail()
        {
            var p = new EditorExecuteCodeParams { Code = "UnityEngine.Application.nonExistentProp" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            // Error message says "No static property or field '...' found on type '...'"
            StringAssert.Contains("found", result.Error.ToLowerInvariant());
        }

        [Test]
        public void Execute_UnparsableExpression_ReturnsFail()
        {
            var p = new EditorExecuteCodeParams { Code = "not_a_valid_expression" };
            var result = EditorExecuteCodeTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Cannot parse", result.Error);
        }
    }
}
