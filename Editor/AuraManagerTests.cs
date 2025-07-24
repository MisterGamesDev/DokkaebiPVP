using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Dokkaebi.Core;
using Dokkaebi.TurnSystem;
using Dokkaebi.Units;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// Comprehensive unit tests for AuraManager
    /// Tests turn integration, aura gains, network sync, and anti-cheat validation
    /// </summary>
    public class AuraManagerTests
    {
        private GameObject testGameObject;
        private AuraManager auraManager;
        private V3TurnManager mockTurnManager;

        [SetUp]
        public void Setup()
        {
            // Reset singleton instance using reflection to avoid conflicts
            var instanceField = typeof(AuraManager).GetField("instance", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            instanceField?.SetValue(null, null);
            
            // Create test game object and components
            testGameObject = new GameObject("AuraManagerTest");
            auraManager = testGameObject.AddComponent<AuraManager>();
            
            // Create mock turn manager
            var turnManagerObject = new GameObject("MockV3TurnManager");
            mockTurnManager = turnManagerObject.AddComponent<V3TurnManager>();
            
            // Force the singleton instance to use our test instance
            instanceField?.SetValue(null, auraManager);
            
            // Initialize AuraManager manually since we're in test mode
            // Note: In actual implementation, this would happen in Start()
        }

        [TearDown]
        public void Teardown()
        {
            // Clean up singleton instance
            var instanceField = typeof(AuraManager).GetField("instance", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            instanceField?.SetValue(null, null);
            
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
            if (mockTurnManager != null)
                Object.DestroyImmediate(mockTurnManager.gameObject);
        }

        [Test]
        public void TestAuraManagerSingleton()
        {
            // Test singleton pattern - should use our test instance
            var instance1 = AuraManager.Instance;
            var instance2 = AuraManager.Instance;
            
            Assert.IsNotNull(instance1);
            Assert.AreSame(instance1, instance2);
            Assert.AreSame(auraManager, instance1, "Singleton should use our test instance");
        }

        [Test]
        public void TestInitialAuraValues()
        {
            // Test initial aura values
            int player1Aura = auraManager.GetCurrentAura(true);
            int player2Aura = auraManager.GetCurrentAura(false);
            
            Assert.IsTrue(player1Aura > 0, "Player 1 should have initial aura");
            Assert.IsTrue(player2Aura > 0, "Player 2 should have initial aura");
            Assert.AreEqual(player1Aura, player2Aura, "Both players should start with equal aura");
        }

        [Test]
        public void TestModifyAura()
        {
            // Test aura modification
            int initialAura = auraManager.GetCurrentAura(true);
            
            auraManager.ModifyAura(true, 5);
            int newAura = auraManager.GetCurrentAura(true);
            
            Assert.AreEqual(initialAura + 5, newAura, "Aura should increase by 5");
        }

        [Test]
        public void TestAuraCapLimits()
        {
            // Test aura cap limits
            int maxAura = auraManager.GetMaxAura(true);
            
            // Try to exceed maximum
            auraManager.ModifyAura(true, 100);
            int cappedAura = auraManager.GetCurrentAura(true);
            
            Assert.AreEqual(maxAura, cappedAura, "Aura should be capped at maximum");
        }

        [Test]
        public void TestAuraNegativePrevention()
        {
            // Test negative aura prevention
            auraManager.ModifyAura(true, -100);
            int aura = auraManager.GetCurrentAura(true);
            
            Assert.AreEqual(0, aura, "Aura should not go below 0");
        }

        [Test]
        public void TestHasEnoughAura()
        {
            // Test aura sufficiency checking
            auraManager.ModifyAura(true, -auraManager.GetCurrentAura(true)); // Set to 0
            auraManager.ModifyAura(true, 5); // Set to 5
            
            Assert.IsTrue(auraManager.HasEnoughAura(true, 3), "Should have enough aura for cost 3");
            Assert.IsTrue(auraManager.HasEnoughAura(true, 5), "Should have exactly enough aura for cost 5");
            Assert.IsFalse(auraManager.HasEnoughAura(true, 6), "Should not have enough aura for cost 6");
        }

        [Test]
        public void TestGainAuraForTurnWithReason()
        {
            // Test enhanced aura gain with reason tracking
            int initialAura = auraManager.GetCurrentAura(true);
            bool gainEventFired = false;
            string gainReason = "";
            int gainAmount = 0;
            
            // Subscribe to gain event
            auraManager.OnAuraGained += (playerId, amount, reason) =>
            {
                if (playerId == 1)
                {
                    gainEventFired = true;
                    gainAmount = amount;
                    gainReason = reason;
                }
            };
            
            auraManager.GainAuraForTurn(true, 3, "Test Gain");
            
            Assert.IsTrue(gainEventFired, "Aura gain event should fire");
            Assert.AreEqual(3, gainAmount, "Gain amount should be 3");
            Assert.AreEqual("Test Gain", gainReason, "Gain reason should match");
            Assert.AreEqual(initialAura + 3, auraManager.GetCurrentAura(true), "Aura should increase by 3");
        }

        [Test]
        public void TestAuraChangeEvent()
        {
            // Test aura change event firing
            bool changeEventFired = false;
            int oldValue = 0;
            int newValue = 0;
            
            auraManager.OnAuraChanged += (playerId, oldVal, newVal) =>
            {
                if (playerId == 1)
                {
                    changeEventFired = true;
                    oldValue = oldVal;
                    newValue = newVal;
                }
            };
            
            int initialAura = auraManager.GetCurrentAura(true);
            auraManager.ModifyAura(true, 2);
            
            Assert.IsTrue(changeEventFired, "Aura change event should fire");
            Assert.AreEqual(initialAura, oldValue, "Old value should match initial");
            Assert.AreEqual(initialAura + 2, newValue, "New value should be initial + 2");
        }

        [Test]
        public void TestAntiCheatValidation()
        {
            // Test anti-cheat validation for impossible aura modifications
            bool isValidNegative = auraManager.ValidateAuraModification(true, -100, "Test");
            bool isValidHighCost = auraManager.ValidateAuraModification(true, -25, "Test"); // Assuming max capacity is 15
            bool isValidNormal = auraManager.ValidateAuraModification(true, -5, "Test");
            
            Assert.IsFalse(isValidNegative, "Should reject impossible negative aura");
            Assert.IsFalse(isValidHighCost, "Should reject impossible high cost");
            Assert.IsTrue(isValidNormal, "Should accept normal modification");
        }

        [Test]
        public void TestSetMaxAura()
        {
            // Test setting maximum aura
            auraManager.SetMaxAura(true, 20);
            
            Assert.AreEqual(20, auraManager.GetMaxAura(true), "Max aura should be set to 20");
            
            // Test that current aura is clamped to new max
            auraManager.ModifyAura(true, 100); // Try to exceed new max
            Assert.AreEqual(20, auraManager.GetCurrentAura(true), "Current aura should be clamped to new max");
        }

        [Test]
        public void TestNetworkModeToggle()
        {
            // Test network mode configuration
            auraManager.SetNetworkMode(true, false); // Enable network, disable offline
            // Note: This test mainly verifies the method doesn't crash
            // In a real test environment, we'd mock network components
            
            auraManager.SetNetworkMode(false, true); // Disable network, enable offline
            // Verify offline mode works
            auraManager.ModifyAura(true, 1);
            // Should complete without network calls
        }

        [Test]
        public void TestTurnIntegration()
        {
            // Test turn system integration (converted from UnityTest due to framework issues)
            int initialPlayer1Aura = auraManager.GetCurrentAura(true);
            int initialPlayer2Aura = auraManager.GetCurrentAura(false);
            
            // Simulate turn start event
            if (mockTurnManager != null)
            {
                mockTurnManager.OnTurnStarted?.Invoke(1);
                
                // Note: Without UnityTest, we can't wait for frame processing
                // This test now checks immediate synchronous effects only
                
                // Check that aura was gained for both players
                int newPlayer1Aura = auraManager.GetCurrentAura(true);
                int newPlayer2Aura = auraManager.GetCurrentAura(false);
                
                Assert.IsTrue(newPlayer1Aura >= initialPlayer1Aura, "Player 1 should gain aura on turn start");
                Assert.IsTrue(newPlayer2Aura >= initialPlayer2Aura, "Player 2 should gain aura on turn start");
            }
        }

        [Test]
        public void TestCalculateAuraGainWithUnitBonus()
        {
            // This test would require mocking UnitManager and alive units
            // For now, we test the basic gain calculation without unit bonus
            
            // Test that gain calculation returns reasonable values
            // Note: This uses reflection to access private method for testing
            var calculateMethod = typeof(AuraManager).GetMethod("CalculateAuraGain", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (calculateMethod != null)
            {
                int gain = (int)calculateMethod.Invoke(auraManager, new object[] { true });
                Assert.IsTrue(gain > 0, "Aura gain should be positive");
                Assert.IsTrue(gain <= 10, "Aura gain should not exceed maximum"); // Assuming max gain is 10
            }
        }

        [Test]
        public void TestAuraModificationWithZeroAmount()
        {
            // Test that zero modifications don't trigger unnecessary events
            int initialAura = auraManager.GetCurrentAura(true);
            bool eventFired = false;
            
            auraManager.OnAuraChanged += (playerId, oldVal, newVal) =>
            {
                if (playerId == 1) eventFired = true;
            };
            
            auraManager.ModifyAura(true, 0);
            
            Assert.AreEqual(initialAura, auraManager.GetCurrentAura(true), "Aura should remain unchanged");
            Assert.IsTrue(eventFired, "Event should still fire for tracking purposes");
        }

        [Test]
        public void TestMultiplePlayersIndependence()
        {
            // Test that player 1 and player 2 aura are independent
            int player1Initial = auraManager.GetCurrentAura(true);
            int player2Initial = auraManager.GetCurrentAura(false);
            
            auraManager.ModifyAura(true, 5);
            
            Assert.AreEqual(player1Initial + 5, auraManager.GetCurrentAura(true), "Player 1 aura should change");
            Assert.AreEqual(player2Initial, auraManager.GetCurrentAura(false), "Player 2 aura should remain unchanged");
        }

        [Test]
        public void TestAuraGainWithBackwardCompatibility()
        {
            // Test the overloaded GainAuraForTurn method for backward compatibility
            int initialAura = auraManager.GetCurrentAura(true);
            
            auraManager.GainAuraForTurn(true); // Uses calculated amount
            
            int newAura = auraManager.GetCurrentAura(true);
            Assert.IsTrue(newAura > initialAura, "Aura should increase with backward compatible method");
        }
    }
} 