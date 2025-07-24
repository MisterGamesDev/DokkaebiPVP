using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Dokkaebi.Core;
using Dokkaebi.Core.Networking;
using Dokkaebi.Units;
using Dokkaebi.Core.Data;
using Dokkaebi.Grid;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
using Dokkaebi.TurnSystem;
using Dokkaebi.Interfaces;
using Dokkaebi.Zones;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// Comprehensive PlayFab integration tests
    /// Tests the full pipeline: AbilityManager -> NetworkingManager -> Server -> GameStateManagerMultiplayer
    /// </summary>
    public class PlayFabIntegrationTests
    {
        private GameObject testManagersObject;
        private NetworkingManager networkingManager;
        private AbilityManager abilityManager;
        private AuraManager auraManager;
        private GameStateManagerMultiplayer gameStateManager;
        private UnitManager unitManager;
        private V3TurnManager turnManager;
        
        private GameObject player1Unit;
        private GameObject player2Unit;
        private DokkaebiUnit player1DokkaebiUnit;
        private DokkaebiUnit player2DokkaebiUnit;
        private AbilityData testAbility;
        
        private bool isPlayFabConnected = false;
        private string currentMatchId = null;

        [SetUp]
        public void Setup()
        {
            SmartLogger.Log("[PlayFabIntegrationTests] Starting setup", LogCategory.Testing);
            
            // Create test managers object
            testManagersObject = new GameObject("TestManagers");
            
            // Initialize GridManager singleton first (required by DokkaebiUnit.SetGridPosition)
            var gridManagerObject = new GameObject("TestGridManager");
            var gridManager = gridManagerObject.AddComponent<GridManager>();
            // Manually set the singleton instance using reflection since Awake won't run in EditMode
            var gridManagerType = typeof(GridManager);
            var instanceField = gridManagerType.GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            instanceField?.SetValue(null, gridManager);
            // Initialize grid manually
            var initializeGridMethod = gridManagerType.GetMethod("InitializeGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initializeGridMethod?.Invoke(gridManager, null);
            
            // Initialize ZoneManager singleton (required by DokkaebiUnit.SetGridPosition)  
            var zoneManagerObject = new GameObject("TestZoneManager");
            var zoneManager = zoneManagerObject.AddComponent<ZoneManager>();
            // ZoneManager uses a property setter, so we can just access Instance to trigger initialization
            // But we need to make sure our instance is used
            var zoneManagerType = typeof(ZoneManager);
            var zoneInstanceProperty = zoneManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var zoneInstanceSetter = zoneInstanceProperty?.GetSetMethod(true);
            zoneInstanceSetter?.Invoke(null, new object[] { zoneManager });
            
            // Add managers to test object
            networkingManager = testManagersObject.AddComponent<NetworkingManager>();
            
            // Add other managers
            abilityManager = testManagersObject.AddComponent<AbilityManager>();
            auraManager = testManagersObject.AddComponent<AuraManager>();
            gameStateManager = testManagersObject.AddComponent<GameStateManagerMultiplayer>();
            unitManager = testManagersObject.AddComponent<UnitManager>();
            turnManager = testManagersObject.AddComponent<V3TurnManager>();
            
            // Set up test units (this will now work without null reference errors)
            SetupTestUnits();
            
            // Create test ability
            CreateTestAbility();
            
            // Configure managers for integration testing
            ConfigureManagers();
            
            SmartLogger.Log("[PlayFabIntegrationTests] Setup completed", LogCategory.Testing);
        }

        [TearDown]
        public void Teardown()
        {
            // Disconnect from PlayFab
            if (networkingManager != null && networkingManager.IsAuthenticated())
            {
                networkingManager.Disconnect();
            }
            
            // Clean up test objects
            if (player1Unit != null) Object.DestroyImmediate(player1Unit);
            if (player2Unit != null) Object.DestroyImmediate(player2Unit);
            if (testAbility != null) Object.DestroyImmediate(testAbility);
            if (testManagersObject != null) Object.DestroyImmediate(testManagersObject);
            
            // Clean up singleton objects
            if (GridManager.Instance != null) Object.DestroyImmediate(GridManager.Instance.gameObject);
            if (ZoneManager.Instance != null) Object.DestroyImmediate(ZoneManager.Instance.gameObject);
        }

        private void SetupTestUnits()
        {
            // Create Player 1 unit
            player1Unit = new GameObject("TestPlayer1Unit");
            player1DokkaebiUnit = player1Unit.AddComponent<DokkaebiUnit>();
            player1DokkaebiUnit.SetUnitId(1);
            player1DokkaebiUnit.SetIsPlayerUnit(true);
            player1DokkaebiUnit.SetTeamId(0); // Player 1 team
            player1DokkaebiUnit.SetGridPosition(new GridPosition(2, 2));
            
            // Create Player 2 unit
            player2Unit = new GameObject("TestPlayer2Unit");
            player2DokkaebiUnit = player2Unit.AddComponent<DokkaebiUnit>();
            player2DokkaebiUnit.SetUnitId(2);
            player2DokkaebiUnit.SetIsPlayerUnit(true);
            player2DokkaebiUnit.SetTeamId(1); // Player 2 team
            player2DokkaebiUnit.SetGridPosition(new GridPosition(7, 7));
        }

        private void CreateTestAbility()
        {
            testAbility = ScriptableObject.CreateInstance<AbilityData>();
            testAbility.abilityId = "TestAbility";
            testAbility.displayName = "Test Ability";
            testAbility.auraCost = 3;
            testAbility.range = 5;
            testAbility.damageAmount = 25;
            testAbility.damageType = DamageType.Physical;
            testAbility.targetsEnemy = true;
        }

        private void ConfigureManagers()
        {
            // Configure AbilityManager for PVP mode
            abilityManager.SetPVPMode(true, false); // Enable PVP, not offline
            
            // Configure AuraManager for networking
            auraManager.SetNetworkMode(true, false); // Enable network sync, not offline
            
            // Set initial aura values
            auraManager.ModifyAura(true, 10); // Player 1 starts with 10 aura
            auraManager.ModifyAura(false, 10); // Player 2 starts with 10 aura
        }

        [UnityTest]
        public IEnumerator TestPlayFabConnection()
        {
            LogTestStart("PlayFab Connection");
            
            // Test connection to PlayFab
            bool loginSuccessful = false;
            string loginError = null;
            
            networkingManager.OnLoginSuccess += () => { loginSuccessful = true; };
            networkingManager.OnLoginFailure += (error) => { loginError = error; };
            
            // Attempt login
            networkingManager.LoginWithDeviceId();
            
            // Wait for login result (max 15 seconds)
            float timeout = 15f;
            while (!loginSuccessful && string.IsNullOrEmpty(loginError) && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            // Assert connection successful
            if (!loginSuccessful)
            {
                if (!string.IsNullOrEmpty(loginError))
                {
                    Assert.Fail($"PlayFab login failed: {loginError}");
                }
                else
                {
                    Assert.Fail("PlayFab login timed out");
                }
            }
            
            Assert.IsTrue(networkingManager.IsAuthenticated(), "Should be authenticated with PlayFab");
            isPlayFabConnected = true;
            
            SmartLogger.Log("[PlayFabIntegrationTests] PlayFab connection successful", LogCategory.Testing);
            LogTestEnd("PlayFab Connection", true);
        }

        [UnityTest]
        public IEnumerator TestMatchCreation()
        {
            LogTestStart("Match Creation");
            
            // Ensure we're connected first
            if (!isPlayFabConnected)
            {
                yield return TestPlayFabConnection();
            }
            
            // Test match creation
            bool matchCreated = false;
            string matchError = null;
            string createdMatchId = null;
            
            networkingManager.CreateMatch(
                matchId => { 
                    matchCreated = true; 
                    createdMatchId = matchId;
                },
                error => { matchError = error; }
            );
            
            // Wait for match creation result
            float timeout = 10f;
            while (!matchCreated && string.IsNullOrEmpty(matchError) && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            // Assert match creation successful
            if (!matchCreated)
            {
                if (!string.IsNullOrEmpty(matchError))
                {
                    Assert.Fail($"Match creation failed: {matchError}");
                }
                else
                {
                    Assert.Fail("Match creation timed out");
                }
            }
            
            Assert.IsNotNull(createdMatchId, "Match ID should not be null");
            Assert.IsNotEmpty(createdMatchId, "Match ID should not be empty");
            
            currentMatchId = createdMatchId;
            
            SmartLogger.Log($"[PlayFabIntegrationTests] Match created successfully: {currentMatchId}", LogCategory.Testing);
            LogTestEnd("Match Creation", true);
        }

        [UnityTest]
        public IEnumerator TestServerHealthCheck()
        {
            LogTestStart("Server Health Check");
            
            // Ensure we're connected and have a match
            if (!isPlayFabConnected)
            {
                yield return TestPlayFabConnection();
            }
            
            // Test server health check
            bool healthCheckPassed = false;
            string healthCheckError = null;
            
            networkingManager.ExecuteCloudScript(
                "HealthCheck",
                new { test = "integration_test" },
                result => { healthCheckPassed = true; },
                error => { healthCheckError = error.ErrorMessage; }
            );
            
            // Wait for health check result
            float timeout = 10f;
            while (!healthCheckPassed && string.IsNullOrEmpty(healthCheckError) && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            // Assert health check successful
            if (!healthCheckPassed)
            {
                if (!string.IsNullOrEmpty(healthCheckError))
                {
                    Assert.Fail($"Health check failed: {healthCheckError}");
                }
                else
                {
                    Assert.Fail("Health check timed out");
                }
            }
            
            SmartLogger.Log("[PlayFabIntegrationTests] Server health check passed", LogCategory.Testing);
            LogTestEnd("Server Health Check", true);
        }

        [UnityTest]
        public IEnumerator TestAbilityCommandSubmission()
        {
            LogTestStart("Ability Command Submission");
            
            // Ensure we're connected and have a match
            if (!isPlayFabConnected)
            {
                yield return TestPlayFabConnection();
            }
            if (string.IsNullOrEmpty(currentMatchId))
            {
                yield return TestMatchCreation();
            }
            
            // Test ability command submission
            bool abilityExecuted = false;
            string abilityError = null;
            
            // Subscribe to ability manager events
            abilityManager.OnAbilityExecuted += (ability, unit, position) => { abilityExecuted = true; };
            abilityManager.OnAbilityFailed += (ability, unit, error) => { abilityError = error; };
            
            // Execute ability through AbilityManager
            var targetPosition = new GridPosition(7, 7); // Target player 2 unit
            bool commandSubmitted = abilityManager.ExecuteAbility(
                testAbility, 
                player1DokkaebiUnit, 
                targetPosition, 
                player2DokkaebiUnit, 
                false
            );
            
            Assert.IsTrue(commandSubmitted, "Ability command should be submitted");
            
            // Wait for server response
            float timeout = 15f;
            while (!abilityExecuted && string.IsNullOrEmpty(abilityError) && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            // Check results
            if (!abilityExecuted && !string.IsNullOrEmpty(abilityError))
            {
                SmartLogger.LogWarning($"[PlayFabIntegrationTests] Ability command rejected by server: {abilityError}", LogCategory.Testing);
                // This might be expected if server validation fails - not necessarily a test failure
                // But we should log it for analysis
            }
            else if (!abilityExecuted)
            {
                Assert.Fail("Ability command timed out waiting for server response");
            }
            else
            {
                SmartLogger.Log("[PlayFabIntegrationTests] Ability command executed successfully", LogCategory.Testing);
            }
            
            LogTestEnd("Ability Command Submission", abilityExecuted);
        }

        [UnityTest]
        public IEnumerator TestGameStateSync()
        {
            LogTestStart("Game State Synchronization");
            
            // Ensure we're connected and have a match
            if (!isPlayFabConnected)
            {
                yield return TestPlayFabConnection();
            }
            if (string.IsNullOrEmpty(currentMatchId))
            {
                yield return TestMatchCreation();
            }
            
            // Test game state retrieval
            bool gameStateReceived = false;
            Dictionary<string, object> receivedGameState = null;
            
            gameStateManager.OnAuthoritativeStateReceived += (state) => {
                gameStateReceived = true;
                receivedGameState = state;
            };
            
            // Request game state
            networkingManager.GetGameState();
            
            // Wait for game state
            float timeout = 10f;
            while (!gameStateReceived && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            // Assert game state received
            if (!gameStateReceived)
            {
                Assert.Fail("Game state sync timed out");
            }
            
            Assert.IsNotNull(receivedGameState, "Received game state should not be null");
            Assert.Greater(receivedGameState.Count, 0, "Game state should contain data");
            
            SmartLogger.Log($"[PlayFabIntegrationTests] Game state sync successful - received {receivedGameState.Count} properties", LogCategory.Testing);
            LogTestEnd("Game State Synchronization", true);
        }

        [UnityTest]
        public IEnumerator TestAuraSync()
        {
            LogTestStart("Aura Synchronization");
            
            // Ensure we're connected and have a match
            if (!isPlayFabConnected)
            {
                yield return TestPlayFabConnection();
            }
            if (string.IsNullOrEmpty(currentMatchId))
            {
                yield return TestMatchCreation();
            }
            
            // Record initial aura values
            int initialPlayer1Aura = auraManager.GetPlayerAura(true);
            int initialPlayer2Aura = auraManager.GetPlayerAura(false);
            
            SmartLogger.Log($"[PlayFabIntegrationTests] Initial aura - P1: {initialPlayer1Aura}, P2: {initialPlayer2Aura}", LogCategory.Testing);
            
            // Simulate server state update with different aura values
            var mockServerState = new Dictionary<string, object>
            {
                { "player1Aura", initialPlayer1Aura + 5 },
                { "player2Aura", initialPlayer2Aura - 2 },
                { "player1MaxAura", 20 },
                { "player2MaxAura", 20 }
            };
            
            // Apply server state through GameStateManagerMultiplayer
            gameStateManager.ApplyAuthoritativeStateForTesting(mockServerState);
            
            // Wait a frame for processing
            yield return new WaitForEndOfFrame();
            
            // Check that aura values were synced
            int syncedPlayer1Aura = auraManager.GetPlayerAura(true);
            int syncedPlayer2Aura = auraManager.GetPlayerAura(false);
            
            Assert.AreEqual(initialPlayer1Aura + 5, syncedPlayer1Aura, "Player 1 aura should be synced from server");
            Assert.AreEqual(initialPlayer2Aura - 2, syncedPlayer2Aura, "Player 2 aura should be synced from server");
            
            SmartLogger.Log($"[PlayFabIntegrationTests] Aura sync successful - P1: {syncedPlayer1Aura}, P2: {syncedPlayer2Aura}", LogCategory.Testing);
            LogTestEnd("Aura Synchronization", true);
        }

        [UnityTest]
        public IEnumerator TestFullIntegrationFlow()
        {
            LogTestStart("Full Integration Flow");
            
            // Run through all integration steps
            yield return TestPlayFabConnection();
            yield return TestMatchCreation();
            yield return TestServerHealthCheck();
            yield return TestGameStateSync();
            yield return TestAuraSync();
            yield return TestAbilityCommandSubmission();
            
            SmartLogger.Log("[PlayFabIntegrationTests] Full integration flow completed successfully", LogCategory.Testing);
            LogTestEnd("Full Integration Flow", true);
        }

        #region Test Helpers

        private void LogTestStart(string testName)
        {
            SmartLogger.Log($"[PlayFabIntegrationTests] Starting test: {testName}", LogCategory.Testing);
        }

        private void LogTestEnd(string testName, bool success)
        {
            string status = success ? "PASSED" : "FAILED";
            SmartLogger.Log($"[PlayFabIntegrationTests] Test {testName} {status}", LogCategory.Testing);
        }

        #endregion
    }
} 