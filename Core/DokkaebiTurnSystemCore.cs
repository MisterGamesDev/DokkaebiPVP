using System;
using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Utilities;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Pathfinding;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Core implementation of the Dokkaebi Turn Flow System (DTFS).
    /// Handles turn progression, movement, and Aura activation.
    /// </summary>
    public class DokkaebiTurnSystemCore : MonoBehaviour, IUpdateObserver, ITurnSystem
    {
        // Singleton pattern
        public static DokkaebiTurnSystemCore Instance { get; private set; }
        
        // Core components
        [SerializeField] private UnitStateManager unitStateManager;
        public UnitStateManager UnitStateManager => unitStateManager; // Public accessor
        private UnitManager unitManager;
        
        // Turn state management
        private TurnStateContext turnStateContext;
        
        // Turn settings
        [Header("Turn Settings")]
        [SerializeField] private float phaseTransitionDelay = 0.3f;
        
        [Header("Phase Durations")]
        [SerializeField] private float openingPhaseDuration = 3.0f;
        [SerializeField] private float movementPhaseDuration = 15.0f;
        [SerializeField] private float auraChargingPhaseDuration = 5.0f;
        [SerializeField] private float bufferPhaseDuration = 2.0f;
        
        // Events
        public event Action<TurnPhase> OnPhaseChanged;
        public event Action<int> OnTurnChanged;
        public event Action<int> OnActivePlayerChanged;
        public event Action OnMovementPhaseStart;
        public event Action OnMovementPhaseEnd;
        public event Action OnTurnResolutionEnd;
        
        // Unit management
        private Dictionary<DokkaebiUnit, GridPosition> pendingMoves = new Dictionary<DokkaebiUnit, GridPosition>();
        private HashSet<DokkaebiUnit> unitsActedThisPhase = new HashSet<DokkaebiUnit>();
        private List<DokkaebiUnit> registeredUnits = new List<DokkaebiUnit>();
        
        // Movement tracking
        private bool isExecutingMoves = false;
        private int totalMovesMade = 0;
        private int requiredMoves = 4; // Default value
        
        // Debug flags
        [Header("Debug")]
        [SerializeField] private bool debugLogTurns = false;
        
        // Properties for phase durations
        public float OpeningPhaseDuration => openingPhaseDuration;
        public float MovementPhaseDuration => movementPhaseDuration;
        public float AuraChargingPhaseDuration => auraChargingPhaseDuration;
        public float BufferPhaseDuration => bufferPhaseDuration;
        
        // Properties required by ITurnSystem
        public int CurrentTurn => turnStateContext != null ? turnStateContext.GetCurrentTurn() : 1;
        public TurnPhase CurrentPhase => turnStateContext != null ? turnStateContext.GetCurrentPhase() : TurnPhase.Opening;
        public int ActivePlayerId => turnStateContext != null ? turnStateContext.GetActivePlayer() : 0;

        public int GetActivePlayer() => ActivePlayerId;
        public TurnPhase GetCurrentPhase() => CurrentPhase;
        public int GetCurrentTurn() => CurrentTurn;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Get UnitManager instance
            unitManager = UnitManager.Instance;
            if (unitManager == null)
            {
                SmartLogger.LogError("DokkaebiTurnSystemCore could not find UnitManager instance!", LogCategory.TurnSystem, this);
            }

            // Initialize turn state context
            turnStateContext = new TurnStateContext(this);
            turnStateContext.OnPhaseChanged += HandlePhaseChanged;
            turnStateContext.OnTurnChanged += HandleTurnChanged;
            turnStateContext.OnMovementPhaseStart += () => OnMovementPhaseStart?.Invoke();
            turnStateContext.OnMovementPhaseEnd += () => OnMovementPhaseEnd?.Invoke();
            turnStateContext.OnTurnResolutionEnd += () => OnTurnResolutionEnd?.Invoke();

            // Register with update manager
            DokkaebiUpdateManager.Instance.RegisterUpdateObserver(this);
        }

        private void OnDestroy()
        {
            if (turnStateContext != null)
            {
                turnStateContext.OnPhaseChanged -= HandlePhaseChanged;
                turnStateContext.OnTurnChanged -= HandleTurnChanged;
            }

            if (DokkaebiUpdateManager.Instance != null)
            {
                DokkaebiUpdateManager.Instance.UnregisterUpdateObserver(this);
            }
        }

        private void HandlePhaseChanged(TurnPhase newPhase)
        {
            // Log entry
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged ENTRY] Received phase change event. New Phase: {newPhase}, Current Turn: {turnStateContext.GetCurrentTurn()}", LogCategory.TurnSystem, this);

            // Debug.Log($"[DokkaebiTurnSystemCore.HandlePhaseChanged] Phase changed to {newPhase} in Turn {turnStateContext.GetCurrentTurn()}");
            OnPhaseChanged?.Invoke(newPhase);
            
            // Fire OnActivePlayerChanged event with the new active player
            int activePlayer = turnStateContext.GetActivePlayer();
            OnActivePlayerChanged?.Invoke(activePlayer);
            
            if (debugLogTurns)
            {
                SmartLogger.Log($"Turn phase changed to {newPhase}", LogCategory.Debug);
            }
            
            // Reset unit states at the start of MovementPhase
            if (newPhase == TurnPhase.MovementPhase) // Assuming Positioning Phase corresponds to MovementPhase start
            {
                // Log start of Movement Phase block
                SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Entering MovementPhase specific logic block for Turn {turnStateContext.GetCurrentTurn()}", LogCategory.TurnSystem, this);
                
                // Get units directly from UnitManager instead of using registeredUnits
                var allActiveUnits = UnitManager.Instance?.GetAliveUnits();
                if (allActiveUnits != null)
                {
                    // Record positions for all units before resetting states
                    SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Recording start positions for {allActiveUnits.Count} units...", LogCategory.TurnSystem, this);
                    foreach (var unit in allActiveUnits)
                    {
                        if (unit != null)
                        {
                            unit.RecordPositionAtTurnStart();
                            // SmartLogger.Log($"[DokkaebiTurnSystemCore.HandlePhaseChanged] Recorded start position for unit {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.TurnSystem);
                        }
                    }
                     // Log completion of recording positions
                    SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Finished recording start positions.", LogCategory.TurnSystem, this);
                }
                else
                {
                    SmartLogger.LogWarning("[DTSCore.HandlePhaseChanged] Could not get active units from UnitManager during MovementPhase start.", LogCategory.TurnSystem, this);
                }
                // Log exit of Movement Phase block
                SmartLogger.Log($"[DTSCore.HandlePhaseChanged] Exiting MovementPhase specific logic block.", LogCategory.TurnSystem, this);
            }
            // Log exit
            SmartLogger.Log($"[DTSCore.HandlePhaseChanged EXIT] Finished processing phase change to {newPhase}.", LogCategory.TurnSystem, this);
        }

        private void HandleTurnChanged(int newTurn)
        {
            SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Processing start-of-turn logic for Turn {newTurn}", LogCategory.TurnSystem);

            if (unitManager != null)
            {
                // Reset action states for all units at the start of the new turn
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Calling UnitManager.ResetActionStates() for the new turn.", LogCategory.TurnSystem);
                unitManager.ResetActionStates(true); // Reset player unit states
                unitManager.ResetActionStates(false); // Reset enemy unit states
                // Process start-of-turn effects for both players at the beginning of the turn cycle
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Calling UnitManager.StartPlayerTurn()", LogCategory.TurnSystem);
                unitManager.StartPlayerTurn();
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Calling UnitManager.StartEnemyTurn()", LogCategory.TurnSystem);
                unitManager.StartEnemyTurn();

                // Grant passive Aura gain to each active unit at the start of the turn
                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Granting unit-based passive Aura for turn {newTurn}", LogCategory.TurnSystem);
                var activeUnits = unitManager.GetAliveUnits(); // Assuming this returns List<DokkaebiUnit> or similar
                if (activeUnits != null && activeUnits.Count > 0)
                {
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Found {activeUnits.Count} active units to grant Aura.", LogCategory.TurnSystem);
                    foreach (var unit in activeUnits)
                    {
                        if (unit != null)
                        {
                            var unitData = unit.GetUnitDefinitionData();
                            if (unitData != null)
                            {
                                int auraGain = unitData.passiveAuraPerTurn;
                                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Granting {auraGain} passive aura to unit {unit.GetUnitName()} (ID: {unit.UnitId}) from UnitDefinitionData ({unitData.name})", LogCategory.TurnSystem | LogCategory.Debug);
                                SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] About to call ModifyUnitAura for unit {unit.GetUnitName()} (ID: {unit.UnitId}) with amount {auraGain}", LogCategory.TurnSystem);
                                unit.ModifyUnitAura(auraGain);
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.HandleTurnChanged] Could not get UnitDefinitionData for unit {unit.GetUnitName()} (ID: {unit.UnitId}). Cannot grant passive aura.", LogCategory.TurnSystem);
                            }
                        }
                        else
                        {
                            SmartLogger.LogWarning("[DokkaebiTurnSystemCore.HandleTurnChanged] Encountered a null unit while granting Aura.", LogCategory.TurnSystem);
                        }
                    }
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Finished granting unit-based Aura.", LogCategory.TurnSystem);
                }
                else
                {
                    SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.HandleTurnChanged] No active units found to grant Aura.", LogCategory.TurnSystem);
                }
            }
            else
            {
                SmartLogger.LogError("[DokkaebiTurnSystemCore.HandleTurnChanged] UnitManager reference is null, cannot process unit start-of-turn!", LogCategory.TurnSystem);
            }

            OnTurnChanged?.Invoke(newTurn);
            
            // Log each unit in unitsActedThisPhase
            SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Current units in unitsActedThisPhase: {unitsActedThisPhase.Count}", LogCategory.Debug, this);
            foreach (var unit in unitsActedThisPhase)
            {
                if (unit != null)
                {
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] - Unit in unitsActedThisPhase: {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.Debug, this);
                }
                else
                {
                    Debug.LogWarning("[DokkaebiTurnSystemCore.HandleTurnChanged] - Found null unit in unitsActedThisPhase");
                }
            }
            
            // Log each unit in the pendingMoves dictionary
            SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] Current units in pendingMoves: {pendingMoves.Count}", LogCategory.Debug, this);
            foreach (var kvp in pendingMoves)
            {
                if (kvp.Key != null)
                {
                    SmartLogger.Log($"[DokkaebiTurnSystemCore.HandleTurnChanged] - Unit in pendingMoves: {kvp.Key.GetUnitName()} (ID: {kvp.Key.UnitId}) targeting {kvp.Value}", LogCategory.Debug, this);
                }
                else
                {
                    Debug.LogWarning("[DokkaebiTurnSystemCore.HandleTurnChanged] - Found null unit in pendingMoves");
                }
            }
            
            // Debug.Log("[DokkaebiTurnSystemCore] Resetting unit state for new turn");
            unitsActedThisPhase.Clear();
            pendingMoves.Clear();
            isExecutingMoves = false;
            totalMovesMade = 0;
        }

        public void CustomUpdate(float deltaTime)
        {
            if (turnStateContext != null)
            {
                turnStateContext.Update(deltaTime);
                //SmartLogger.Log($"[TurnSystem] [TurnSystemCore] CustomUpdate Current State: {turnStateContext.GetCurrentPhase()}", LogCategory.Debug);
            }
        }

        // Unit movement methods
        private bool CanUnitMove(DokkaebiUnit unit)
        {
            if (turnStateContext == null || !turnStateContext.AllowsMovement())
            {
                Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Movement not allowed for {unit.GetUnitName()} (ID: {unit.UnitId}). turnStateContext null: {turnStateContext == null}, AllowsMovement: {turnStateContext?.AllowsMovement()}");
                return false;
            }
            
            if (isExecutingMoves)
            {
                Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Movement not allowed for {unit.GetUnitName()} (ID: {unit.UnitId}). isExecutingMoves: {isExecutingMoves}");
                return false;
            }
            
            if (HasUnitActedThisPhase(unit))
            {
                Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Movement not allowed for {unit.GetUnitName()} (ID: {unit.UnitId}). Unit has already acted this phase");
                return false;
            }
            
            bool canMove = unit.CanMove();
            Debug.Log($"[DokkaebiTurnSystemCore.CanUnitMove] Final check for {unit.GetUnitName()} (ID: {unit.UnitId}). CanMove: {canMove}");
            return canMove;
        }

        private bool CanUnitUseAura(DokkaebiUnit unit)
        {
            if (turnStateContext == null)
                return false;

            return turnStateContext.AllowsAuraActivation(unit.IsPlayer());
        }

        private void QueueMove(DokkaebiUnit unit, GridPosition targetPosition)
        {
            if (!CanUnitMove(unit))
            {
                Debug.LogWarning($"Unit {unit.UnitId} cannot move in the current phase");
                return;
            }

            pendingMoves[unit] = targetPosition;
            unitsActedThisPhase.Add(unit);
        }

        public void NextPhase()
        {
            if (turnStateContext != null)
            {
                turnStateContext.TransitionToNextState();
            }
            else
            {
                Debug.LogError("TurnStateContext is null in DokkaebiTurnSystemCore.NextPhase");
            }
        }

        public void NextTurn()
        {
            if (turnStateContext != null)
            {
                turnStateContext.IncrementTurn();
            }
            else
            {
                Debug.LogError("TurnStateContext is null in DokkaebiTurnSystemCore.NextTurn");
            }
        }

        /// <summary>
        /// Register a unit with the turn system
        /// </summary>
        public void RegisterUnit(DokkaebiUnit unit)
        {
            if (unit != null && !registeredUnits.Contains(unit))
            {
                // Debug.Log($"[DokkaebiTurnSystemCore.RegisterUnit] Registering unit {unit.GetUnitName()} (ID: {unit.UnitId})");
                registeredUnits.Add(unit);
                // Debug.Log($"[DokkaebiTurnSystemCore.RegisterUnit] After registration, registeredUnits count: {registeredUnits.Count}");
                // SmartLogger.Log($"Unit {unit.GetUnitName()} registered with turn system", LogCategory.TurnSystem);
            }
        }
        
        /// <summary>
        /// Unregister a unit from the turn system
        /// </summary>
        public void UnregisterUnit(DokkaebiUnit unit)
        {
            if (unit != null && registeredUnits.Contains(unit))
            {
                Debug.Log($"[DokkaebiTurnSystemCore.UnregisterUnit] Unregistering unit {unit.GetUnitName()} (ID: {unit.UnitId}). Current count: {registeredUnits.Count}");
                registeredUnits.Remove(unit);
                Debug.Log($"[DokkaebiTurnSystemCore.UnregisterUnit] After unregistration, registeredUnits count: {registeredUnits.Count}");
                
                // Also remove from pending actions if present
                if (pendingMoves.ContainsKey(unit))
                {
                    pendingMoves.Remove(unit);
                }
                
                if (unitsActedThisPhase.Contains(unit))
                {
                    unitsActedThisPhase.Remove(unit);
                }
            }
        }
        
        /// <summary>
        /// Check if all required movement has been completed
        /// </summary>
        private void CheckMovementCompletion()
        {
            TurnPhase currentPhase = GetCurrentPhase();
            if (currentPhase != TurnPhase.MovementPhase || isExecutingMoves)
            {
                return;
            }
            
            bool allMovesComplete = false;
            bool bothPlayersComplete = false;
            
            // Check if all required moves have been completed
            if (totalMovesMade >= requiredMoves)
            {
                allMovesComplete = true;
            }
            
            // Check if both players have reached their individual move limits
            if (unitStateManager != null)
            {
                bool p1MaxReached = unitStateManager.HasReachedMaxMoves(true);
                bool p2MaxReached = unitStateManager.HasReachedMaxMoves(false);
                bothPlayersComplete = p1MaxReached && p2MaxReached;
            }
            
            // If either condition is met, trigger the phase advance
            if ((allMovesComplete || bothPlayersComplete) && !turnStateContext.IsTransitionLocked)
            {
                SmartLogger.Log("All required moves completed!", LogCategory.Movement);
                
                // Execute all pending moves and advance phase
                ExecuteAllPendingMoves();
                turnStateContext.TransitionToNextState();
            }
        }
        
        /// <summary>
        /// Queue an Aura ability usage with strict limit enforcement
        /// </summary>
        public bool QueueAura(DokkaebiUnit unit)
        {
            using (new PerformanceScope("QueueAura"))
            {
                LogTurnSystemState(); // Log turn state for debugging
                
                if (unit == null)
                {
                    SmartLogger.LogError("Cannot queue Aura for null unit!", LogCategory.Ability);
                    return false;
                }
                
                // Check if we're in an Aura phase
                TurnPhase currentPhase = GetCurrentPhase();
                bool isAuraPhase = currentPhase == TurnPhase.AuraPhase1A || 
                                  currentPhase == TurnPhase.AuraPhase1B || 
                                  currentPhase == TurnPhase.AuraPhase2A || 
                                  currentPhase == TurnPhase.AuraPhase2B;
                if (!isAuraPhase)
                {
                    SmartLogger.LogWarning($"Cannot use Aura for {unit.GetUnitName()} - not in Aura phase", LogCategory.Ability);
                    return false;
                }
                
                // Check if it's the right player's turn
                bool isUnitPlayer1 = unit.IsPlayer();
                if (!turnStateContext.AllowsAuraActivation(isUnitPlayer1))
                {
                    SmartLogger.LogWarning($"Not {unit.GetUnitName()}'s turn to use Aura. Active player: {GetActivePlayer()}", LogCategory.Ability);
                    return false;
                }
                
                // Check if unit already used an ability this phase
                if (HasUnitActedThisPhase(unit))
                {
                    SmartLogger.LogWarning($"{unit.GetUnitName()} has already used an ability this phase", LogCategory.Ability);
                    return false;
                }
                
                // Check if player has reached their aura limit for this phase
                if (unitStateManager != null)
                {
                    int maxAuras = unitStateManager.GetMaxAurasPerPhase();
                    bool isPlayer1 = unit.IsPlayer();
                    int currentAuras = isPlayer1 ? unitStateManager.GetPlayer1AurasActivated() : unitStateManager.GetPlayer2AurasActivated();
                    
                    if (currentAuras >= maxAuras)
                    {
                        SmartLogger.LogWarning($"{unit.GetUnitName()} has reached the maximum aura activations for this phase", LogCategory.Ability);
                        return false;
                    }
                }
                
                // Mark unit as having acted this phase
                unitsActedThisPhase.Add(unit);
                
                // Update aura activation count
                if (unitStateManager != null)
                {
                    unitStateManager.RegisterAuraActivated(unit.IsPlayer());
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Check if we should advance to the next phase based on aura usage
        /// </summary>
        private void CheckAuraCompletion()
        {
            TurnPhase currentPhase = GetCurrentPhase();
            bool isAuraPhase = currentPhase == TurnPhase.AuraPhase1A || 
                              currentPhase == TurnPhase.AuraPhase1B || 
                              currentPhase == TurnPhase.AuraPhase2A || 
                              currentPhase == TurnPhase.AuraPhase2B;
            if (!isAuraPhase || turnStateContext.IsTransitionLocked)
            {
                return;
            }
            
            if (unitStateManager != null)
            {
                int activePlayer = GetActivePlayer();
                int maxAuras = unitStateManager.GetMaxAurasPerPhase();
                
                int currentAuras = 0;
                if (activePlayer == 1)
                {
                    currentAuras = unitStateManager.GetPlayer1AurasActivated();
                }
                else if (activePlayer == 2)
                {
                    currentAuras = unitStateManager.GetPlayer2AurasActivated();
                }
                
                // Auto-advance if max auras used
                if (currentAuras >= maxAuras)
                {
                    SmartLogger.Log($"Player {activePlayer} used all {currentAuras}/{maxAuras} auras", LogCategory.Ability);
                    turnStateContext.TransitionToNextState();
                }
                else
                {
                    SmartLogger.Log($"Player {activePlayer} used {currentAuras}/{maxAuras} auras - not advancing yet", LogCategory.Ability);
                }
            }
        }
        
        /// <summary>
        /// Check if a unit has already acted in the current phase
        /// </summary>
        public bool HasUnitActedThisPhase(DokkaebiUnit unit)
        {
            return unit != null && unitsActedThisPhase.Contains(unit);
        }
        
        /// <summary>
        /// Log the current turn system state
        /// </summary>
        public void LogTurnSystemState()
        {
            TurnPhase phase = GetCurrentPhase();
            int turn = GetCurrentTurn();
            int activePlayer = GetActivePlayer();
            
            SmartLogger.Log($"TURN SYSTEM STATE: Turn {turn}, Phase {phase}, Active Player: {activePlayer}", LogCategory.TurnSystem);
            
            if (unitStateManager != null)
            {
                int p1Auras = unitStateManager.GetPlayer1AurasActivated();
                int p2Auras = unitStateManager.GetPlayer2AurasActivated();
                int maxAuras = unitStateManager.GetMaxAurasPerPhase();
                SmartLogger.Log($"P1 Auras: {p1Auras}/{maxAuras}, P2 Auras: {p2Auras}/{maxAuras}", LogCategory.TurnSystem);
                
                int p1Moves = unitStateManager.GetPlayer1UnitsMoved();
                int p2Moves = unitStateManager.GetPlayer2UnitsMoved();
                int p1Required = unitStateManager.GetRequiredPlayer1Moves();
                int p2Required = unitStateManager.GetRequiredPlayer2Moves();
                SmartLogger.Log($"P1 Moves: {p1Moves}/{p1Required}, P2 Moves: {p2Moves}/{p2Required}", LogCategory.TurnSystem);
            }
        }
        
        /// <summary>
        /// Executes all pending moves by initiating pathfinding for each unit
        /// </summary>
        public void ExecuteAllPendingMoves()
        {
            if (isExecutingMoves)
            {
                SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Already executing moves, skipping", LogCategory.Movement);
                return;
            }

            isExecutingMoves = true;
            SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Starting execution of pending moves", LogCategory.Movement);

            // Get units from UnitManager
            if (UnitManager.Instance == null)
            {
                SmartLogger.LogError("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] UnitManager.Instance is null!", LogCategory.Movement);
                isExecutingMoves = false;
                return;
            }

            // Initialize set to track reserved target tiles
            HashSet<GridPosition> reservedTargetTiles = new HashSet<GridPosition>();
            SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Initialized reserved target tile set", LogCategory.Movement);

            var unitsToProcess = UnitManager.Instance.GetAliveUnits();
            SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Found {unitsToProcess.Count} alive units from UnitManager.", LogCategory.Movement);

            // Process all alive units
            foreach (var unit in unitsToProcess)
            {
                if (unit == null) continue;

                SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Checking unit {unit.GetUnitName()} (ID: {unit.UnitId}). HasPendingMovement: {unit.HasPendingMovement}", LogCategory.TurnSystem);

                if (unit.HasPendingMovement)
                {
                    var targetPosition = unit.GetPendingTargetPosition();
                    GridPosition currentPos = unit.CurrentGridPosition;
                    
                    // Only process the move if the target is different from current position
                    if (targetPosition != currentPos)
                    {
                        SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Unit {unit.GetUnitName()} has pending move to {targetPosition}", LogCategory.Movement);

                        // Check if target tile is already reserved
                        if (reservedTargetTiles.Contains(targetPosition))
                        {
                            SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Conflict: Tile {targetPosition} already reserved. Cancelling move for {unit.GetUnitName()}", LogCategory.Movement);
                            unit.ClearPendingMovement();
                            continue;
                        }

                        // Reserve the tile for this unit
                        reservedTargetTiles.Add(targetPosition);
                        SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Tile {targetPosition} reserved for {unit.GetUnitName()}", LogCategory.Movement);
                        
                        // Get the movement handler and initiate movement
                        DokkaebiMovementHandler handler = unit.GetComponent<DokkaebiMovementHandler>();
                        if (handler != null)
                        {
                            SmartLogger.Log($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Initiating movement for {unit.GetUnitName()} to target {targetPosition}", LogCategory.Movement);
                            handler.RequestPath(targetPosition);
                            
                            // Clear pending state AFTER initiating movement
                            unit.ClearPendingMovement();
                        }
                        else
                        {
                            SmartLogger.LogError($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Unit {unit.GetUnitName()} has no DokkaebiMovementHandler component!", LogCategory.Movement);
                            // Remove reservation since movement failed
                            reservedTargetTiles.Remove(targetPosition);
                        }
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Unit {unit.GetUnitName()} has pending movement but target position is same as current position", LogCategory.Movement);
                        unit.ClearPendingMovement();
                    }
                }
            }

            // Mark execution as complete
            isExecutingMoves = false;
            SmartLogger.Log("[DokkaebiTurnSystemCore.ExecuteAllPendingMoves] Completed execution of pending moves", LogCategory.Movement);
        }
        
        /// <summary>
        /// Enable or disable debug logging
        /// </summary>
        public void ToggleDebugMode(bool enabled)
        {
            debugLogTurns = enabled;
            SmartLogger.SetCategoryEnabled(LogCategory.TurnSystem, enabled);
            SmartLogger.Log($"Debug mode {(enabled ? "enabled" : "disabled")}", LogCategory.TurnSystem);
        }
        
        // Getters that use the turn state context
        public bool IsPlayerTurn() => GetActivePlayer() == 1 || GetActivePlayer() == 0;
        public bool IsExecutingMoves() => isExecutingMoves;
        public bool IsPhaseAdvancementLocked() => turnStateContext?.IsTransitionLocked ?? false;

        // ITurnSystem implementation
        public bool CanUnitMove(IDokkaebiUnit unit)
        {
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                return CanUnitMove(dokkaebiUnit);
            }
            return false;
        }
        
        public bool CanUnitUseAura(IDokkaebiUnit unit)
        {
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                return CanUnitUseAura(dokkaebiUnit);
            }
            return false;
        }
        
        public void QueueMove(IDokkaebiUnit unit, GridPosition targetPosition)
        {
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                QueueMove(dokkaebiUnit, targetPosition);
            }
        }
        
        public void EndMovementPhase()
        {
            SmartLogger.Log($"[DokkaebiTurnSystemCore.EndMovementPhase] Called. Current phase: {GetCurrentPhase()}, IsTransitionLocked: {turnStateContext?.IsTransitionLocked ?? false}", LogCategory.TurnSystem);
            
            if (turnStateContext != null && GetCurrentPhase() == TurnPhase.MovementPhase)
            {
                SmartLogger.Log("[DokkaebiTurnSystemCore.EndMovementPhase] Attempting to transition to next state", LogCategory.TurnSystem);
                turnStateContext.TransitionToNextState();
                
                // Log the result
                SmartLogger.Log($"[DokkaebiTurnSystemCore.EndMovementPhase] After transition attempt. New phase: {GetCurrentPhase()}", LogCategory.TurnSystem);
            }
            else
            {
                SmartLogger.LogWarning($"[DokkaebiTurnSystemCore.EndMovementPhase] Cannot end movement phase. TurnStateContext null: {turnStateContext == null}, Current phase: {GetCurrentPhase()}", LogCategory.TurnSystem);
            }
        }

        public void ForceTransitionTo(TurnPhase phase)
        {
            if (turnStateContext != null)
            {
                turnStateContext.ForceTransitionTo(phase);
            }
        }

        public bool RequestMovement(DokkaebiUnit unit, GridPosition targetPosition)
        {
            if (!unit || !turnStateContext.AllowsMovement() || isExecutingMoves)
            {
                return false;
            }
            
            // Add to pending moves
            pendingMoves[unit] = targetPosition;
            return true;
        }
        
        public bool RequestAuraActivation(DokkaebiUnit unit, bool isPlayer1)
        {
            if (!unit || !turnStateContext.AllowsAuraActivation(isPlayer1))
            {
                return false;
            }
            
            // Add to units that acted this phase
            unitsActedThisPhase.Add(unit);
            return true;
        }

        /// <summary>
        /// Sets the turn system state based on network data
        /// </summary>
        /// <param name="turnNumber">The turn number to set</param>
        /// <param name="phase">The phase to transition to</param>
        /// <param name="activePlayer">The active player (1 for player 1, 0 for player 2)</param>
        public void SetState(int turnNumber, TurnPhase phase, int activePlayer)
        {
            if (turnStateContext == null)
            {
                Debug.LogError("Cannot set state: TurnStateContext is null");
                return;
            }

            // Set turn number first
            turnStateContext.SetTurn(turnNumber);

            // Force transition to the target phase
            turnStateContext.ForceTransitionTo(phase);

            // Log the state change
            if (debugLogTurns)
            {
                SmartLogger.Log($"Turn system state set: Turn {turnNumber}, Phase {phase}, Active Player {activePlayer}", LogCategory.TurnSystem);
            }
        }

        public float GetRemainingPhaseTime()
        {
            return turnStateContext != null ? turnStateContext.GetRemainingTime() : 0f;
        }

        /// <summary>
        /// Resets the turn system to its initial state.
        /// </summary>
        public void ResetTurnSystem()
        {
            // Clear all collections
            pendingMoves.Clear();
            unitsActedThisPhase.Clear();
            registeredUnits.Clear();

            // Reset movement tracking
            isExecutingMoves = false;
            totalMovesMade = 0;

            // Reset turn state context
            if (turnStateContext != null)
            {
                // Reset to turn 1, opening phase, player 1
                SetState(1, TurnPhase.Opening, 1);
            }

            // Reset all units via UnitManager if available
            if (UnitManager.Instance != null)
            {
                // Ensure all units' action states are reset when the turn system is reset
                SmartLogger.Log("[DokkaebiTurnSystemCore.ResetTurnSystem] Calling UnitManager.ResetActionStates() as part of system reset.", LogCategory.TurnSystem);
                UnitManager.Instance.ResetActionStates(true); // Reset player unit states
                UnitManager.Instance.ResetActionStates(false); // Reset enemy unit states
                var allUnits = UnitManager.Instance.GetAliveUnits();
                if (allUnits != null)
                {
                    foreach (var unit in allUnits)
                    {
                        if (unit != null)
                        {
                            unit.ResetActionState();
                        }
                    }
                }
            }

            if (debugLogTurns)
            {
                SmartLogger.Log("Turn system has been reset to initial state", LogCategory.TurnSystem);
            }
        }
    }
}
