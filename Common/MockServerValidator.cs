using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Core.Data;
using Dokkaebi.Units;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.Common;
using Dokkaebi.Grid;

namespace Dokkaebi.Common.Mocks
{
    /// <summary>
    /// Mock server validator for local testing that simulates PlayFab CloudScript validation
    /// Focuses on basic anti-cheat detection for impossible actions
    /// </summary>
    public static class MockServerValidator
    {
        // Anti-Cheat Settings
        private static int maxAbilityRange = 10; // Maximum possible ability range
        private static int maxMovementRange = 5; // Maximum possible movement range
        private static float maxSubmissionRate = 10.0f; // Max actions per second
        private static bool enableStrictValidation = true;
        
        // Track submission rates for anti-cheat
        private static Dictionary<int, List<float>> playerSubmissionTimes = new Dictionary<int, List<float>>();
        
        /// <summary>
        /// Validate an ability command for impossible actions
        /// </summary>
        public static ValidationResult ValidateAbilityCommand(AbilityData abilityData, DokkaebiUnit sourceUnit, GridPosition targetPosition, DokkaebiUnit targetUnit = null)
        {
            // 1. Basic null checks - MUST be first to prevent NullReferenceException in logging
            if (abilityData == null || sourceUnit == null)
            {
                SmartLogger.LogWarning("[MockServerValidator] Validation failed: null ability data or source unit", LogCategory.Networking);
                return ValidationResult.Fail("INVALID_INPUT", "Null ability data or source unit");
            }
            
            SmartLogger.Log($"[MockServerValidator] Validating ability command: {abilityData.displayName} by {sourceUnit.GetUnitName()}", LogCategory.Networking);
            
            // 2. Unit ownership validation (simulate server-side check)
            int playerId = sourceUnit.IsPlayer() ? 1 : 2;
            if (!ValidateUnitOwnership(sourceUnit, playerId))
            {
                return ValidationResult.Fail("INVALID_OWNERSHIP", "Unit does not belong to player");
            }
            
            // 3. Range validation - ANTI-CHEAT: Impossible range detection
            int distance = GridPosition.GetManhattanDistance(sourceUnit.GetGridPosition(), targetPosition);
            int maxPossibleRange = Mathf.Max(abilityData.range + 2, maxAbilityRange); // Allow small buffer for zones/modifiers
            
            if (distance > maxPossibleRange)
            {
                SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Impossible range detected - Distance: {distance}, Max: {maxPossibleRange}", LogCategory.Networking);
                return ValidationResult.Fail("IMPOSSIBLE_RANGE", $"Target out of possible range ({distance} > {maxPossibleRange})");
            }
            
            // 4. Target validation - ANTI-CHEAT: Invalid targeting
            if (!ValidateTargeting(abilityData, sourceUnit, targetUnit))
            {
                return ValidationResult.Fail("INVALID_TARGET", "Invalid target for ability");
            }
            
            // 5. Resource validation - ANTI-CHEAT: Impossible resources
            if (abilityData.auraCost > 20) // Assume max aura capacity is 20
            {
                SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Impossible aura cost - Cost: {abilityData.auraCost}", LogCategory.Networking);
                return ValidationResult.Fail("IMPOSSIBLE_COST", "Ability cost exceeds maximum possible aura");
            }
            
            // 6. Submission rate validation - ANTI-CHEAT: Rapid-fire detection
            if (!ValidateSubmissionRate(playerId))
            {
                return ValidationResult.Fail("RAPID_SUBMISSION", "Submission rate too high - possible automation");
            }
            
            // 7. Grid bounds validation
            if (!GridManager.Instance.IsValidGridPosition(targetPosition))
            {
                return ValidationResult.Fail("OUT_OF_BOUNDS", "Target position is outside valid grid");
            }
            
            SmartLogger.Log($"[MockServerValidator] Validation passed for {abilityData.displayName}", LogCategory.Networking);
            return ValidationResult.Success("Ability command validated");
        }
        
        /// <summary>
        /// Validate a movement command for impossible actions
        /// </summary>
        public static ValidationResult ValidateMovementCommand(DokkaebiUnit sourceUnit, GridPosition targetPosition)
        {
            // 1. Basic validation - MUST be first to prevent NullReferenceException in logging
            if (sourceUnit == null)
            {
                SmartLogger.LogWarning("[MockServerValidator] Movement validation failed: null source unit", LogCategory.Networking);
                return ValidationResult.Fail("INVALID_INPUT", "Null source unit");
            }
            
            SmartLogger.Log($"[MockServerValidator] Validating movement command: {sourceUnit.GetUnitName()} to {targetPosition}", LogCategory.Networking);
            
            // 2. Range validation - ANTI-CHEAT: Impossible movement distance
            int distance = GridPosition.GetManhattanDistance(sourceUnit.GetGridPosition(), targetPosition);
            if (distance > maxMovementRange)
            {
                SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Impossible movement distance - Distance: {distance}, Max: {maxMovementRange}", LogCategory.Networking);
                return ValidationResult.Fail("IMPOSSIBLE_MOVEMENT", $"Movement distance exceeds maximum ({distance} > {maxMovementRange})");
            }
            
            // 3. Grid bounds validation
            if (!GridManager.Instance.IsValidGridPosition(targetPosition))
            {
                return ValidationResult.Fail("OUT_OF_BOUNDS", "Target position is outside valid grid");
            }
            
            // 4. Occupancy validation
            if (GridManager.Instance.IsTileOccupied(targetPosition))
            {
                return ValidationResult.Fail("POSITION_OCCUPIED", "Target position is occupied");
            }
            
            SmartLogger.Log($"[MockServerValidator] Movement validation passed", LogCategory.Networking);
            return ValidationResult.Success("Movement command validated");
        }
        
        /// <summary>
        /// Validate unit ownership (simulate server-side player verification)
        /// </summary>
        private static bool ValidateUnitOwnership(DokkaebiUnit unit, int playerId)
        {
            // Simple ownership check - in real server this would check against match data
            bool unitBelongsToPlayer = (playerId == 1 && unit.IsPlayer()) || (playerId == 2 && !unit.IsPlayer());
            
            if (!unitBelongsToPlayer)
            {
                SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Unit ownership violation - Player {playerId} trying to control {unit.GetUnitName()}", LogCategory.Networking);
            }
            
            return unitBelongsToPlayer;
        }
        
        /// <summary>
        /// Validate ability targeting rules
        /// </summary>
        private static bool ValidateTargeting(AbilityData abilityData, DokkaebiUnit sourceUnit, DokkaebiUnit targetUnit)
        {
            // Ground targeting abilities don't need unit validation
            if (abilityData.targetsGround)
            {
                return true;
            }
            
            // Unit targeting abilities need valid targets
            if (targetUnit == null && !abilityData.targetsGround)
            {
                SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Missing target unit for unit-targeting ability {abilityData.displayName}", LogCategory.Networking);
                return false;
            }
            
            if (targetUnit != null)
            {
                // Check targeting rules
                bool canTargetSelf = abilityData.targetsSelf && targetUnit == sourceUnit;
                bool canTargetAlly = abilityData.targetsAlly && targetUnit.IsPlayer() == sourceUnit.IsPlayer() && targetUnit != sourceUnit;
                bool canTargetEnemy = abilityData.targetsEnemy && targetUnit.IsPlayer() != sourceUnit.IsPlayer();
                
                if (!canTargetSelf && !canTargetAlly && !canTargetEnemy)
                {
                    SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Invalid targeting - {abilityData.displayName} cannot target {targetUnit.GetUnitName()}", LogCategory.Networking);
                    return false;
                }
                
                // Check if target is alive
                if (!targetUnit.IsAlive)
                {
                    SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Targeting dead unit {targetUnit.GetUnitName()}", LogCategory.Networking);
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validate submission rate to detect automation/rapid-fire
        /// </summary>
        private static bool ValidateSubmissionRate(int playerId)
        {
            if (!enableStrictValidation) return true;
            
            float currentTime = Time.time;
            
            // Initialize tracking for new players
            if (!playerSubmissionTimes.ContainsKey(playerId))
            {
                playerSubmissionTimes[playerId] = new List<float>();
            }
            
            var submissions = playerSubmissionTimes[playerId];
            
            // Add current submission
            submissions.Add(currentTime);
            
            // Remove submissions older than 1 second
            submissions.RemoveAll(time => currentTime - time > 1.0f);
            
            // Check if rate exceeds maximum
            if (submissions.Count > maxSubmissionRate)
            {
                SmartLogger.LogWarning($"[MockServerValidator] ANTI-CHEAT: Rapid submission detected for Player {playerId} - {submissions.Count} submissions in 1 second", LogCategory.Networking);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Simulate server game state update response
        /// </summary>
        public static MockGameStateResponse SimulateGameStateUpdate(string commandType, object commandData)
        {
            // Simulate server processing delay
            float simulatedLatency = Random.Range(0.1f, 0.3f);
            
            SmartLogger.Log($"[MockServerValidator] Simulating {commandType} command processing with {simulatedLatency:F2}s latency", LogCategory.Networking);
            
            // Create mock response
            var response = new MockGameStateResponse
            {
                success = true,
                latency = simulatedLatency,
                timestamp = System.DateTime.UtcNow,
                gameState = new Dictionary<string, object>
                {
                    { "currentTurn", Random.Range(1, 10) },
                    { "currentPhase", "ActionSelection" },
                    { "player1", new Dictionary<string, object> { { "currentAura", Random.Range(5, 15) } } },
                    { "player2", new Dictionary<string, object> { { "currentAura", Random.Range(5, 15) } } }
                }
            };
            
            return response;
        }
        
        /// <summary>
        /// Configure anti-cheat settings for testing
        /// </summary>
        public static void ConfigureAntiCheat(int maxRange = 10, int maxMovement = 5, float maxRate = 10.0f, bool strict = true)
        {
            maxAbilityRange = maxRange;
            maxMovementRange = maxMovement;
            maxSubmissionRate = maxRate;
            enableStrictValidation = strict;
            
            SmartLogger.Log($"[MockServerValidator] Anti-cheat configured - MaxRange: {maxRange}, MaxMovement: {maxMovement}, MaxRate: {maxRate}, Strict: {strict}", LogCategory.Networking);
        }
        
        /// <summary>
        /// Reset submission tracking (for testing)
        /// </summary>
        public static void ResetSubmissionTracking()
        {
            playerSubmissionTimes.Clear();
            SmartLogger.Log("[MockServerValidator] Submission tracking reset", LogCategory.Networking);
        }
    }
    
    /// <summary>
    /// Validation result structure
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorCode { get; private set; }
        public string ErrorMessage { get; private set; }
        
        private ValidationResult(bool isValid, string errorCode, string errorMessage)
        {
            IsValid = isValid;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
        
        public static ValidationResult Success(string message = "Validation passed")
        {
            return new ValidationResult(true, null, message);
        }
        
        public static ValidationResult Fail(string errorCode, string errorMessage)
        {
            return new ValidationResult(false, errorCode, errorMessage);
        }
    }
    
    /// <summary>
    /// Mock game state response structure
    /// </summary>
    public class MockGameStateResponse
    {
        public bool success;
        public float latency;
        public System.DateTime timestamp;
        public Dictionary<string, object> gameState;
        public string errorCode;
        public string errorMessage;
    }
} 