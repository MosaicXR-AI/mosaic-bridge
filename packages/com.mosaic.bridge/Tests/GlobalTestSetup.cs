using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Global test setup that runs once before/after all tests in the assembly.
///
/// Suppresses the known Unity Editor bug:
///   "Assertion failed on expression: 'targetScene != nullptr'"
/// which fires in EditorApplication.Internal_CallUpdateFunctions when
/// EditMode tests create/destroy GameObjects. This is a Unity internal
/// assertion, not a Mosaic Bridge issue.
///
/// Note: ignoreFailingMessages = true means Unity log errors won't fail tests.
/// Our tools return errors via ToolResult.Fail, which tests assert against directly —
/// so test correctness is not affected.
/// </summary>
[SetUpFixture]
public class GlobalTestSetup
{
    [OneTimeSetUp]
    public void RunBeforeAllTests()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        LogAssert.ignoreFailingMessages = false;
    }
}
