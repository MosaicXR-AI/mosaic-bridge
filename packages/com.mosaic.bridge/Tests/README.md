# Mosaic.Bridge.Tests

Test suite for Mosaic Bridge. Single test assembly (`Mosaic.Bridge.Tests`)
per the layered asmdef strategy.

## Test Categories

| Folder | Type | Purpose |
|---|---|---|
| `Unit/` | NUnit `[Test]` | Framework-level unit tests with mocked Unity APIs where possible |
| `Integration/` | Unity TestRunner `[UnityTest]` (EditMode only) | Integration tests requiring real Unity Editor APIs |
| `Regression/` | Unity TestRunner `[Test]` with `[Category("Regression")]` | End-to-end regression fixtures verifying tool behavior across releases |
| `Fixtures/` | JSON test fixtures | Input/expected-output data for regression and integration tests |
| Category-specific folders (e.g. `Authentication/`, `Dispatcher/`, `Physics/`) | Mixed | Tests grouped by bridge subsystem |

## Test Conventions

- **Test method naming:** `MethodUnderTest_Condition_ExpectedResult`
  - Example: `Create_NameIsNullOrEmpty_ReturnsFailWithInvalidParam`
- **Test class naming:** `<ClassUnderTest>Tests` (e.g., `HmacAuthenticatorTests`)
- **EditMode only:** Mosaic Bridge is an Editor-only plugin. PlayMode tests are NOT applicable.
- **HttpListener tests:** Use `[Test]` (not `[UnityTest]`) with sync HTTP clients on background threads — `[UnityTest]` is coroutine-based and cannot await Tasks cleanly.

## Running Tests

1. Open the Unity project that includes this package.
2. Open `Window → General → Test Runner`.
3. Select the **EditMode** tab.
4. Run all, run by category, or run individual tests.

## Coverage Targets

- Every tool method: at least one happy-path unit test, one error-path unit test, and one integration test where applicable.
- Bridge infrastructure (HttpListener, queue, dispatch, discovery, envelope, parameter validation, authentication): maintain at least 80% line coverage.
