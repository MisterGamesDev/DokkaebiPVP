# Phase 2-4 Implementation Plan

## 🎯 **Goal: Achieve 100% Test Pass Rate (46/46)**

**Current Status**: 40/46 tests passing (87% pass rate)  
**Remaining**: 6 failing tests across 3 phases  
**Timeline**: 3-4 days to completion

---

## 🔥 **Phase 2: Fix AuraManager Logic Bugs** - **HIGH PRIORITY**

### **Issue Summary**
- **Failing Tests**: 4/8 AuraManager tests (50% pass rate)
- **Root Causes**: Initialization bugs, event firing issues, server validation conflicts
- **Impact**: Blocks resource management functionality

### **Detailed Issues & Solutions**

#### **Issue 1: Aura Values Stuck at 0**
```csharp
// Problem: AuraManager initialization failing
// Symptoms: CurrentAura = 0, MaxAura = 0 in all tests
// Location: Likely in AuraManager constructor or Initialize()
```

**Investigation Steps:**
1. Check `AuraManager.Initialize()` method
2. Verify `SetMaxAura()` is being called during setup
3. Ensure singleton pattern isn't causing initialization conflicts
4. Test with direct instantiation vs. singleton access

**Expected Fix:**
```csharp
// Ensure proper initialization order
public void Initialize()
{
    MaxAura = initialMaxAura; // Set before CurrentAura
    CurrentAura = MaxAura;    // Initialize to full
    isInitialized = true;
}
```

#### **Issue 2: Events Not Firing**
```csharp
// Problem: AuraChanged, MaxAuraChanged events never trigger
// Symptoms: Event handlers never called in tests
// Location: Property setters or direct field assignments
```

**Investigation Steps:**
1. Check if events are using properties vs. direct field access
2. Verify event handlers are properly subscribed in tests
3. Ensure events aren't null before invoking

**Expected Fix:**
```csharp
public int CurrentAura 
{ 
    get => currentAura;
    private set 
    { 
        if (currentAura != value)
        {
            currentAura = value;
            AuraChanged?.Invoke(currentAura); // Ensure this fires
        }
    }
}
```

#### **Issue 3: Anti-Cheat Validation Conflicts**
```csharp
// Problem: Server validation rejecting valid aura changes
// Symptoms: MockServerValidator.ValidateAuraChange() returning false
// Location: Server-side validation logic
```

**Investigation Steps:**
1. Check `MockServerValidator.ValidateAuraChange()` logic
2. Verify client-server aura value synchronization
3. Ensure validation thresholds are reasonable

### **Phase 2 Implementation Steps**
1. **Debug AuraManager initialization** (30 min)
   - Add logging to constructor and Initialize()
   - Run failing tests with debug output
   - Identify initialization order issues

2. **Fix event firing mechanisms** (45 min)
   - Review property setters vs. field access
   - Add null checks before event invocation
   - Test event subscription in isolated environment

3. **Resolve server validation conflicts** (30 min)
   - Review MockServerValidator validation logic
   - Ensure client-server value consistency
   - Adjust validation thresholds if too restrictive

4. **Verify all AuraManager tests pass** (15 min)
   - Run AuraManager test category
   - Confirm 8/8 tests now passing
   - Check for any regression in other systems

**Phase 2 Acceptance**: AuraManager tests: 8/8 passing ✅

---

## 🌐 **Phase 3: Deploy CloudScript Functions to PlayFab** - **HIGH PRIORITY**

### **Issue Summary**
- **Failing Tests**: 2/6 PlayFab integration tests (67% pass rate)
- **Root Cause**: Server functions not deployed to PlayFab
- **Impact**: Blocks server-authoritative gameplay

### **Required CloudScript Functions**

#### **Function 1: InitializeMatch.js**
```javascript
// Purpose: Initialize new match with validation
handlers.InitializeMatch = function(args, context) {
    var matchId = generateMatchId();
    var players = validatePlayers(args.players);
    
    return {
        matchId: matchId,
        players: players,
        timestamp: new Date().toISOString(),
        success: true
    };
};
```

#### **Function 2: HealthCheck.js**
```javascript
// Purpose: Verify server connectivity and status
handlers.HealthCheck = function(args, context) {
    return {
        status: "ok",
        timestamp: new Date().toISOString(),
        version: "1.0.0"
    };
};
```

#### **Function 3: ExecuteAbility.js**
```javascript
// Purpose: Server-side ability validation and execution
handlers.ExecuteAbility = function(args, context) {
    var ability = args.ability;
    var target = args.target;
    var caster = args.caster;
    
    // Validate ability usage
    if (!validateAbilityUsage(caster, ability)) {
        return { success: false, error: "Invalid ability usage" };
    }
    
    // Execute ability logic
    var result = executeAbilityLogic(ability, target, caster);
    
    return {
        success: true,
        result: result,
        timestamp: new Date().toISOString()
    };
};
```

### **Phase 3 Implementation Steps**

1. **Prepare CloudScript Files** (15 min)
   - Verify files exist in `Dokkaebi.Server/CloudScripts/`
   - Review function signatures and logic
   - Ensure JavaScript syntax is correct

2. **Access PlayFab Dashboard** (10 min)
   - Log into PlayFab Developer Portal
   - Navigate to Automation → CloudScript → Revisions
   - Prepare for function upload

3. **Deploy Functions** (20 min)
   - Upload each `.js` file to PlayFab
   - Set function names exactly as in files
   - Click "Save and Publish" for each

4. **Configure Unity Settings** (10 min)
   - Verify Title ID in PlayFab settings
   - Ensure API permissions are enabled
   - Test basic connectivity

5. **Test Deployment** (15 min)
   - Use PlayFab CloudScript debugger
   - Test each function individually
   - Verify expected response formats

6. **Run PlayFab Integration Tests** (10 min)
   - Execute PlayFab test category in Unity
   - Confirm 6/6 tests now passing
   - Check for network timeout issues

**Phase 3 Acceptance**: PlayFab integration tests: 6/6 passing ✅

---

## 🛡️ **Phase 4: Fix MockServerValidator Null Handling** - **MEDIUM PRIORITY**

### **Issue Summary**
- **Failing Tests**: 2 null validation tests
- **Root Cause**: Missing null checks in MockServerValidator
- **Impact**: Crashes on invalid input during testing

### **Specific Issues**

#### **Issue 1: TestNullInputValidation**
```csharp
// Problem: MockServerValidator crashes on null input
// Symptoms: NullReferenceException when validating null objects
// Location: Input validation methods
```

#### **Issue 2: TestNullMovementValidation**  
```csharp
// Problem: Movement validation missing null checks
// Symptoms: Crashes when validating null movement data
// Location: ValidateMovement() method
```

### **Phase 4 Implementation Steps**

1. **Identify Null Reference Locations** (15 min)
   - Run failing tests with debugger
   - Locate exact crash points
   - Review MockServerValidator methods

2. **Add Comprehensive Null Checks** (30 min)
   ```csharp
   public bool ValidateInput(object input)
   {
       if (input == null) 
       {
           LogError("Input validation failed: null input");
           return false;
       }
       
       // Continue with validation logic
       return ValidateInputInternal(input);
   }
   ```

3. **Implement Graceful Error Handling** (15 min)
   - Return false instead of throwing exceptions
   - Add meaningful error messages
   - Ensure consistent behavior across all validation methods

4. **Test Null Handling** (10 min)
   - Run null validation tests
   - Confirm no more crashes
   - Verify appropriate error responses

**Phase 4 Acceptance**: All MockServerValidator tests passing ✅

---

## 🎯 **Final Verification Phase**

### **Complete Testing Checklist**
1. **Run Full Test Suite** (5 min)
   - Execute all 46 tests in Unity Test Runner
   - Verify 46/46 passing (100% pass rate)
   - Check for any unexpected regressions

2. **Manual Play Mode Testing** (15 min)
   - Load BattleScene in Unity Editor
   - Test complete gameplay flow:
     - Unit selection and movement
     - Ability targeting and execution
     - Turn progression and UI updates
     - Win/loss condition triggering

3. **Performance Validation** (5 min)
   - Verify test suite runs in ~60 seconds
   - Check for memory leaks or performance issues
   - Ensure smooth gameplay in Play Mode

4. **Documentation Updates** (5 min)
   - Update `status.md` with 100% pass rate
   - Mark Phase 2-4 as completed
   - Update test results in all documentation

### **Success Criteria**
- ✅ All 46 tests passing (100% pass rate)
- ✅ No console errors during gameplay
- ✅ Smooth UI responsiveness
- ✅ Server functions deployed and working
- ✅ Robust error handling for edge cases

---

## 📊 **Timeline & Effort Estimation**

| Phase | Tasks | Estimated Time | Priority |
|-------|-------|----------------|----------|
| **Phase 2** | AuraManager fixes | 2 hours | HIGH |
| **Phase 3** | PlayFab deployment | 1.5 hours | HIGH |
| **Phase 4** | Null handling | 1 hour | MEDIUM |
| **Final** | Verification | 30 minutes | HIGH |
| **Total** | **All phases** | **5 hours** | - |

### **Recommended Schedule**
- **Day 1**: Phase 2 (AuraManager fixes)
- **Day 2**: Phase 3 (PlayFab deployment)  
- **Day 3**: Phase 4 (Null handling) + Final verification
- **Day 4**: Buffer for unexpected issues

---

## 🚨 **Risk Mitigation**

### **Potential Blockers**
1. **PlayFab Access Issues**: Ensure account credentials are available
2. **AuraManager Architecture**: May require deeper refactoring than expected
3. **Network Connectivity**: PlayFab deployment requires stable internet

### **Contingency Plans**
1. **Mock PlayFab**: If deployment fails, create local mock for testing
2. **Incremental Fixes**: Fix AuraManager issues one test at a time
3. **Documentation**: Keep detailed notes of any architectural decisions

---

**Next Action**: Begin Phase 2 AuraManager debugging and fixes. 