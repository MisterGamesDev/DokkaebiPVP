# Dokkaebi Testing Guide

## 🧪 **Comprehensive Test Suite Overview**

Dokkaebi employs a robust testing strategy with **46 comprehensive tests** covering all core systems. This guide explains our testing approach, how to run tests, and how to add new ones.

**Current Status**: 40/46 tests passing (87% pass rate)

---

## 📊 **Test Suite Breakdown**

### **Test Distribution**
```
Total Tests: 46
├── EditMode Tests: 28 (61%)
├── PlayMode Tests: 18 (39%)
└── Integration Tests: 12 (26%)

Pass Rate: 40/46 (87%)
├── Passing: 40 tests ✅
├── Failing: 6 tests (AuraManager: 4, PlayFab: 2)
└── Target: 46/46 (100%)
```

### **System Coverage**
| System | Tests | Status | Coverage |
|--------|-------|--------|----------|
| Core Turn System | 8 | ✅ All Pass | 100% |
| Grid & Movement | 6 | ✅ All Pass | 100% |
| Unit Management | 7 | ✅ All Pass | 100% |
| Ability System | 5 | ✅ All Pass | 100% |
| Status Effects | 4 | ✅ All Pass | 100% |
| Zone System | 3 | ✅ All Pass | 100% |
| **AuraManager** | **8** | **❌ 4 Failing** | **50%** |
| **PlayFab Integration** | **6** | **❌ 2 Failing** | **67%** |
| UI Integration | 4 | ✅ All Pass | 100% |
| Pathfinding | 3 | ✅ All Pass | 100% |

---

## 🚀 **Running Tests**

### **Unity Test Runner**
1. Open Unity Editor
2. Navigate to **Window** → **General** → **Test Runner**
3. Select test category:
   - **EditMode**: Logic and unit tests
   - **PlayMode**: Scene-based integration tests

### **Run All Tests**
```csharp
// Command line (for CI/CD)
Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform EditMode
Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform PlayMode
```

### **Run Specific Test Categories**
```csharp
// In Test Runner, filter by:
[Category("AuraManager")]     // AuraManager tests only
[Category("PlayFab")]         // PlayFab integration tests
[Category("Integration")]     // Cross-system integration tests
[Category("Core")]           // Core system tests
```

---

## 🏗️ **Test Architecture**

### **EditMode Test Pattern**
```csharp
[TestFixture]
public class SystemNameTests
{
    private SystemName systemUnderTest;
    
    [SetUp]
    public void Setup()
    {
        // Initialize test environment
        // Handle singleton dependencies using reflection
        systemUnderTest = new SystemName();
    }
    
    [Test]
    public void TestMethod_Condition_ExpectedResult()
    {
        // Arrange
        var input = new TestInput();
        
        // Act
        var result = systemUnderTest.Method(input);
        
        // Assert
        Assert.AreEqual(expectedValue, result);
    }
    
    [TearDown]
    public void TearDown()
    {
        // Clean up test environment
        // Reset singleton states if needed
    }
}
```

### **PlayMode Test Pattern**
```csharp
[UnityTest]
public IEnumerator TestGameplayFeature_Condition_ExpectedResult()
{
    // Arrange - Setup scene and game objects
    var testScene = SceneManager.CreateScene("TestScene");
    SceneManager.SetActiveScene(testScene);
    
    // Act - Perform gameplay actions
    yield return new WaitForSeconds(0.1f); // Allow frame updates
    
    // Assert - Verify game state
    Assert.IsTrue(condition);
    
    // Cleanup
    SceneManager.UnloadSceneAsync(testScene);
}
```

---

## 🔧 **Singleton Handling in Tests**

### **Problem**: EditMode tests struggle with Unity singletons
### **Solution**: Reflection-based initialization

```csharp
[SetUp]
public void Setup()
{
    // Reset singleton instance using reflection
    var instanceField = typeof(SingletonManager)
        .GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
    instanceField?.SetValue(null, null);
    
    // Create test instance
    var testObject = new GameObject("TestSingleton");
    var singleton = testObject.AddComponent<SingletonManager>();
    
    // Force initialization
    instanceField?.SetValue(null, singleton);
}
```

### **Alternative**: Mock Dependencies
```csharp
// Create mock implementations for testing
public class MockAuraManager : IAuraManager
{
    public int CurrentAura { get; set; } = 100;
    public int MaxAura { get; set; } = 100;
    
    public bool TrySpendAura(int cost) => CurrentAura >= cost;
    public void AddAura(int amount) => CurrentAura += amount;
}
```

---

## 📝 **Adding New Tests**

### **1. Create Test File**
```csharp
// Location: Assets/Scripts/Tests/
// Naming: [SystemName]Tests.cs
// Assembly: Reference Dokkaebi.Testing.asmdef
```

### **2. Test Structure**
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Dokkaebi.Core;

[TestFixture]
[Category("YourSystem")]
public class YourSystemTests
{
    [Test]
    public void YourMethod_ValidInput_ReturnsExpectedResult()
    {
        // Your test implementation
    }
}
```

### **3. Assembly Definition**
Ensure your test references:
```json
{
    "name": "Dokkaebi.Testing",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Dokkaebi.Core",
        "Dokkaebi.Interfaces"
    ]
}
```

---

## 🐛 **Current Known Issues**

### **AuraManager Tests (4 failing)**
**Issues:**
- Aura values stuck at 0 (initialization bug)
- Events not firing (`AuraChanged`, `MaxAuraChanged`)
- Anti-cheat validation conflicts

**Solution Plan (Phase 2)**:
1. Debug aura calculation logic
2. Fix event firing mechanisms  
3. Resolve server validation conflicts

### **PlayFab Integration Tests (2 failing)**
**Issues:**
- Server functions not deployed
- Network connectivity timeouts

**Solution Plan (Phase 3)**:
1. Deploy CloudScript functions to PlayFab
2. Configure Title ID and authentication
3. Test server connectivity

---

## 🎯 **Test Quality Standards**

### **Good Test Characteristics**
- ✅ **Isolated**: Each test is independent
- ✅ **Fast**: EditMode tests run in <1 second
- ✅ **Reliable**: Consistent results across runs
- ✅ **Clear**: Descriptive names and assertions
- ✅ **Maintainable**: Easy to update when code changes

### **Test Naming Convention**
```csharp
[Test]
public void MethodName_Scenario_ExpectedOutcome()
{
    // Examples:
    // CalculateDamage_ValidInput_ReturnsCorrectValue()
    // TrySpendAura_InsufficientAura_ReturnsFalse()
    // ProcessTurn_MultipleUnits_ResolvesInPriorityOrder()
}
```

### **Assertion Best Practices**
```csharp
// ✅ Good - Specific and descriptive
Assert.AreEqual(100, player.CurrentAura, "Player should start with 100 aura");

// ❌ Bad - Vague and unhelpful
Assert.IsTrue(result);

// ✅ Good - Test one thing at a time
Assert.AreEqual(expectedHealth, unit.CurrentHealth);
Assert.AreEqual(expectedPosition, unit.Position);

// ❌ Bad - Multiple unrelated assertions
Assert.IsTrue(unit.IsAlive && unit.Position == expectedPos && unit.Health > 0);
```

---

## 📈 **Performance Benchmarks**

### **Test Execution Times**
```
EditMode Tests: ~15 seconds (average)
├── Core Systems: 8 tests in 3.2s
├── Grid & Movement: 6 tests in 2.1s
├── Unit Management: 7 tests in 2.8s
└── Other Systems: 17 tests in 6.9s

PlayMode Tests: ~45 seconds (average)
├── Integration Tests: 12 tests in 28s
├── UI Tests: 4 tests in 12s
└── Pathfinding: 3 tests in 5s

Total Suite: ~60 seconds for all 46 tests
```

### **CI/CD Integration**
```bash
# Automated testing pipeline
1. Run EditMode tests (fail fast)
2. Run PlayMode tests (if EditMode passes)
3. Generate test reports
4. Upload coverage data
5. Notify team of results
```

---

## 🔍 **Debugging Test Failures**

### **Common Issues**
1. **Null Reference Exceptions**: Check singleton initialization
2. **Scene Loading Failures**: Ensure proper scene cleanup
3. **Timing Issues**: Add appropriate `yield return` statements
4. **Mock Data Problems**: Verify mock implementations match real behavior

### **Debug Tools**
```csharp
// Enable detailed logging in tests
[SetUp]
public void Setup()
{
    Debug.unityLogger.logEnabled = true;
    SmartLogger.SetLogLevel(LogLevel.Debug);
}

// Use Unity's built-in profiler
[UnityTest]
public IEnumerator TestWithProfiling()
{
    Profiler.BeginSample("MyTest");
    // Test code here
    Profiler.EndSample();
    yield return null;
}
```

---

## 🚀 **Next Steps**

### **Phase 2-4 Testing Goals**
1. **Phase 2**: Fix AuraManager tests (4 → 0 failing)
2. **Phase 3**: Fix PlayFab tests (2 → 0 failing)  
3. **Phase 4**: Add null handling tests
4. **Final**: Achieve 46/46 tests passing (100%)

### **Future Testing Enhancements**
- Add performance regression tests
- Implement visual testing for UI
- Add stress testing for multiplayer
- Create automated smoke tests for builds

---

**For questions or issues with testing, refer to the [Integration Test Checklist](INTEGRATION_TEST_CHECKLIST.md) or check the test logs in Unity Console.** 