using System;
using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Zones;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.Core.Data;
using Dokkaebi.Core;
using Dokkaebi.TurnSystem;
using System.Linq;

namespace Dokkaebi.Core.Networking
{
    /// <summary>
    /// Enhanced GameStateManager for V3 Multiplayer system
    /// Synchronizes local game state with authoritative server state
    /// Handles real-time updates and conflict resolution
    /// </summary>
    public class GameStateManagerMultiplayer : MonoBehaviour
    {
        public static GameStateManagerMultiplayer Instance { get; private set; }

        [Header("Network Dependencies")]
        [SerializeField] private NetworkingManager networkManager;
        [SerializeField] private V3TurnManager turnManager;
        
        [Header("Game Managers")]
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private ZoneManager zoneManager;
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private AuraManager auraManager;
        
        [Header("Synchronization Settings")]
        [SerializeField] private bool enableRealTimeSync = true;
        [SerializeField] private float syncInterval = 1.0f; // seconds
        [SerializeField] private bool enableDebugLogging = true;
        
        // Current authoritative state
        private Dictionary<string, object> authoritativeGameState;
        private string currentMatchId;
        private float lastSyncTime;
        
        // Events
        public event Action<Dictionary<string, object>> OnAuthoritativeStateReceived;
        public event Action<List<UnitStateUpdate>> OnUnitsUpdated;
        public event Action<List<ZoneStateUpdate>> OnZonesUpdated;
        public event Action<GamePhaseUpdate> OnPhaseUpdated;
        public event Action<string> OnSyncError;
        
        // State tracking for smooth updates
        private Dictionary<int, UnitState> lastKnownUnitStates = new Dictionary<int, UnitState>();
        private Queue<StateUpdate> pendingUpdates = new Queue<StateUpdate>();
        
        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeComponents();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Subscribe to network and turn system events
            SubscribeToEvents();
            
            LogDebug("GameStateManagerMultiplayer initialized and ready for synchronization");
        }
        
        /// <summary>
        /// Initialize component references
        /// </summary>
        private void InitializeComponents()
        {
            if (networkManager == null)
                networkManager = FindFirstObjectByType<NetworkingManager>();
                
            if (turnManager == null)
                turnManager = FindFirstObjectByType<V3TurnManager>();
                
            if (unitManager == null)
                unitManager = FindFirstObjectByType<UnitManager>();
                
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
                
            if (zoneManager == null)
                zoneManager = FindFirstObjectByType<ZoneManager>();
                
            if (abilityManager == null)
                abilityManager = FindFirstObjectByType<AbilityManager>();
                
            if (auraManager == null)
                auraManager = FindFirstObjectByType<AuraManager>();
        }
        
        /// <summary>
        /// Subscribe to relevant events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnGameStateUpdated += HandleAuthoritativeStateUpdate;
                networkManager.OnNetworkError += HandleNetworkError;
            }
            
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.AddListener(OnTurnStarted);
                turnManager.OnPhaseChanged.AddListener(OnPhaseChanged);
                turnManager.OnTurnCompleted.AddListener(OnTurnCompleted);
            }
        }
        
        /// <summary>
        /// Handle authoritative state updates from server
        /// </summary>
        private void HandleAuthoritativeStateUpdate(Dictionary<string, object> serverState)
        {
            LogDebug("Received authoritative state update from server");
            
            authoritativeGameState = serverState;
            OnAuthoritativeStateReceived?.Invoke(serverState);
            
            // Apply updates to local game objects
            ApplyAuthoritativeState(serverState);
        }
        
        /// <summary>
        /// Apply server state to local game objects (public for testing)
        /// </summary>
        public void ApplyAuthoritativeStateForTesting(Dictionary<string, object> serverState)
        {
            ApplyAuthoritativeState(serverState);
        }

        /// <summary>
        /// Apply server state to local game objects
        /// </summary>
        private void ApplyAuthoritativeState(Dictionary<string, object> serverState)
        {
            try
            {
                // Update turn information
                ApplyTurnState(serverState);
                
                // Update unit states
                ApplyUnitStates(serverState);
                
                // Update zone states
                ApplyZoneStates(serverState);
                
                // Update player resources
                ApplyPlayerResources(serverState);
                
                LogDebug("Successfully applied authoritative state");
            }
            catch (Exception e)
            {
                LogError($"Failed to apply authoritative state: {e.Message}");
                OnSyncError?.Invoke($"State sync error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Apply turn state updates
        /// </summary>
        private void ApplyTurnState(Dictionary<string, object> serverState)
        {
            if (serverState.TryGetValue("currentTurn", out var turnObj) && 
                int.TryParse(turnObj.ToString(), out int serverTurn))
            {
                if (turnManager != null && turnManager.CurrentTurnNumber != serverTurn)
                {
                    LogDebug($"Syncing turn number: {turnManager.CurrentTurnNumber} -> {serverTurn}");
                    // Note: This would require a public setter on V3TurnManagerMultiplayer
                }
            }
            
            if (serverState.TryGetValue("currentPhase", out var phaseObj) &&
                Enum.TryParse(phaseObj.ToString(), out V3TurnPhase serverPhase))
            {
                if (turnManager != null && turnManager.CurrentPhase != serverPhase)
                {
                    LogDebug($"Syncing phase: {turnManager.CurrentPhase} -> {serverPhase}");
                    OnPhaseUpdated?.Invoke(new GamePhaseUpdate 
                    { 
                        newPhase = serverPhase, 
                        previousPhase = turnManager.CurrentPhase 
                    });
                }
            }
        }
        
        /// <summary>
        /// Apply unit state updates from server
        /// </summary>
        private void ApplyUnitStates(Dictionary<string, object> serverState)
        {
            if (!serverState.TryGetValue("units", out var unitsObj) || !(unitsObj is List<object> unitsList))
            {
                LogDebug("No unit data in server state");
                return;
            }
                
            var unitUpdates = new List<UnitStateUpdate>();
            
            foreach (var unitObj in unitsList)
            {
                if (!(unitObj is Dictionary<string, object> unitData))
                    continue;
                    
                try
                {
                    var unitUpdate = ParseUnitStateFromServer(unitData);
                    if (unitUpdate != null)
                    {
                        unitUpdates.Add(unitUpdate);
                        ApplyUnitStateToLocal(unitUpdate);
                    }
                }
                catch (Exception e)
                {
                    LogError($"Failed to parse unit state: {e.Message}");
                }
            }
            
            if (unitUpdates.Count > 0)
            {
                OnUnitsUpdated?.Invoke(unitUpdates);
                LogDebug($"Applied {unitUpdates.Count} unit state updates");
            }
        }
        
        /// <summary>
        /// Parse unit state data from server response
        /// </summary>
        private UnitStateUpdate ParseUnitStateFromServer(Dictionary<string, object> unitData)
        {
            if (!unitData.TryGetValue("unitId", out var unitIdObj))
                return null;
                
            string unitId = unitIdObj.ToString();
            var update = new UnitStateUpdate { UnitId = unitId };
            
            // Position
            if (unitData.TryGetValue("position", out var posObj) && posObj is Dictionary<string, object> posData)
            {
                if (posData.TryGetValue("x", out var xObj) && posData.TryGetValue("y", out var yObj))
            {
                    update.NewPosition = new GridPosition(
                        Convert.ToInt32(xObj), 
                        Convert.ToInt32(yObj)
                );
                }
            }

            // Health
            if (unitData.TryGetValue("currentHealth", out var healthObj))
            {
                update.CurrentHealth = Convert.ToInt32(healthObj);
            }

            // MP/Movement Points
            if (unitData.TryGetValue("currentMP", out var mpObj))
            {
                update.CurrentMP = Convert.ToSingle(mpObj);
            }

            // Status effects
            if (unitData.TryGetValue("statusEffects", out var effectsObj) && effectsObj is List<object> effectsList)
            {
                update.StatusEffects = new List<string>();
                foreach (var effectObj in effectsList)
                {
                    if (effectObj is Dictionary<string, object> effectData && 
                        effectData.TryGetValue("type", out var typeObj))
                    {
                        update.StatusEffects.Add(typeObj.ToString());
                    }
                }
            }

            // Ability cooldowns
            if (unitData.TryGetValue("abilityCooldowns", out var cooldownsObj) && 
                cooldownsObj is Dictionary<string, object> cooldownsData)
            {
                update.AbilityCooldowns = new Dictionary<int, int>();
                foreach (var kvp in cooldownsData)
                {
                    if (int.TryParse(kvp.Key, out int abilityIndex))
                    {
                        update.AbilityCooldowns[abilityIndex] = Convert.ToInt32(kvp.Value);
                    }
                }
            }
            
            // Action state flags
            if (unitData.TryGetValue("hasMovedThisTurn", out var movedObj))
            {
                update.HasMovedThisTurn = Convert.ToBoolean(movedObj);
            }

            if (unitData.TryGetValue("hasUsedAbility", out var abilityObj))
            {
                update.HasUsedAbility = Convert.ToBoolean(abilityObj);
            }

            return update;
        }

        /// <summary>
        /// Apply unit state update to local unit
        /// </summary>
        private void ApplyUnitStateToLocal(UnitStateUpdate update)
                {
            if (unitManager == null)
                return;

            // Find the unit by ID
            var localUnit = unitManager.GetUnitById(int.Parse(update.UnitId));
            if (localUnit == null)
            {
                LogError($"Cannot find local unit with ID: {update.UnitId}");
                return;
            }

            // Apply position update
            if (update.NewPosition.HasValue && localUnit.CurrentGridPosition != update.NewPosition.Value)
            {
                LogDebug($"Updating unit {update.UnitId} position: {localUnit.CurrentGridPosition} -> {update.NewPosition.Value}");
                localUnit.SetGridPosition(update.NewPosition.Value);
            }

            // Apply health update
            if (update.CurrentHealth.HasValue)
            {
                int healthDifference = update.CurrentHealth.Value - localUnit.CurrentHealth;
                if (healthDifference != 0)
                {
                    LogDebug($"Updating unit {update.UnitId} health: {localUnit.CurrentHealth} -> {update.CurrentHealth.Value}");
                    
                    if (healthDifference > 0)
                    {
                        localUnit.Heal(healthDifference);
                    }
                    else
                    {
                        localUnit.TakeDamage(-healthDifference, DamageType.True); // Use True damage for server sync
                    }
                }
            }

            // Apply MP update
            if (update.CurrentMP.HasValue)
                {
                LogDebug($"Updating unit {update.UnitId} MP: {localUnit.GetCurrentMP()} -> {update.CurrentMP.Value}");
                // Note: SetCurrentMP method doesn't exist in DokkaebiUnit, would need to be added
                // For now, we'll log this but skip the actual update
                LogDebug($"MP update skipped - SetCurrentMP method not implemented in DokkaebiUnit");
            }
            
            // Apply status effects (simplified - would need more complex logic for full implementation)
            if (update.StatusEffects != null)
            {
                LogDebug($"Unit {update.UnitId} has {update.StatusEffects.Count} status effects from server");
                // Note: Full status effect sync would require more complex implementation
            }

            // Apply ability cooldowns
            if (update.AbilityCooldowns != null)
            {
                foreach (var kvp in update.AbilityCooldowns)
                {
                    LogDebug($"Setting ability {kvp.Key} cooldown to {kvp.Value} for unit {update.UnitId}");
                    localUnit.SetAbilityCooldown(kvp.Key.ToString(), kvp.Value);
                }
            }

            // Apply action state flags
            if (update.HasMovedThisTurn.HasValue)
            {
                localUnit.SetHasMoved(update.HasMovedThisTurn.Value);
            }

            if (update.HasUsedAbility.HasValue)
            {
                // Note: SetHasUsedAbility method doesn't exist in DokkaebiUnit, would need to be added
                // For now, we'll log this but skip the actual update
                LogDebug($"HasUsedAbility update skipped - SetHasUsedAbility method not implemented in DokkaebiUnit");
            }
        }
        
        /// <summary>
        /// Apply zone state updates from server
        /// </summary>
        private void ApplyZoneStates(Dictionary<string, object> serverState)
        {
            if (!serverState.TryGetValue("zones", out var zonesObj) || !(zonesObj is List<object> zonesList))
            {
                LogDebug("No zone data in server state");
                return;
            }
                
            var zoneUpdates = new List<ZoneStateUpdate>();
            
            foreach (var zoneObj in zonesList)
            {
                if (!(zoneObj is Dictionary<string, object> zoneData))
                    continue;
                    
                try
                {
                    var zoneUpdate = ParseZoneStateFromServer(zoneData);
                    if (zoneUpdate != null)
                    {
                        zoneUpdates.Add(zoneUpdate);
                        ApplyZoneStateToLocal(zoneUpdate);
                    }
                }
                catch (Exception e)
                {
                    LogError($"Failed to parse zone state: {e.Message}");
                }
            }
            
            if (zoneUpdates.Count > 0)
            {
                OnZonesUpdated?.Invoke(zoneUpdates);
                LogDebug($"Applied {zoneUpdates.Count} zone state updates");
            }
        }
        
        /// <summary>
        /// Parse zone state from server response
        /// </summary>
        private ZoneStateUpdate ParseZoneStateFromServer(Dictionary<string, object> zoneData)
        {
            if (!zoneData.TryGetValue("zoneId", out var zoneIdObj))
            return null;

            int zoneId = Convert.ToInt32(zoneIdObj);
            var update = new ZoneStateUpdate { ZoneId = zoneId };

            // Control percentage
            if (zoneData.TryGetValue("controlPercentage", out var controlObj))
            {
                update.ControlPercentage = Convert.ToSingle(controlObj);
            }

            // Controlling player
            if (zoneData.TryGetValue("controllingPlayerId", out var playerObj))
            {
                update.ControllingPlayerId = Convert.ToInt32(playerObj);
            }

            // Duration remaining
            if (zoneData.TryGetValue("durationRemaining", out var durationObj))
            {
                update.DurationRemaining = Convert.ToInt32(durationObj);
            }

            // Active effects
            if (zoneData.TryGetValue("activeEffects", out var effectsObj) && effectsObj is List<object> effectsList)
            {
                update.ActiveEffects = new List<string>();
                foreach (var effectObj in effectsList)
                {
                    update.ActiveEffects.Add(effectObj.ToString());
                }
            }

            return update;
        }
        
        /// <summary>
        /// Apply zone state update to local zone
        /// </summary>
        private void ApplyZoneStateToLocal(ZoneStateUpdate update)
        {
            if (zoneManager == null)
                return;

            var localZone = zoneManager.GetZone(update.ZoneId.ToString());
            if (localZone == null)
            {
                LogError($"Cannot find local zone with ID: {update.ZoneId}");
                return;
            }

            // Apply control updates
            if (update.ControlPercentage.HasValue)
            {
                LogDebug($"Zone {update.ZoneId} control percentage update skipped - GetControlPercentage/SetControlPercentage not implemented in Zone class");
            }

            if (update.ControllingPlayerId.HasValue)
            {
                LogDebug($"Zone {update.ZoneId} controlling player update skipped - GetControllingPlayerId/SetControllingPlayer not implemented in Zone class");
            }
            
            // Apply duration update
            if (update.DurationRemaining.HasValue)
            {
                LogDebug($"Updating zone {update.ZoneId} duration: {localZone.RemainingDuration} -> {update.DurationRemaining.Value}");
                localZone.SetDuration(update.DurationRemaining.Value);
            }
        }
        
        /// <summary>
        /// Apply player resource updates (including aura)
        /// </summary>
        private void ApplyPlayerResources(Dictionary<string, object> serverState)
        {
            // Player 1 aura
            if (serverState.TryGetValue("player1Aura", out var p1AuraObj))
            {
                int serverAura = Convert.ToInt32(p1AuraObj);
                if (auraManager != null)
                {
                    int currentAura = auraManager.GetPlayerAura(true);
                    if (currentAura != serverAura)
                    {
                        LogDebug($"Syncing Player 1 aura: {currentAura} -> {serverAura}");
                        auraManager.SetPlayerAura(true, serverAura);
                    }
                }
            }

            // Player 2 aura
            if (serverState.TryGetValue("player2Aura", out var p2AuraObj))
            {
                int serverAura = Convert.ToInt32(p2AuraObj);
                if (auraManager != null)
                {
                    int currentAura = auraManager.GetPlayerAura(false);
                if (currentAura != serverAura)
                {
                        LogDebug($"Syncing Player 2 aura: {currentAura} -> {serverAura}");
                        auraManager.SetPlayerAura(false, serverAura);
                    }
                }
            }

            // Player 1 max aura
            if (serverState.TryGetValue("player1MaxAura", out var p1MaxAuraObj))
            {
                int serverMaxAura = Convert.ToInt32(p1MaxAuraObj);
                if (auraManager != null)
                {
                    int currentMaxAura = auraManager.GetPlayerMaxAura(true);
                    if (currentMaxAura != serverMaxAura)
                    {
                        LogDebug($"Syncing Player 1 max aura: {currentMaxAura} -> {serverMaxAura}");
                        auraManager.SetPlayerMaxAura(true, serverMaxAura);
                    }
                }
            }

            // Player 2 max aura
            if (serverState.TryGetValue("player2MaxAura", out var p2MaxAuraObj))
            {
                int serverMaxAura = Convert.ToInt32(p2MaxAuraObj);
                if (auraManager != null)
                {
                    int currentMaxAura = auraManager.GetPlayerMaxAura(false);
                    if (currentMaxAura != serverMaxAura)
                    {
                        LogDebug($"Syncing Player 2 max aura: {currentMaxAura} -> {serverMaxAura}");
                        auraManager.SetPlayerMaxAura(false, serverMaxAura);
                    }
                }
            }
        }
        
        /// <summary>
        /// Request immediate state sync from server
        /// </summary>
        public void RequestStateSync()
        {
            if (turnManager != null && !string.IsNullOrEmpty(currentMatchId))
            {
                LogDebug("Requesting immediate state sync from server");
                // This would trigger the V3TurnManagerMultiplayer to request game state
            }
        }
        
        /// <summary>
        /// Set the current match ID for synchronization
        /// </summary>
        public void SetMatchId(string matchId)
        {
            currentMatchId = matchId;
            LogDebug($"Match ID set to: {matchId}");
        }
        
        /// <summary>
        /// Handle network errors
        /// </summary>
        private void HandleNetworkError(string error)
        {
            LogError($"Network error during state sync: {error}");
            OnSyncError?.Invoke(error);
        }
        
        /// <summary>
        /// Handle turn system events
        /// </summary>
        private void OnTurnStarted(int turnNumber)
        {
            LogDebug($"Turn {turnNumber} started - requesting state sync");
            RequestStateSync();
        }
        
        private void OnPhaseChanged(V3TurnPhase newPhase)
        {
            LogDebug($"Phase changed to {newPhase}");
        }
        
        private void OnTurnCompleted()
        {
            LogDebug("Turn completed - state should be synchronized");
        }
        
        /// <summary>
        /// Periodic sync check
        /// </summary>
        private void Update()
        {
            if (enableRealTimeSync && Time.time - lastSyncTime > syncInterval)
            {
                lastSyncTime = Time.time;
                
                // Only sync if we have an active match
                if (!string.IsNullOrEmpty(currentMatchId))
                {
                    RequestStateSync();
                }
            }
        }
        
        /// <summary>
        /// Cleanup on destroy
        /// </summary>
        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnGameStateUpdated -= HandleAuthoritativeStateUpdate;
                networkManager.OnNetworkError -= HandleNetworkError;
            }
            
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.RemoveListener(OnTurnStarted);
                turnManager.OnPhaseChanged.RemoveListener(OnPhaseChanged);
                turnManager.OnTurnCompleted.RemoveListener(OnTurnCompleted);
            }
        }
        
        /// <summary>
        /// Debug logging
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                SmartLogger.Log($"[GameStateManagerMultiplayer] {message}", LogCategory.Networking, this);
            }
        }
        
        private void LogError(string message)
        {
            SmartLogger.LogError($"[GameStateManagerMultiplayer] {message}", LogCategory.Networking, this);
        }
    }
    
    // Data structures for state updates
    [System.Serializable]
    public class UnitStateUpdate
    {
        public string UnitId;
        public GridPosition? NewPosition;
        public int? CurrentHealth;
        public float? CurrentMP;
        public List<string> StatusEffects;
        public Dictionary<int, int> AbilityCooldowns;
        public bool? HasMovedThisTurn;
        public bool? HasUsedAbility;
    }
    
    [System.Serializable]
    public class ZoneStateUpdate
    {
        public int ZoneId;
        public float? ControlPercentage;
        public int? ControllingPlayerId;
        public int? DurationRemaining;
        public List<string> ActiveEffects;
    }
    
    [System.Serializable]
    public class GamePhaseUpdate
    {
        public V3TurnPhase previousPhase;
        public V3TurnPhase newPhase;
    }
    
    [System.Serializable]
    public class UnitState
    {
        public int unitId;
        public GridPosition position;
        public int health;
        public int aura;
        public List<string> statusEffects;
    }
    
    [System.Serializable]
    public class StateUpdate
    {
        public string updateType;
        public Dictionary<string, object> data;
        public float timestamp;
    }
} 