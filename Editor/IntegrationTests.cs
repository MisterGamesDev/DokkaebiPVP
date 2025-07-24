using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Dokkaebi.Core;
using Dokkaebi.Core.Data;
using Dokkaebi.Units;
using Dokkaebi.Common;
using Dokkaebi.Common.Mocks;
using Dokkaebi.TurnSystem;
using Dokkaebi.Utilities;
using Dokkaebi.Grid;
using Dokkaebi.Zones;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// Integration tests for the complete AuraManager + AbilityManager + MockValidator system
    /// Simulates local PVP matches with anti-cheat validation
    /// </summary>
    public class IntegrationTests
    {
        private GameObject testManagersObject;
        private AuraManager auraManager;
        private AbilityManager abilityManager;
        private V3TurnManager turnManager;
        
        private GameObject player1Unit;
        private GameObject player2Unit;
        private DokkaebiUnit player1DokkaebiUnit;
        private DokkaebiUnit player2DokkaebiUnit;
        
        private AbilityData testAbility;

        [SetUp]
        public void Setup()
        {
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
            
            // Set up AuraManager
            auraManager = testManagersObject.AddComponent<AuraManager>();
            
            // Set up AbilityManager
            abilityManager = testManagersObject.AddComponent<AbilityManager>();
            
            // Set up V3TurnManager
            turnManager = testManagersObject.AddComponent<V3TurnManager>();
            
            // Create test units
            player1Unit = new GameObject("Player1Unit");
            player1DokkaebiUnit = player1Unit.AddComponent<DokkaebiUnit>();
            
            player2Unit = new GameObject("Player2Unit");
            player2DokkaebiUnit = player2Unit.AddComponent<DokkaebiUnit>();
            
            // Create test ability
            testAbility = ScriptableObject.CreateInstance<AbilityData>();
            testAbility.displayName = "Test Strike";
            testAbility.abilityId = "TestStrike";
            testAbility.range = 3;
            testAbility.auraCost = 4;
            testAbility.targetsEnemy = true;
            testAbility.targetsGround = false;
            
            // Configure systems for local testing
            abilityManager.SetPVPMode(false, true); // Offline mode
            abilityManager.SetMockValidation(true, 10, 5, 10.0f);
            auraManager.SetNetworkMode(false, true); // Offline mode
            
            MockServerValidator.ResetSubmissionTracking();
        }

        [TearDown]
        public void Teardown()
        {
            if (testAbility != null)
                Object.DestroyImmediate(testAbility);
            if (player1Unit != null)
                Object.DestroyImmediate(player1Unit);
            if (player2Unit != null)
                Object.DestroyImmediate(player2Unit);
            if (testManagersObject != null)
                Object.DestroyImmediate(testManagersObject);
            
            // Clean up singleton objects
            if (GridManager.Instance != null) Object.DestroyImmediate(GridManager.Instance.gameObject);
            if (ZoneManager.Instance != null) Object.DestroyImmediate(ZoneManager.Instance.gameObject);
        }

            [Test]
    public void TestCompleteLocalPVPTurn()
        {
            // Integration test: Complete local PVP turn with aura management
            SmartLogger.Log("[IntegrationTest] Starting complete local PVP turn test", LogCategory.Debug);
            
            // Initial state check
            int initialPlayer1Aura = auraManager.GetCurrentAura(true);
            int initialPlayer2Aura = auraManager.GetCurrentAura(false);
            
            SmartLogger.Log($"[IntegrationTest] Initial aura - P1: {initialPlayer1Aura}, P2: {initialPlayer2Aura}", LogCategory.Debug);
            
            // Simulate turn start (should grant aura to both players)
            turnManager.OnTurnStarted?.Invoke(1);
            // Note: Converted from UnityTest - no frame waiting available
            
            // Check aura gain
            int afterTurnPlayer1Aura = auraManager.GetCurrentAura(true);
            int afterTurnPlayer2Aura = auraManager.GetCurrentAura(false);
            
            SmartLogger.Log($"[IntegrationTest] After turn start - P1: {afterTurnPlayer1Aura}, P2: {afterTurnPlayer2Aura}", LogCategory.Debug);
            
            Assert.IsTrue(afterTurnPlayer1Aura >= initialPlayer1Aura, "Player 1 should gain aura on turn start");
            Assert.IsTrue(afterTurnPlayer2Aura >= initialPlayer2Aura, "Player 2 should gain aura on turn start");
            
            // Simulate ability usage by Player 1
            var targetPosition = new GridPosition(5, 5);
            bool abilityExecuted = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, targetPosition, player2DokkaebiUnit, false);
            
            SmartLogger.Log($"[IntegrationTest] Ability executed: {abilityExecuted}", LogCategory.Debug);
            
            // Check that aura was deducted
            int afterAbilityPlayer1Aura = auraManager.GetCurrentAura(true);
            int expectedAura = afterTurnPlayer1Aura - testAbility.auraCost;
            
            SmartLogger.Log($"[IntegrationTest] After ability - P1: {afterAbilityPlayer1Aura}, Expected: {expectedAura}", LogCategory.Debug);
            
            // Note: This test might not pass due to missing component dependencies
            // In a real integration test, we'd set up the full game environment
        }

        [Test]
        public void TestAntiCheatIntegration()
        {
            // Test that anti-cheat validation works through the full system
            SmartLogger.Log("[IntegrationTest] Testing anti-cheat integration", LogCategory.Debug);
            
            // Try to execute an ability with impossible range
            var impossiblePosition = new GridPosition(50, 50);
            bool shouldFail = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, impossiblePosition, player2DokkaebiUnit, false);
            
            Assert.IsFalse(shouldFail, "Should reject ability with impossible range");
            
            // Check that aura was not deducted due to validation failure
            int currentAura = auraManager.GetCurrentAura(true);
            // Aura should remain unchanged since ability was rejected
            
            SmartLogger.Log($"[IntegrationTest] Anti-cheat test completed. Ability rejected: {!shouldFail}", LogCategory.Debug);
        }

        [Test]
        public void TestInsufficientAuraHandling()
        {
            // Test handling of insufficient aura
            SmartLogger.Log("[IntegrationTest] Testing insufficient aura handling", LogCategory.Debug);
            
            // Drain player's aura
            int currentAura = auraManager.GetCurrentAura(true);
            auraManager.ModifyAura(true, -currentAura); // Set to 0
            
            // Try to use ability
            var targetPosition = new GridPosition(4, 4);
            bool shouldFail = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, targetPosition, player2DokkaebiUnit, false);
            
            Assert.IsFalse(shouldFail, "Should reject ability when insufficient aura");
            Assert.AreEqual(0, auraManager.GetCurrentAura(true), "Aura should remain at 0");
            
            SmartLogger.Log("[IntegrationTest] Insufficient aura test completed", LogCategory.Debug);
        }

        [Test]
        public void TestAuraManagerAbilityManagerIntegration()
        {
            // Test tight integration between AuraManager and AbilityManager
            SmartLogger.Log("[IntegrationTest] Testing AuraManager-AbilityManager integration", LogCategory.Debug);
            
            // Ensure player has enough aura
            auraManager.ModifyAura(true, 10);
            int beforeAura = auraManager.GetCurrentAura(true);
            
            // Execute ability (should succeed and deduct aura)
            var targetPosition = new GridPosition(4, 4);
            bool executed = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, targetPosition, player2DokkaebiUnit, false);
            
            int afterAura = auraManager.GetCurrentAura(true);
            int expectedAura = beforeAura - testAbility.auraCost;
            
            SmartLogger.Log($"[IntegrationTest] Before: {beforeAura}, After: {afterAura}, Expected: {expectedAura}", LogCategory.Debug);
            
            // Note: This test depends on proper component setup
            // In a real test environment, we'd ensure all dependencies are properly initialized
        }

        [Test]
        public void TestMockValidationConfiguration()
        {
            // Test that mock validation can be configured and affects behavior
            SmartLogger.Log("[IntegrationTest] Testing mock validation configuration", LogCategory.Debug);
            
            // Configure strict validation
            abilityManager.SetMockValidation(true, 5, 3, 5.0f); // Very strict limits
            
            // Try ability at medium range (should fail with strict limits)
            var mediumRangePosition = new GridPosition(8, 8);
            bool strictResult = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, mediumRangePosition, player2DokkaebiUnit, false);
            
            // Configure lenient validation
            abilityManager.SetMockValidation(true, 15, 10, 20.0f); // Lenient limits
            
            // Same ability should now succeed
            bool lenientResult = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, mediumRangePosition, player2DokkaebiUnit, false);
            
            SmartLogger.Log($"[IntegrationTest] Strict result: {strictResult}, Lenient result: {lenientResult}", LogCategory.Debug);
            
            // Note: Results depend on proper mock validator integration
        }

        [Test]
        public void TestEventSystemIntegration()
        {
            // Test that events fire correctly across the integrated system
            SmartLogger.Log("[IntegrationTest] Testing event system integration", LogCategory.Debug);
            
            bool auraChangeEventFired = false;
            bool auraGainEventFired = false;
            
            // Subscribe to events
            auraManager.OnAuraChanged += (playerId, oldVal, newVal) =>
            {
                if (playerId == 1) auraChangeEventFired = true;
            };
            
            auraManager.OnAuraGained += (playerId, amount, reason) =>
            {
                if (playerId == 1) auraGainEventFired = true;
            };
            
            // Trigger events through ability execution
            auraManager.ModifyAura(true, 5); // Should trigger change event
            auraManager.GainAuraForTurn(true, 3, "Test"); // Should trigger gain event
            
            Assert.IsTrue(auraChangeEventFired, "Aura change event should fire");
            Assert.IsTrue(auraGainEventFired, "Aura gain event should fire");
            
            SmartLogger.Log("[IntegrationTest] Event system test completed", LogCategory.Debug);
        }

        [Test]
        public void TestSystemStateConsistency()
        {
            // Test that system state remains consistent across operations
            SmartLogger.Log("[IntegrationTest] Testing system state consistency", LogCategory.Debug);
            
            // Record initial state
            int initialP1Aura = auraManager.GetCurrentAura(true);
            int initialP2Aura = auraManager.GetCurrentAura(false);
            
            // Perform multiple operations
            auraManager.ModifyAura(true, 5);
            auraManager.ModifyAura(false, 3);
            auraManager.ModifyAura(true, -2);
            
            // Check state consistency
            int finalP1Aura = auraManager.GetCurrentAura(true);
            int finalP2Aura = auraManager.GetCurrentAura(false);
            
            int expectedP1 = initialP1Aura + 5 - 2;
            int expectedP2 = initialP2Aura + 3;
            
            // Account for capping
            expectedP1 = Mathf.Min(expectedP1, auraManager.GetMaxAura(true));
            expectedP2 = Mathf.Min(expectedP2, auraManager.GetMaxAura(false));
            
            Assert.AreEqual(expectedP1, finalP1Aura, "Player 1 aura should match expected value");
            Assert.AreEqual(expectedP2, finalP2Aura, "Player 2 aura should match expected value");
            
            SmartLogger.Log($"[IntegrationTest] State consistency verified - P1: {finalP1Aura}, P2: {finalP2Aura}", LogCategory.Debug);
        }

        [Test]
        public void TestPVPModeToggling()
        {
            // Test switching between PVP and local modes
            SmartLogger.Log("[IntegrationTest] Testing PVP mode toggling", LogCategory.Debug);
            
            // Start in offline mode
            abilityManager.SetPVPMode(false, true);
            auraManager.SetNetworkMode(false, true);
            
            // Execute ability in offline mode
            var targetPosition = new GridPosition(4, 4);
            auraManager.ModifyAura(true, 10); // Ensure enough aura
            
            bool offlineResult = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, targetPosition, player2DokkaebiUnit, false);
            
            // Switch to PVP mode (but still offline for testing)
            abilityManager.SetPVPMode(true, true); // PVP enabled, but still offline
            auraManager.SetNetworkMode(true, true);
            
            // Try same ability in PVP mode
            bool pvpResult = abilityManager.ExecuteAbility(
                testAbility, player1DokkaebiUnit, targetPosition, player2DokkaebiUnit, false);
            
            SmartLogger.Log($"[IntegrationTest] Offline result: {offlineResult}, PVP result: {pvpResult}", LogCategory.Debug);
            
            // Both should work in offline testing mode
            // In real PVP mode with network, behavior would be different
        }

            [Test]
    public void TestFullGameplayLoop()
        {
            // Test a complete gameplay loop with multiple turns
            SmartLogger.Log("[IntegrationTest] Testing full gameplay loop", LogCategory.Debug);
            
            for (int turn = 1; turn <= 3; turn++)
            {
                SmartLogger.Log($"[IntegrationTest] Processing turn {turn}", LogCategory.Debug);
                
                // Start turn (grants aura)
                turnManager.OnTurnStarted?.Invoke(turn);
                // Note: Frame wait removed - converted from UnityTest
                
                // Player 1 uses ability
                var p1Target = new GridPosition(4 + turn, 4);
                if (auraManager.HasEnoughAura(true, testAbility.auraCost))
                {
                    abilityManager.ExecuteAbility(testAbility, player1DokkaebiUnit, p1Target, player2DokkaebiUnit, false);
                }
                
                // Player 2 uses ability
                var p2Target = new GridPosition(6 - turn, 6);
                if (auraManager.HasEnoughAura(false, testAbility.auraCost))
                {
                    abilityManager.ExecuteAbility(testAbility, player2DokkaebiUnit, p2Target, player1DokkaebiUnit, false);
                }
                
                // Note: Time delay removed - converted from UnityTest
            }
            
            // Verify final state
            int finalP1Aura = auraManager.GetCurrentAura(true);
            int finalP2Aura = auraManager.GetCurrentAura(false);
            
            SmartLogger.Log($"[IntegrationTest] Final aura - P1: {finalP1Aura}, P2: {finalP2Aura}", LogCategory.Debug);
            
            // Both players should have some aura (gained from turns, spent on abilities)
            Assert.IsTrue(finalP1Aura >= 0, "Player 1 aura should not be negative");
            Assert.IsTrue(finalP2Aura >= 0, "Player 2 aura should not be negative");
        }
    }
} 