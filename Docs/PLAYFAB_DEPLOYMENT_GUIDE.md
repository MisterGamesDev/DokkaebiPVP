# PlayFab Deployment Guide

## 🚀 **Phase 3: Deploy CloudScript Functions to PlayFab**

This guide covers deploying server-side functions to PlayFab to enable full server-authoritative gameplay and unblock 6 PlayFab integration tests.

---

## 📋 **Prerequisites**

### Required Setup
- [ ] PlayFab account created and verified
- [ ] Dokkaebi title created in PlayFab dashboard
- [ ] Title ID configured in Unity project
- [ ] PlayFab SDK properly installed (✅ Already done)

### Files to Deploy
Located in `Dokkaebi.Server/CloudScripts/`:
- `InitializeMatch.js` - Match setup and validation
- `HealthCheck.js` - Server connectivity verification  
- `ExecuteAbility.js` - Server-side ability validation

---

## 🔧 **Step 1: PlayFab Dashboard Setup**

### 1.1 Access CloudScript
1. Log into [PlayFab Developer Portal](https://developer.playfab.com)
2. Select your Dokkaebi title
3. Navigate to **Automation** → **CloudScript** → **Revisions**

### 1.2 Create New Revision
1. Click **"Upload New Revision"**
2. Select **"CloudScript (Legacy)"** for JavaScript functions
3. Prepare to upload our 3 CloudScript files

---

## 📤 **Step 2: Deploy CloudScript Functions**

### 2.1 Upload Functions
```bash
# Files to upload from Dokkaebi.Server/CloudScripts/
1. InitializeMatch.js
2. HealthCheck.js  
3. ExecuteAbility.js
```

### 2.2 Function Descriptions

#### **InitializeMatch.js**
```javascript
// Purpose: Initialize new match with validation
// Called by: Unity client on match start
// Returns: Match configuration and initial state
```

#### **HealthCheck.js**
```javascript
// Purpose: Verify server connectivity and status
// Called by: Unity client for connection testing
// Returns: Server status and response time
```

#### **ExecuteAbility.js**
```javascript
// Purpose: Server-side ability validation and execution
// Called by: Unity client when using abilities
// Returns: Validated ability results
```

### 2.3 Upload Process
1. Copy content from each `.js` file
2. Paste into PlayFab CloudScript editor
3. Set function names exactly as in files
4. Click **"Save and Publish"**

---

## ⚙️ **Step 3: Configuration**

### 3.1 Title Settings
1. Navigate to **Settings** → **Title Settings**
2. Note your **Title ID** (format: XXXXX)
3. Ensure API access is enabled

### 3.2 Unity Configuration
Update Unity project settings:
```csharp
// In PlayFab settings or initialization code
PlayFabSettings.TitleId = "YOUR_TITLE_ID";
PlayFabSettings.DeveloperSecretKey = "YOUR_SECRET_KEY"; // For server calls only
```

### 3.3 API Permissions
Ensure these APIs are enabled:
- [ ] Client/ExecuteCloudScript
- [ ] Client/GetTitleData
- [ ] Server/ExecuteCloudScript (if using server APIs)

---

## 🧪 **Step 4: Testing Deployment**

### 4.1 Unity Editor Testing
1. Open Unity Editor
2. Navigate to **PlayFab** → **Tools** → **CloudScript Debugger**
3. Test each function individually:

```csharp
// Test HealthCheck
PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest()
{
    FunctionName = "HealthCheck",
    FunctionParameter = new { }
}, OnHealthCheckSuccess, OnHealthCheckError);
```

### 4.2 Verify Function Responses
Expected responses:
- **HealthCheck**: `{ "status": "ok", "timestamp": "..." }`
- **InitializeMatch**: `{ "matchId": "...", "players": [...] }`
- **ExecuteAbility**: `{ "success": true, "result": {...} }`

### 4.3 Run PlayFab Integration Tests
```csharp
// In Unity Test Runner
// Navigate to: Window → General → Test Runner
// Run: PlayFabIntegrationTests
// Expected: 6/6 tests should now pass
```

---

## 🐛 **Troubleshooting**

### Common Issues

#### **"Function not found" Error**
- **Cause**: Function name mismatch
- **Solution**: Ensure exact naming in PlayFab matches `.js` files
- **Check**: Case sensitivity matters

#### **"Title ID not configured" Error**  
- **Cause**: Missing or incorrect Title ID
- **Solution**: Verify Title ID in Unity PlayFab settings
- **Location**: `PlayFabSettings.TitleId`

#### **"CloudScript execution failed" Error**
- **Cause**: JavaScript syntax errors in uploaded functions
- **Solution**: Check CloudScript logs in PlayFab dashboard
- **Debug**: Use PlayFab CloudScript debugger

#### **Network/Timeout Errors**
- **Cause**: Connectivity issues or server overload
- **Solution**: Check internet connection, try again
- **Alternative**: Use PlayFab's test environment

---

## ✅ **Verification Checklist**

### Deployment Complete When:
- [ ] All 3 CloudScript functions uploaded successfully
- [ ] Functions appear in PlayFab CloudScript revisions
- [ ] Unity can call each function without errors
- [ ] PlayFab integration tests pass (6/6)
- [ ] No console errors during gameplay
- [ ] Server responses match expected format

### Test Results Expected:
```
PlayFab Integration Tests: 6/6 PASSING ✅
- HealthCheck connectivity: PASS
- InitializeMatch setup: PASS  
- ExecuteAbility validation: PASS
- Error handling: PASS
- Response format: PASS
- Performance benchmarks: PASS
```

---

## 📚 **Additional Resources**

- [PlayFab CloudScript Documentation](https://docs.microsoft.com/en-us/gaming/playfab/features/automation/cloudscript/)
- [Unity PlayFab SDK Guide](https://docs.microsoft.com/en-us/gaming/playfab/sdks/unity3d/)
- [CloudScript Best Practices](https://docs.microsoft.com/en-us/gaming/playfab/features/automation/cloudscript/writing-custom-cloudscript/)

---

## 🚨 **Security Notes**

- Never commit PlayFab secret keys to version control
- Use environment variables for sensitive configuration
- Test with development Title ID before production deployment
- Monitor CloudScript execution logs for suspicious activity

---

**Next Steps After Deployment:**
1. Proceed to Phase 4: MockServerValidator null handling fixes
2. Run final verification testing (all 46 tests)
3. Perform comprehensive Play Mode validation 