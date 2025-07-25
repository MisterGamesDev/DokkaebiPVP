using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Dokkaebi.Common;
using Dokkaebi.Core;
using Dokkaebi.Core.Data;
using Dokkaebi.Grid;
using Dokkaebi.Interfaces;
using Dokkaebi.Units;
using Dokkaebi.Utilities;
using Dokkaebi.Pathfinding;
using Dokkaebi.UI;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Manages unit spawning, tracking, and lifecycle.
    /// </summary>
    public class UnitManager : MonoBehaviour
    {
        // Singleton instance
        public static UnitManager Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject baseUnitPrefab;

        [Header("Unit Definitions")]
        [SerializeField] private List<UnitDefinitionData> unitDefinitions;

        // Runtime data
        private Dictionary<int, DokkaebiUnit> activeUnits = new Dictionary<int, DokkaebiUnit>();
        private Dictionary<GridPosition, IDokkaebiUnit> unitPositions = new Dictionary<GridPosition, IDokkaebiUnit>();
        private int nextUnitId = 1;
        private DokkaebiUnit selectedUnit;

        private void Awake()
        {
            // Singleton pattern setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                SmartLogger.LogWarning("Multiple UnitManager instances detected. Destroying duplicate.", LogCategory.Unit);
                Destroy(gameObject);
                return;
            }

            if (baseUnitPrefab == null)
            {
                SmartLogger.LogError("Base unit prefab not assigned to UnitManager!", LogCategory.Unit);
                return;
            }
        }

        /// <summary>
        /// Spawns all units defined in the UnitSpawnData configuration
        /// </summary>
        public void SpawnUnitsFromConfiguration()
        {
            SmartLogger.Log("[UnitManager.SpawnUnitsFromConfiguration] Starting unit spawning...", LogCategory.Unit);
            var spawnData = DataManager.Instance.GetUnitSpawnData();
            if (spawnData == null) {
                SmartLogger.LogError("[UnitManager.SpawnUnitsFromConfiguration] Failed to get UnitSpawnData from DataManager!", LogCategory.Unit);
                return;
            }
            SmartLogger.Log($"[UnitManager.SpawnUnitsFromConfiguration] Found UnitSpawnData. Player Spawns: {spawnData.playerUnitSpawns.Count}, Enemy Spawns: {spawnData.enemyUnitSpawns.Count}", LogCategory.Unit);

            // Spawn player units
            foreach (var spawnConfig in spawnData.playerUnitSpawns)
            {
                var unitDefinition = spawnConfig.unitDefinition;
                if (unitDefinition != null)
                {
                    SmartLogger.Log($"[UnitManager.SpawnUnitsFromConfiguration] Spawning player unit {unitDefinition.displayName} at grid position {spawnConfig.spawnPosition}", LogCategory.Unit);
                    SpawnAndRegisterUnit(unitDefinition, GridPosition.FromVector2Int(spawnConfig.spawnPosition), 1); // TeamId 1 for player
                }
                else
                {
                    SmartLogger.LogError("[UnitManager.SpawnUnitsFromConfiguration] Player unit spawn configuration is missing unit definition!", LogCategory.Unit);
                }
            }

            // Spawn enemy units
            foreach (var spawnConfig in spawnData.enemyUnitSpawns)
            {
                var unitDefinition = spawnConfig.unitDefinition;
                if (unitDefinition != null)
                {
                    SmartLogger.Log($"[UnitManager.SpawnUnitsFromConfiguration] Spawning enemy unit {unitDefinition.displayName} at grid position {spawnConfig.spawnPosition}", LogCategory.Unit);
                    SpawnAndRegisterUnit(unitDefinition, GridPosition.FromVector2Int(spawnConfig.spawnPosition), 2); // TeamId 2 for enemy
                }
                else
                {
                    SmartLogger.LogError("[UnitManager.SpawnUnitsFromConfiguration] Enemy unit spawn configuration is missing unit definition!", LogCategory.Unit);
                }
            }

            SmartLogger.Log($"[UnitManager.SpawnUnitsFromConfiguration] Completed spawning. Active units count: {activeUnits.Count}", LogCategory.Unit);
        }

        /// <summary>
        /// Spawn a unit from a definition
        /// </summary>
        public DokkaebiUnit SpawnUnit(UnitDefinitionData definition, Vector2Int spawnPosition, bool isPlayerUnit)
        {
            return SpawnUnit(definition, GridPosition.FromVector2Int(spawnPosition), isPlayerUnit);
        }

        /// <summary>
        /// Handle unit movement event to update position tracking
        /// </summary>
        private void HandleUnitMoved(IDokkaebiUnit unit, GridPosition oldPos, GridPosition newPos)
        {
            UpdateUnitPositionInDictionary(unit, oldPos, newPos);
        }

        /// <summary>
        /// Spawn a unit from a definition at a grid position
        /// </summary>
        public DokkaebiUnit SpawnUnit(UnitDefinitionData definition, GridPosition gridPosition, bool isPlayerUnit)
        {
            SmartLogger.Log($"[UnitManager.SpawnUnit] Starting spawn for {definition.displayName} at {gridPosition} (Player: {isPlayerUnit})", LogCategory.Unit);
            
            if (GridManager.Instance == null)
            {
                SmartLogger.LogError("[UnitManager.SpawnUnit] GridManager.Instance is null!", LogCategory.Unit);
                return null;
            }

            // Log which prefab we're using
            GameObject prefabToUse = definition.unitPrefab ?? baseUnitPrefab;
            SmartLogger.Log($"[UnitManager.SpawnUnit] Using prefab: {(definition.unitPrefab != null ? "definition.unitPrefab" : "baseUnitPrefab")}", LogCategory.Unit);
            
            // Check for UnitStatusUI on the prefab
            var uiComponentsOnPrefab = prefabToUse.GetComponentsInChildren<UnitStatusUI>();
            SmartLogger.Log($"[UnitManager.SpawnUnit] Prefab has {uiComponentsOnPrefab.Length} UnitStatusUI components", LogCategory.Unit);
            foreach (var ui in uiComponentsOnPrefab)
            {
                SmartLogger.Log($"[UnitManager.SpawnUnit] Found UnitStatusUI on prefab: Path={GetGameObjectPath(ui.gameObject)}", LogCategory.Unit);
            }

            // Get world position from grid position
            Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(gridPosition);
            SmartLogger.Log($"[UnitManager.SpawnUnit] Converting grid position {gridPosition} to world position {worldPosition}", LogCategory.Unit);

            // Instantiate the unit
            GameObject unitObject = Instantiate(prefabToUse, worldPosition, Quaternion.identity);
            if (unitObject == null)
            {
                SmartLogger.LogError($"[UnitManager.SpawnUnit] Failed to instantiate unit prefab for {definition.displayName}", LogCategory.Unit);
                return null;
            }
            SmartLogger.Log($"[UnitManager.SpawnUnit] Successfully instantiated unit GameObject", LogCategory.Unit);

            // Rotate enemy units by 180 degrees if they are not player units
            if (!isPlayerUnit)
            {
                // Create a rotation of 180 degrees around the Y-axis
                Quaternion rotate180 = Quaternion.Euler(0, 180, 0);

                // Apply the rotation to the instantiated unit object
                // Using *= applies the rotation relative to the object's current rotation.
                unitObject.transform.rotation *= rotate180;

                SmartLogger.Log($"[UnitManager.SpawnUnit] Rotated enemy unit {definition.displayName} by 180 degrees.", LogCategory.Unit);
            }

            // Check for UnitStatusUI components on the instantiated object
            var uiComponents = unitObject.GetComponentsInChildren<UnitStatusUI>();
            SmartLogger.Log($"[UnitManager.SpawnUnit] Instantiated object has {uiComponents.Length} UnitStatusUI components", LogCategory.Unit);
            foreach (var ui in uiComponents)
            {
                SmartLogger.Log($"[UnitManager.SpawnUnit] Found UnitStatusUI on instance: Path={GetGameObjectPath(ui.gameObject)}", LogCategory.Unit);
            }

            // Get and configure the DokkaebiUnit component
            DokkaebiUnit unit = unitObject.GetComponent<DokkaebiUnit>();
            if (unit == null)
            {
                SmartLogger.LogError($"[UnitManager.SpawnUnit] DokkaebiUnit component missing on instantiated prefab for {definition.displayName}", LogCategory.Unit);
                Destroy(unitObject);
                return null;
            }
            SmartLogger.Log($"[UnitManager.SpawnUnit] Found DokkaebiUnit component, configuring unit...", LogCategory.Unit);

            // Configure the unit
            ConfigureUnit(unit, definition, isPlayerUnit);
            SmartLogger.Log($"[UnitManager.SpawnUnit] Unit configured with definition {definition.displayName}", LogCategory.Unit);

            // *** Explicitly set the logical grid position after configuration ***
            unit.SetGridPosition(gridPosition);
            SmartLogger.Log($"[UnitManager.SpawnUnit] Explicitly called unit.SetGridPosition({gridPosition})", LogCategory.Unit);

            // Register with GridManager
            GridManager.Instance.SetTileOccupant(gridPosition, unit);
            SmartLogger.Log($"[UnitManager.SpawnUnit] Unit registered with GridManager at {gridPosition}", LogCategory.Unit);

            // Add to active units list
            RegisterUnit(unit);
            SmartLogger.Log($"[UnitManager.SpawnUnit] Unit added to active units list. Total active units: {activeUnits.Count}", LogCategory.Unit);

            // Initialize unit position in tracking dictionary
            unitPositions[gridPosition] = unit;
            SmartLogger.Log($"[UnitManager.SpawnUnit] Unit position initialized in tracking dictionary at {gridPosition}", LogCategory.Unit);

            // Subscribe to unit events
            var capturedUnit = unit; // Capture the unit reference
            unit.OnUnitDefeated += () => HandleUnitDefeat(capturedUnit);
            unit.OnUnitMoved += HandleUnitMoved;
            SmartLogger.Log($"[UnitManager.SpawnUnit] Subscribed to unit events", LogCategory.Unit);

            Vector3 finalWorldPos = unitObject.transform.position;
            return unit;
        }

        private void ConfigureUnit(DokkaebiUnit unit, UnitDefinitionData definition, bool isPlayerUnit)
        {
            // Set the UnitDefinitionData first
            unit.SetUnitDefinitionData(definition);

            // Set basic properties
            unit.SetUnitName(definition.displayName);
            unit.SetIsPlayerUnit(isPlayerUnit);

            // Set stats
            unit.SetMaxHealth(definition.baseHealth);
            unit.SetCurrentHealth(definition.baseHealth);
            SmartLogger.Log($"[UnitManager.ConfigureUnit] Unit {definition.displayName} (ID: {unit.GetUnitId()}) configured with health values - Max: {definition.baseHealth}, Current: {unit.GetCurrentHealth()}", LogCategory.Unit);
            
            unit.SetMaxAura(definition.baseAura);
            AuraManager.Instance.SetMaxAura(isPlayerUnit, definition.baseAura);
            AuraManager.Instance.ModifyAura(isPlayerUnit, definition.baseAura);
            unit.SetMovementRange(definition.baseMovement);

            // Set identity
            unit.SetOrigin(definition.origin);
            unit.SetCalling(definition.calling);

            // Set abilities
            unit.SetAbilities(definition.abilities);
            // --- LOG: Trace movementType for FlameLunge when assigned ---
            foreach (var ability in unit.GetAbilities())
            {
                if (ability != null && ability.abilityId == "FlameLunge")
                {
                    SmartLogger.Log($"[UnitManager.ConfigureUnit] Configured unit {unit.GetUnitName()}. FlameLunge ability assigned. Movement Type: {ability.movementType}", LogCategory.Unit, this);
                    break;
                }
            }

            // --- TEAM ID ASSIGNMENT FIX ---
            int assignedTeamId = isPlayerUnit ? 1 : 2;
            var teamIdField = typeof(DokkaebiUnit).GetField("teamId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (teamIdField != null)
            {
                teamIdField.SetValue(unit, assignedTeamId);
                SmartLogger.Log($"[UnitManager.ConfigureUnit] Assigned TeamId={assignedTeamId} to unit {unit.GetUnitName()} (isPlayerUnit={isPlayerUnit}), Ref={unit.GetHashCode()}", LogCategory.Unit, this);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitManager.ConfigureUnit] Reflection failed to set TeamId for unit {unit.GetUnitName()} (Ref={unit.GetHashCode()})", LogCategory.Unit, this);
            }
        }

        /// <summary>
        /// Registers a unit, assigns a unique UnitId, and adds to activeUnits. Always use this for registration.
        /// </summary>
        public void RegisterUnit(DokkaebiUnit unit)
        {
            SmartLogger.Log($"[UnitManager.RegisterUnit] ENTRY: Attempting to register unit Ref={unit?.GetHashCode() ?? 0}, ID={unit?.UnitId ?? -1}, TeamId={unit?.TeamId ?? -1}.", LogCategory.Unit, this);
            if (unit == null)
            {
                SmartLogger.Log("[UnitManager.RegisterUnit] Unit is null, cannot register.", LogCategory.Unit, this);
                return;
            }
            if (unit.UnitId == -1)
            {
                SmartLogger.LogWarning($"[UnitManager.RegisterUnit] WARNING: Registering unit with ID=-1! Ref={unit.GetHashCode()}", LogCategory.Unit, this);
            }
            if (unit.TeamId == 0)
            {
                SmartLogger.LogWarning($"[UnitManager.RegisterUnit] WARNING: Registering unit with TeamId=0! Ref={unit.GetHashCode()}", LogCategory.Unit, this);
            }
            SmartLogger.Log($"[UnitManager.RegisterUnit] Before adding unit Ref={unit.GetHashCode()} to activeUnits. Current count: {activeUnits.Count}", LogCategory.Unit, this);
            int unitId = nextUnitId++;
            unit.SetUnitId(unitId);
            if (activeUnits.ContainsKey(unitId))
            {
                SmartLogger.LogWarning($"[UnitManager.RegisterUnit] Duplicate UnitId {unitId} detected! Skipping registration.", LogCategory.Unit, this);
                return;
            }
            activeUnits.Add(unitId, unit);
            SmartLogger.Log($"[UnitManager.RegisterUnit] After adding unit Ref={unit.GetHashCode()} to activeUnits. New count: {activeUnits.Count}", LogCategory.Unit, this);
            SmartLogger.Log($"[UnitManager.RegisterUnit] Successfully registered unit Ref={unit.GetHashCode()}, ID={unit.UnitId}, TeamId={unit.TeamId}.", LogCategory.Unit, this);
        }

        public DokkaebiUnit GetUnitById(int unitId)
        {
            return activeUnits.TryGetValue(unitId, out var unit) ? unit : null;
        }

        public List<DokkaebiUnit> GetUnitsByPlayer(bool isPlayerUnit)
        {
            return new List<DokkaebiUnit>(activeUnits.Values).FindAll(u => u.IsPlayer() == isPlayerUnit);
        }

        public List<DokkaebiUnit> GetUnitsAtPosition(Vector2Int positionVector)
        {
            // Convert Vector2Int to GridPosition using the proper utility
            GridPosition position = Dokkaebi.Interfaces.GridPosition.FromVector2Int(positionVector);
            return new List<DokkaebiUnit>(activeUnits.Values).FindAll(u => u.GetGridPosition() == position);
        }

        /// <summary>
        /// Get a single unit at the specified grid position (returns first found if multiple exist)
        /// </summary>
        public DokkaebiUnit GetUnitAtPosition(GridPosition position)
        {
            if (unitPositions.TryGetValue(position, out IDokkaebiUnit unit))
            {
                return unit as DokkaebiUnit;
            }
            return null;
        }

        public void RemoveUnit(DokkaebiUnit unit)
        {
            if (unit != null && activeUnits.ContainsKey(unit.GetUnitId()))
            {
                // Remove from active units
                activeUnits.Remove(unit.GetUnitId());

                // Remove from position tracking
                var position = unit.GetGridPosition();
                if (unitPositions.TryGetValue(position, out IDokkaebiUnit unitAtPos) && (unitAtPos?.UnitId ?? -1) == unit.UnitId)
                {
                    unitPositions.Remove(position);
                    SmartLogger.Log($"Removed unit {unit.GetUnitName()} from position tracking at {position}", LogCategory.Unit);
                }

                Destroy(unit.gameObject);
            }
        }

        public void RemoveAllUnits()
        {
            foreach (var unit in activeUnits.Values)
            {
                if (unit != null)
                {
                    Destroy(unit.gameObject);
                }
            }
            activeUnits.Clear();
            unitPositions.Clear(); // Clear position tracking
            nextUnitId = 1;
        }

        /// <summary>
        /// Unregisters a unit from the manager without destroying it
        /// </summary>
        public void UnregisterUnit(DokkaebiUnit unit)
        {
            if (unit != null && activeUnits.ContainsKey(unit.GetUnitId()))
            {
                activeUnits.Remove(unit.GetUnitId());
                SmartLogger.Log($"Unit {unit.GetUnitName()} unregistered from UnitManager", LogCategory.Unit);
            }
        }

        #region Turn Management
        /// <summary>
        /// Apply start of turn effects to all player units
        /// </summary>
        public void StartPlayerTurn()
        {
            var playerUnits = GetUnitsByPlayer(true);
            SmartLogger.Log($"[UnitManager.StartPlayerTurn] Starting cooldown reduction for {playerUnits.Count} player units.", LogCategory.TurnSystem);
            
            foreach (var unit in playerUnits)
            {
                SmartLogger.Log($"[UnitManager.StartPlayerTurn] Calling UpdateCooldowns for unit: {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.TurnSystem);
                unit.ResetMP();
                unit.ReduceCooldowns();
                StatusEffectSystem.ProcessTurnEndForUnit(unit);
            }
            
            SmartLogger.Log("Player turn started - processed effects for " + playerUnits.Count + " units", LogCategory.Unit);
        }
        
        /// <summary>
        /// Apply start of turn effects to all enemy units
        /// </summary>
        public void StartEnemyTurn()
        {
            var enemyUnits = GetUnitsByPlayer(false);
            SmartLogger.Log($"[UnitManager.StartEnemyTurn] Starting cooldown reduction for {enemyUnits.Count} enemy units.", LogCategory.TurnSystem);
            
            foreach (var unit in enemyUnits)
            {
                SmartLogger.Log($"[UnitManager.StartEnemyTurn] Calling UpdateCooldowns for unit: {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.TurnSystem);
                unit.ResetMP();
                unit.ReduceCooldowns();
                StatusEffectSystem.ProcessTurnEndForUnit(unit);
            }
            
            SmartLogger.Log("Enemy turn started - processed effects for " + enemyUnits.Count + " units", LogCategory.Unit);
        }
        
        /// <summary>
        /// Apply end of turn effects to all player units
        /// </summary>
        public void EndPlayerTurn()
        {
            var playerUnits = GetUnitsByPlayer(true);
            foreach (var unit in playerUnits)
            {
                unit.EndTurn();
            }
            
            SmartLogger.Log("Player turn ended - processed " + playerUnits.Count + " units", LogCategory.Unit);
        }
        
        /// <summary>
        /// Apply end of turn effects to all enemy units
        /// </summary>
        public void EndEnemyTurn()
        {
            var enemyUnits = GetUnitsByPlayer(false);
            foreach (var unit in enemyUnits)
            {
                unit.EndTurn();
            }
            
            SmartLogger.Log("Enemy turn ended - processed " + enemyUnits.Count + " units", LogCategory.Unit);
        }
        #endregion
        
        #region Unit State Queries
        /// <summary>
        /// Get all units that are still alive
        /// </summary>
        public List<DokkaebiUnit> GetAliveUnits()
        {
            return new List<DokkaebiUnit>(activeUnits.Values).FindAll(u => u.IsAlive);
        }
        
        /// <summary>
        /// Get all alive units belonging to a specific player
        /// </summary>
        public List<DokkaebiUnit> GetAliveUnitsByPlayer(bool isPlayerUnit)
        {
            return GetUnitsByPlayer(isPlayerUnit).FindAll(u => u.IsAlive);
        }
        
        /// <summary>
        /// Get all units within a certain range of a position
        /// </summary>
        public List<DokkaebiUnit> GetUnitsInRange(GridPosition center, int range)
        {
            List<DokkaebiUnit> unitsInRange = new List<DokkaebiUnit>();
            
            foreach (var unit in activeUnits.Values)
            {
                if (unit.IsAlive)
                {
                    int distance = GridPosition.GetManhattanDistance(center, unit.GetGridPosition());
                    if (distance <= range)
                    {
                        unitsInRange.Add(unit);
                    }
                }
            }
            
            return unitsInRange;
        }
        
        /// <summary>
        /// Check if any unit has movement points left
        /// </summary>
        public bool AnyUnitsHaveRemainingMP(bool isPlayerUnit)
        {
            var units = GetAliveUnitsByPlayer(isPlayerUnit);
            foreach (var unit in units)
            {
                if (unit.GetCurrentMP() > 0)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Unit State Management
        
        /// <summary>
        /// Reset action states for all units or for a specific player's units
        /// </summary>
        public void ResetActionStates(bool isPlayer = false)
        {
            var units = isPlayer ? GetUnitsByPlayer(isPlayer) : new List<DokkaebiUnit>(activeUnits.Values);
            
            foreach (var unit in units)
            {
                unit.ResetActionState();
            }
            
            SmartLogger.Log($"Reset action states for {(isPlayer ? "player" : "all")} units", LogCategory.Unit);
        }
        
        /// <summary>
        /// Set units interactable state based on player type
        /// </summary>
        public void SetUnitsInteractable(bool isPlayer, bool interactable)
        {
            var units = GetUnitsByPlayer(isPlayer);
            foreach (var unit in units)
            {
                unit.SetInteractable(interactable);
            }
            SmartLogger.Log($"Set {units.Count} {(isPlayer ? "player" : "enemy")} units interactable: {interactable}", LogCategory.Unit);
        }
        
        /// <summary>
        /// Plan AI movement for non-player units
        /// </summary>
        public void PlanAIMovements()
        {
            var aiUnits = GetUnitsByPlayer(false);
            
            foreach (var unit in aiUnits)
            {
                // Basic AI logic - move towards nearest player unit if in range
                var playerUnits = GetUnitsByPlayer(true);
                if (playerUnits.Count > 0)
                {
                    // Find closest player unit
                    DokkaebiUnit closestUnit = null;
                    int minDistance = int.MaxValue;
                    
                    foreach (var playerUnit in playerUnits)
                    {
                        int distance = GridPosition.GetManhattanDistance(
                            unit.GetGridPosition(), 
                            playerUnit.GetGridPosition()
                        );
                        
                        if (distance < minDistance)
                        {
                            closestUnit = playerUnit;
                            minDistance = distance;
                        }
                    }
                    
                    // If found a player unit, try to move towards it
                    if (closestUnit != null)
                    {
                        // Get all possible move positions
                        var validMoves = unit.GetValidMovePositions();
                        
                        if (validMoves.Count > 0)
                        {
                            // Find move that gets us closest to target
                            GridPosition bestMove = unit.GetGridPosition();
                            int bestDistance = minDistance;
                            
                            foreach (var move in validMoves)
                            {
                                int dist = GridPosition.GetManhattanDistance(
                                    move, 
                                    closestUnit.GetGridPosition()
                                );
                                
                                if (dist < bestDistance)
                                {
                                    bestMove = move;
                                    bestDistance = dist;
                                }
                            }
                            
                            // Set the target position
                            if (bestDistance < minDistance)
                            {
                                unit.SetTargetPosition(bestMove);
                                SmartLogger.Log($"AI unit {unit.GetUnitName()} planning move to {bestMove}", LogCategory.Unit);
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Get all units with pending movement
        /// </summary>
        public List<DokkaebiUnit> GetUnitsWithPendingMovement()
        {
            List<DokkaebiUnit> unitsWithMovement = new List<DokkaebiUnit>();
            
            foreach (var unit in activeUnits.Values)
            {
                if (unit.HasPendingMovement)
                {
                    unitsWithMovement.Add(unit);
                }
            }
            
            return unitsWithMovement;
        }
        
        /// <summary>
        /// Update all unit grid positions after movement
        /// </summary>
        public void UpdateAllUnitGridPositions()
        {
            foreach (var unit in activeUnits.Values)
            {
                // Update the grid position based on world position
                GridPosition gridPos = GridManager.Instance.WorldToGrid(unit.transform.position);
                unit.UpdateGridPosition(gridPos);
            }
        }
        
        /// <summary>
        /// Process status effects for all units
        /// </summary>
        public void ProcessStatusEffects()
        {
            foreach (var unit in activeUnits.Values)
            {
                StatusEffectSystem.ProcessTurnEndForUnit(unit);
            }
        }
        
        /// <summary>
        /// Update cooldowns for all units
        /// </summary>
        public void UpdateCooldowns(bool isPlayerTurn)
        {
            var units = GetUnitsByPlayer(isPlayerTurn);
            foreach (var unit in units)
            {
                unit.UpdateCooldowns();
            }
        }
        
        /// <summary>
        /// Check if a player has any units remaining
        /// </summary>
        public bool HasRemainingUnits(bool isPlayer)
        {
            var units = GetUnitsByPlayer(isPlayer);
            return units.Count > 0 && units.Exists(u => u.IsAlive);
        }
        
        /// <summary>
        /// Check if all units of a player are ready
        /// </summary>
        public bool AreAllUnitsReady(bool isPlayer)
        {
            var units = GetUnitsByPlayer(isPlayer);
            
            foreach (var unit in units)
            {
                if (!unit.IsReady())
                {
                    return false;
                }
            }
            
            return true;
        }
        
        #endregion

        /// <summary>
        /// Update a unit's position in the tracking dictionary
        /// </summary>
        public void UpdateUnitPositionInDictionary(IDokkaebiUnit unit, GridPosition oldPos, GridPosition newPos)
        {
            if (unit == null) return;
            
            // Remove unit from old position in UnitManager's dictionary
            if (unitPositions.TryGetValue(oldPos, out IDokkaebiUnit unitAtOldPos) && unitAtOldPos == unit)
            {
                unitPositions.Remove(oldPos);
                SmartLogger.Log($"Removed unit {unit.DisplayName} from internal dictionary at {oldPos}", LogCategory.Unit);
            }

            // Add/update unit at the new position in UnitManager's dictionary
            unitPositions[newPos] = unit;
            SmartLogger.Log($"Added/Updated unit {unit.DisplayName} in internal dictionary at {newPos}", LogCategory.Unit);
            
            SmartLogger.Log($"Updated unit {unit.DisplayName} position in dictionary from {oldPos} to {newPos}", LogCategory.Unit);
        }

        /// <summary>
        /// Process a unit being defeated and remove it from tracking
        /// </summary>
        public void HandleUnitDefeat(DokkaebiUnit unit)
        {
            if (unit == null) return;
            
            SmartLogger.Log($"Unit {unit.GetUnitName()} has been defeated", LogCategory.Unit);
            
            // Trigger the unit defeated event for other systems
            OnUnitDefeated?.Invoke(unit);
            
            // Remove from active units
            if (activeUnits.ContainsKey(unit.GetUnitId()))
            {
                activeUnits.Remove(unit.GetUnitId());
            }

            // Remove from position tracking
            var position = unit.GetGridPosition();
            if (unitPositions.TryGetValue(position, out IDokkaebiUnit unitAtPos) && (unitAtPos?.UnitId ?? -1) == unit.UnitId)
            {
                unitPositions.Remove(position);
                SmartLogger.Log($"Removed defeated unit {unit.GetUnitName()} from position tracking at {position}", LogCategory.Unit);
            }
        }

        /// <summary>
        /// Event triggered when a unit is defeated
        /// </summary>
        public event System.Action<DokkaebiUnit> OnUnitDefeated;

        /// <summary>
        /// Get the currently selected unit
        /// </summary>
        public DokkaebiUnit GetSelectedUnit()
        {
            // Add diagnostic log (commented out to avoid spam, uncomment for debugging)
            // Debug.Log($"[UnitManager GetSelectedUnit] Returning: {(selectedUnit != null ? selectedUnit.GetUnitName() : "NULL")}");
            return selectedUnit;
        }

        /// <summary>
        /// Set the currently selected unit
        /// </summary>
        public void SetSelectedUnit(DokkaebiUnit unitToSelect)
        {
            SmartLogger.Log($"[UnitManager] SetSelectedUnit called. Attempting to select: {(unitToSelect != null ? unitToSelect.GetUnitName() : "NULL")}", LogCategory.Unit);
            selectedUnit = unitToSelect;
            SmartLogger.Log($"[UnitManager] selectedUnit field is now: {(selectedUnit != null ? selectedUnit.GetUnitName() : "NULL")}", LogCategory.Unit);
        }

        /// <summary>
        /// Clear the currently selected unit
        /// </summary>
        public void ClearSelectedUnit()
        {
            SmartLogger.Log("[UnitManager] ClearSelectedUnit called. Clearing selection.", LogCategory.Unit);
            selectedUnit = null;
            SmartLogger.Log("[UnitManager] Selection cleared.", LogCategory.Unit);
        }

        /// <summary>
        /// Unsubscribe from unit events and clean up
        /// </summary>
        private void OnDestroy()
        {
            // Unsubscribe from all unit events
            foreach (var unit in activeUnits.Values)
            {
                if (unit != null)
                {
                    unit.OnUnitDefeated -= () => HandleUnitDefeat(unit);
                    unit.OnUnitMoved -= HandleUnitMoved;
                }
            }
        }

        /// <summary>
        /// Get a read-only view of the unit positions dictionary for debugging
        /// </summary>
        public System.Collections.Generic.IReadOnlyDictionary<GridPosition, IDokkaebiUnit> GetUnitPositionsReadOnly()
        {
            return new System.Collections.ObjectModel.ReadOnlyDictionary<GridPosition, IDokkaebiUnit>(this.unitPositions);
        }

        // Helper method to get full GameObject path
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = obj.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Returns all units currently managed by the UnitManager (no filtering).
        /// </summary>
        public List<DokkaebiUnit> GetAllUnits()
        {
            SmartLogger.Log("[UnitManager.GetAllUnits] ENTRY: Method called.", LogCategory.AI, this);
            var allUnits = new List<DokkaebiUnit>(activeUnits.Values);
            SmartLogger.Log($"[UnitManager.GetAllUnits] Processing internal list. Total units in internal list: {allUnits.Count}", LogCategory.AI, this);
            foreach (var unit in allUnits)
            {
                SmartLogger.Log($"[UnitManager.GetAllUnits] Considering unit from internal list: ID={unit.UnitId}, Name={unit.DisplayName}, TeamId={unit.TeamId}, IsAlive={unit.IsAlive}", LogCategory.AI, this);
            }
            SmartLogger.Log($"[UnitManager.GetAllUnits] Method finished. Returning {allUnits.Count} units.", LogCategory.AI, this);
            return allUnits;
        }

        /// <summary>
        /// Spawns, configures, assigns TeamId, and registers a unit. Use for all player/enemy units.
        /// </summary>
        public DokkaebiUnit SpawnAndRegisterUnit(UnitDefinitionData definition, GridPosition gridPosition, int teamId)
        {
            SmartLogger.Log($"[SPAWN_AND_REGISTER_UNIT] ENTRY for {definition?.displayName ?? "NULL Definition"}, TeamId={teamId}.", LogCategory.Unit, this);
            GameObject prefabToUse = definition.unitPrefab ?? baseUnitPrefab;
            Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(gridPosition);
            GameObject unitObject = Instantiate(prefabToUse, worldPosition, Quaternion.identity);
            SmartLogger.Log($"[SPAWN_AND_REGISTER_UNIT] Instantiated unit GameObject. Ref={unitObject?.GetHashCode() ?? 0}, Prefab={prefabToUse?.name ?? "NULL"}", LogCategory.Unit, this);
            if (unitObject == null)
            {
                SmartLogger.LogError($"[UnitManager.SpawnAndRegisterUnit] Failed to instantiate unit prefab for {definition.displayName}", LogCategory.Unit);
                return null;
            }
            if (teamId == 2) // Enemy
            {
                unitObject.transform.rotation *= Quaternion.Euler(0, 180, 0);
            }
            DokkaebiUnit unit = unitObject.GetComponent<DokkaebiUnit>();
            if (unit == null)
            {
                SmartLogger.LogError($"[UnitManager.SpawnAndRegisterUnit] DokkaebiUnit component missing on instantiated prefab for {definition.displayName}", LogCategory.Unit);
                Destroy(unitObject);
                return null;
            }
            // Assign unique ID and TeamId
            int assignedUnitId = nextUnitId++;
            unit.SetUnitId(assignedUnitId);
            var teamIdField = typeof(DokkaebiUnit).GetField("teamId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (teamIdField != null)
            {
                teamIdField.SetValue(unit, teamId);
            }
            unit.SetUnitName(definition.displayName);
            SmartLogger.Log($"[SPAWN_AND_REGISTER_UNIT] Assigned ID/TeamId: ID={unit.UnitId}, Name={unit.DisplayName}, TeamId={teamId}", LogCategory.Unit, this);
            // Configure the unit (stats, abilities, etc.)
            ConfigureUnit(unit, definition, teamId == 1); // isPlayerUnit = (teamId == 1)
            unit.SetGridPosition(gridPosition);
            GridManager.Instance.SetTileOccupant(gridPosition, unit);
            RegisterUnit(unit);
            unitPositions[gridPosition] = unit;
            SmartLogger.Log($"[SPAWN_AND_REGISTER_UNIT] Registered unit: Ref={unit.GetHashCode()}, ID={unit.UnitId}, TeamId={teamId}, Name={unit.DisplayName}", LogCategory.Unit, this);
            return unit;
        }
    }
} 
