using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Dokkaebi.Interfaces;
using Dokkaebi.Units;
using Dokkaebi.Grid;
using Dokkaebi.Core;
using Dokkaebi.Core.Data;

namespace Dokkaebi.TurnSystem
{
    /// <summary>
    /// Handles the priority-based resolution of simultaneous actions in the V3 turn system.
    /// </summary>
    public class ActionResolutionSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private AbilityManager abilityManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;
        
        /// <summary>
        /// Resolves a list of player actions based on priority and speed stats
        /// </summary>
        public void ResolveActions(List<PlayerAction> actions)
        {
            if (actions == null || actions.Count == 0)
            {
                LogDebug("No actions to resolve");
                return;
            }
            
            LogDebug($"Starting resolution of {actions.Count} actions");
            
            // Step 1: Filter out actions that can't execute
            var validActions = actions.Where(action => action.CanExecute()).ToList();
            var canceledActions = actions.Where(action => !action.CanExecute()).ToList();
            
            // Cancel invalid actions
            foreach (var canceledAction in canceledActions)
            {
                canceledAction.Cancel("Unit cannot execute action (stunned, dead, or invalid)");
                LogDebug($"Canceled action for {canceledAction.actingUnit?.name}: {canceledAction.cancelReason}");
            }
            
            // Step 2: Sort by priority, then by speed (higher speed goes first)
            var sortedActions = validActions
                .OrderBy(action => (int)action.priorityType)
                .ThenByDescending(action => action.actingUnit?.GetSpeed() ?? 0)
                .ThenBy(action => action.actingUnit?.UnitId ?? 0) // Final tiebreaker for deterministic order
                .ToList();
            
            LogDebug($"Action resolution order (Priority -> Speed -> UnitId):");
            for (int i = 0; i < sortedActions.Count; i++)
            {
                var action = sortedActions[i];
                var speed = action.actingUnit?.GetSpeed() ?? 0;
                var unitId = action.actingUnit?.UnitId ?? 0;
                LogDebug($"  {i + 1}. {action.actingUnit?.name} - Priority: {action.priorityType} - Speed: {speed} - UnitId: {unitId}");
            }
            
            // Log speed tiebreaking information
            var priorityGroups = sortedActions.GroupBy(a => a.priorityType);
            foreach (var group in priorityGroups)
            {
                var speedGroups = group.GroupBy(a => a.actingUnit?.GetSpeed() ?? 0);
                foreach (var speedGroup in speedGroups.Where(sg => sg.Count() > 1))
                {
                    var units = string.Join(", ", speedGroup.Select(a => a.actingUnit?.name ?? "Unknown"));
                    LogDebug($"Speed tie at Priority {group.Key}, Speed {speedGroup.Key}: {units} (resolved by UnitId)");
                }
            }
            
            // Step 3: Execute actions in priority order
            foreach (var action in sortedActions)
            {
                if (action.wasCanceled)
                    continue;
                    
                // Re-check if action can still execute (might have been affected by previous actions)
                if (!action.CanExecute())
                {
                    action.Cancel("Action became invalid during resolution");
                    LogDebug($"Action canceled during resolution: {action.actingUnit?.name}");
                    continue;
                }
                
                ExecuteAction(action);
            }
            
            LogDebug("Action resolution complete");
        }
        
        /// <summary>
        /// Executes a single action
        /// </summary>
        private void ExecuteAction(PlayerAction action)
        {
            LogDebug($"Executing {action.actionType} for {action.actingUnit?.name}");
            
            try
            {
                switch (action.actionType)
                {
                    case ActionType.Move:
                        ExecuteMovementAction(action);
                        break;
                        
                    case ActionType.UseAbility:
                        ExecuteAbilityAction(action);
                        break;
                        
                    default:
                        LogDebug($"Unknown action type: {action.actionType}");
                        break;
                }
                
                action.isResolved = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error executing action for {action.actingUnit?.name}: {e.Message}");
                action.Cancel($"Execution error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Executes a movement action
        /// </summary>
        private void ExecuteMovementAction(PlayerAction action)
        {
            if (gridManager == null)
            {
                LogDebug("GridManager not found, cannot execute movement");
                action.Cancel("GridManager not available");
                return;
            }
            
            // Convert Vector2Int to GridPosition
            var targetGridPos = new GridPosition(action.targetPosition.x, action.targetPosition.y);
            
            // Validate target position
            if (!gridManager.IsPositionValid(targetGridPos))
            {
                action.Cancel("Invalid target position");
                return;
            }
            
            // Check if target position is occupied
            if (gridManager.IsPositionOccupied(targetGridPos))
            {
                action.Cancel("Target position is occupied");
                return;
            }
            
            // Execute the movement
            var currentPos = action.actingUnit.GetGridPosition();
            action.actingUnit.SetGridPosition(targetGridPos);
            
            // Update grid manager - clear old position and set new occupant
            gridManager.ClearUnitFromPreviousTile(action.actingUnit);
            gridManager.SetTileOccupant(targetGridPos, action.actingUnit);
            
            LogDebug($"Moved {action.actingUnit.name} from {currentPos} to {action.targetPosition}");
        }
        
        /// <summary>
        /// Executes an ability action
        /// </summary>
        private void ExecuteAbilityAction(PlayerAction action)
        {
            if (abilityManager == null)
            {
                LogDebug("AbilityManager not found, cannot execute ability");
                action.Cancel("AbilityManager not available");
                return;
            }
            
            if (action.abilityData == null)
            {
                action.Cancel("No ability data provided");
                return;
            }
            
            // Check if unit has enough resources to use the ability
            if (!CanUseAbility(action.actingUnit, action.abilityData))
            {
                action.Cancel("Insufficient resources to use ability");
                return;
            }
            
            // Convert Vector2Int to GridPosition for target
            var targetGridPos = new GridPosition(action.targetPosition.x, action.targetPosition.y);
            
            // Check if this is an overload cast
            bool isOverload = action.actingUnit.IsInOverload();
            
            // Execute the ability
            var success = abilityManager.ExecuteAbility(
                action.abilityData,
                action.actingUnit, 
                targetGridPos,
                action.targetUnit,
                isOverload
            );
            
            if (!success)
            {
                action.Cancel("Ability execution failed");
                return;
            }
            
            LogDebug($"Executed ability {action.abilityData.displayName} for {action.actingUnit.name}");
        }
        
        /// <summary>
        /// Checks if a unit can use a specific ability
        /// </summary>
        private bool CanUseAbility(DokkaebiUnit unit, AbilityData ability)
        {
            // Check aura cost
            if (unit.GetCurrentAura() < ability.auraCost)
            {
                LogDebug($"Unit {unit.name} cannot use {ability.displayName}: insufficient aura ({unit.GetCurrentAura()}/{ability.auraCost})");
                return false;
            }
                
            // Check cooldown
            if (unit.IsAbilityOnCooldown(ability.abilityId))
            {
                int remainingCooldown = unit.GetRemainingCooldown(ability.abilityId);
                LogDebug($"Unit {unit.name} cannot use {ability.displayName}: on cooldown ({remainingCooldown} turns remaining)");
                return false;
            }
                
            // Check overload requirement
            if (ability.requiresOverload && !unit.IsInOverload())
            {
                LogDebug($"Unit {unit.name} cannot use {ability.displayName}: requires overload state");
                return false;
            }
                
            return true;
        }
        
        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[ActionResolutionSystem] {message}");
            }
        }
        
        /// <summary>
        /// Initialize dependencies
        /// </summary>
        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
                
            if (abilityManager == null)
                abilityManager = FindFirstObjectByType<AbilityManager>();
        }
    }
} 