using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Dokkaebi.Units;
using Dokkaebi.Grid;
using Dokkaebi.Interfaces;
using Dokkaebi.Core.Networking;
using Dokkaebi.TurnSystem;
using Dokkaebi.Utilities;
using Dokkaebi.Common;
using Dokkaebi.Core.Data;
using Dokkaebi.Zones;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Manages player actions and input processing for the turn system
    /// Simplified version that integrates with V3TurnManager
    /// </summary>
    public class PlayerActionManager : MonoBehaviour
    {
        public static PlayerActionManager Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private V3TurnManager turnManager;
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private AbilityManager abilityManager;

        [Header("Action State")]
        [SerializeField] private ActionState currentActionState = ActionState.Idle;
        [SerializeField] private DokkaebiUnit selectedUnit;
        [SerializeField] private int targetingAbilityIndex = -1;

        [Header("Settings")]
        [SerializeField] private bool enableDebugLogging = true;

        // Action States
        public enum ActionState
        {
            Idle,
            SelectingAbilityTarget,
            SelectingZoneDestination
        }

        // Events that other components subscribe to
        public event Action<bool, string> OnCommandResult;
        public event Action<AbilityData> OnAbilityTargetingStarted;
        public event Action OnAbilityTargetingCancelled;
        public event Action<DokkaebiUnit> OnUnitSelected;
        public event Action OnUnitDeselected;
        public event Action<IZoneInstance> OnZoneDestinationSelectionStarted;

        // Properties
        public ActionState GetCurrentActionState() => currentActionState;
        public DokkaebiUnit SelectedUnit => selectedUnit;
        public bool HasSelectedUnit => selectedUnit != null;

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                InitializeComponents();
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SubscribeToEvents();
            LogDebug("PlayerActionManager initialized successfully");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            // Find components if not assigned
            if (turnManager == null)
                turnManager = FindFirstObjectByType<V3TurnManager>();
                
            unitManager = UnitManager.Instance;
            if (unitManager == null)
            {
                // Fallback: Try to find it in the scene
                unitManager = FindFirstObjectByType<UnitManager>();
                SmartLogger.LogWarning("DokkaebiTurnSystemCore: UnitManager.Instance was null, using FindFirstObjectByType fallback", LogCategory.TurnSystem, this);
            }

            if (unitManager == null)
            {
                SmartLogger.LogError("DokkaebiTurnSystemCore could not find UnitManager in scene!", LogCategory.TurnSystem, this);
            }
                
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
                
            if (abilityManager == null)
                abilityManager = FindFirstObjectByType<AbilityManager>();

            // Validate critical components
            if (turnManager == null)
            {
                LogError("V3TurnManager not found! PlayerActionManager will not function properly.");
            }

            if (unitManager == null)
            {
                LogError("UnitManager not found! Unit selection will not work.");
            }

            if (gridManager == null)
            {
                LogError("GridManager not found! Grid operations will not work.");
            }
        }

        private void SubscribeToEvents()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.AddListener(OnTurnStarted);
                turnManager.OnPhaseChanged.AddListener(OnPhaseChanged);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.RemoveListener(OnTurnStarted);
                turnManager.OnPhaseChanged.RemoveListener(OnPhaseChanged);
            }
        }

        #endregion

        #region Unit Selection

        /// <summary>
        /// Handle unit click from InputManager
        /// </summary>
        public void HandleUnitClick(DokkaebiUnit unit)
        {
            if (unit == null)
            {
                LogWarning("HandleUnitClick called with null unit");
                return;
            }

            LogDebug($"HandleUnitClick: {unit.DisplayName} (State: {currentActionState})");

            switch (currentActionState)
            {
                case ActionState.Idle:
                    HandleUnitSelectionInIdle(unit);
                    break;
                    
                case ActionState.SelectingAbilityTarget:
                    HandleAbilityTargeting(unit);
                    break;
                    
                case ActionState.SelectingZoneDestination:
                    // Zone targeting typically targets positions, not units
                    // Cancel targeting for now
                    CancelAbilityTargeting();
                    break;
            }
        }

        private void HandleUnitSelectionInIdle(DokkaebiUnit unit)
        {
            // Check if it's the player's turn and they can select units
            if (!CanSelectUnits())
            {
                LogDebug("Cannot select units at this time");
                return;
            }

            // Select the unit if it belongs to the player
            if (unit.IsPlayerControlled)
            {
                SelectUnit(unit);
            }
            else
            {
                LogDebug($"Cannot select enemy unit: {unit.DisplayName}");
                // Could show unit info instead of selecting
            }
        }

        private void SelectUnit(DokkaebiUnit unit)
        {
            if (selectedUnit == unit)
            {
                LogDebug($"Unit {unit.DisplayName} is already selected");
                return;
            }

            // Deselect current unit
            if (selectedUnit != null)
            {
                DeselectCurrentUnit();
            }

            // Select new unit
            selectedUnit = unit;
            OnUnitSelected?.Invoke(unit);
            
            LogDebug($"Selected unit: {unit.DisplayName}");
        }

        private void DeselectCurrentUnit()
        {
            if (selectedUnit != null)
            {
                LogDebug($"Deselected unit: {selectedUnit.DisplayName}");
                selectedUnit = null;
                OnUnitDeselected?.Invoke();
            }
        }

        #endregion

        #region Ground Click Handling

        /// <summary>
        /// Handle ground click from InputManager
        /// </summary>
        public void HandleGroundClick(Vector2Int gridPosition)
        {
            LogDebug($"HandleGroundClick: ({gridPosition.x}, {gridPosition.y}) (State: {currentActionState})");

            switch (currentActionState)
            {
                case ActionState.Idle:
                    HandleMovementCommand(gridPosition);
                    break;
                    
                case ActionState.SelectingAbilityTarget:
                    HandleAbilityGroundTargeting(gridPosition);
                    break;
                    
                case ActionState.SelectingZoneDestination:
                    HandleZoneTargeting(gridPosition);
                    break;
            }
        }

        private void HandleMovementCommand(Vector2Int gridPosition)
        {
            if (selectedUnit == null)
            {
                LogDebug("No unit selected for movement");
                return;
            }

            if (!CanIssueCommands())
            {
                LogDebug("Cannot issue movement commands at this time");
                return;
            }

            // Create and submit movement action
            var moveAction = PlayerAction.CreateMoveAction(selectedUnit, gridPosition);
            SubmitAction(moveAction);
        }

        private void HandleAbilityGroundTargeting(Vector2Int gridPosition)
        {
            if (selectedUnit == null || targetingAbilityIndex < 0)
            {
                LogError("Invalid ability targeting state");
                CancelAbilityTargeting();
                return;
            }

            // Create basic ability action - simplified for now
            var abilityAction = new PlayerAction
            {
                actionType = ActionType.UseAbility,
                actingUnit = selectedUnit,
                targetPosition = gridPosition
            };

            SubmitAction(abilityAction);
            CancelAbilityTargeting();
        }

        private void HandleZoneTargeting(Vector2Int gridPosition)
        {
            if (selectedUnit == null || targetingAbilityIndex < 0)
            {
                LogError("Invalid zone targeting state");
                CancelAbilityTargeting();
                return;
            }

            // Create zone ability action - simplified
            var zoneAction = new PlayerAction
            {
                actionType = ActionType.UseAbility,
                actingUnit = selectedUnit,
                targetPosition = gridPosition
            };

            SubmitAction(zoneAction);
            CancelAbilityTargeting();
        }

        #endregion

        #region Ability System

        /// <summary>
        /// Start ability targeting mode
        /// </summary>
        public void StartAbilityTargeting(DokkaebiUnit unit, int abilityIndex)
        {
            if (unit == null)
            {
                LogError("StartAbilityTargeting called with null unit");
                return;
            }

            if (!CanIssueCommands())
            {
                LogDebug("Cannot start ability targeting at this time");
                return;
            }

            selectedUnit = unit;
            targetingAbilityIndex = abilityIndex;
            currentActionState = ActionState.SelectingAbilityTarget;

            // TODO: Get the actual ability data from the unit
            // For now, pass null to satisfy the event signature
            OnAbilityTargetingStarted?.Invoke(null);
            LogDebug($"Started ability targeting for {unit.DisplayName}, ability index {abilityIndex}");
        }

        /// <summary>
        /// Cancel ability targeting mode
        /// </summary>
        public void CancelAbilityTargeting()
        {
            if (currentActionState == ActionState.SelectingAbilityTarget || 
                currentActionState == ActionState.SelectingZoneDestination)
            {
                currentActionState = ActionState.Idle;
                targetingAbilityIndex = -1;
                
                OnAbilityTargetingCancelled?.Invoke();
                LogDebug("Cancelled ability targeting");
            }
        }

        private void HandleAbilityTargeting(DokkaebiUnit targetUnit)
        {
            if (selectedUnit == null || targetingAbilityIndex < 0)
            {
                LogError("Invalid ability targeting state");
                CancelAbilityTargeting();
                return;
            }

            // Create ability action targeting a unit - simplified
            var abilityAction = new PlayerAction
            {
                actionType = ActionType.UseAbility,
                actingUnit = selectedUnit,
                targetUnit = targetUnit
            };

            SubmitAction(abilityAction);
            CancelAbilityTargeting();
        }

        #endregion

        #region Action Submission

        /// <summary>
        /// Submit action to the turn manager
        /// </summary>
        private void SubmitAction(PlayerAction action)
        {
            if (turnManager == null)
            {
                LogError("Cannot submit action: V3TurnManager not available");
                OnCommandResult?.Invoke(false, "Turn manager not available");
                return;
            }

            LogDebug($"Submitting action: {action.actionType} for unit {action.actingUnit?.name}");

            // TEMP: Local testing mode - submit directly to turn manager
            bool isNetworkConnected = NetworkingManager.Instance != null && 
                                     NetworkingManager.Instance.IsAuthenticated();
            
            if (!isNetworkConnected)
            {
                LogDebug("Network not available - using local turn manager");
                // Submit directly to local turn manager for testing
                bool success = turnManager.SubmitPlayerAction(1, action); // Assume player 1 for local testing
                if (success)
                {
                    OnCommandResult?.Invoke(true, $"Action submitted locally: {action.actionType}");
                    LogDebug("Action submitted successfully to local turn manager");
                }
                else
                {
                    OnCommandResult?.Invoke(false, "Local turn manager rejected action");
                    LogError("Local turn manager rejected the action");
                }
            }
            else
            {
                // TODO: Submit via NetworkingManager for multiplayer
                LogDebug("Network available - would submit to server (not implemented yet)");
                OnCommandResult?.Invoke(true, $"Action submitted: {action.actionType}");
                LogDebug("Action submitted successfully");
            }
        }

        #endregion

        #region State Validation

        private bool CanSelectUnits()
        {
            if (turnManager == null) return false;
            
            // Can select units during action selection phase
            return turnManager.CurrentPhase == V3TurnPhase.ActionSelection;
        }

        private bool CanIssueCommands()
        {
            if (turnManager == null) return false;
            
            // Can issue commands during action selection phase
            return turnManager.CurrentPhase == V3TurnPhase.ActionSelection;
        }

        #endregion

        #region Turn System Events

        private void OnTurnStarted(int turnNumber)
        {
            LogDebug($"Turn {turnNumber} started - resetting action state");
            currentActionState = ActionState.Idle;
            targetingAbilityIndex = -1;
            
            // Keep unit selected if it's still valid
            if (selectedUnit != null && selectedUnit.GetCurrentHealth() <= 0)
            {
                DeselectCurrentUnit();
            }
        }

        private void OnPhaseChanged(V3TurnPhase newPhase)
        {
            LogDebug($"Turn phase changed to: {newPhase}");
            
            // Cancel any ongoing targeting when phase changes
            if (newPhase != V3TurnPhase.ActionSelection)
            {
                CancelAbilityTargeting();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force deselect the current unit
        /// </summary>
        public void DeselectUnit()
        {
            DeselectCurrentUnit();
        }

        /// <summary>
        /// Get the currently selected unit
        /// </summary>
        public DokkaebiUnit GetSelectedUnit()
        {
            return selectedUnit;
        }

        /// <summary>
        /// Check if in targeting mode
        /// </summary>
        public bool IsInTargetingMode()
        {
            return currentActionState == ActionState.SelectingAbilityTarget || 
                   currentActionState == ActionState.SelectingZoneDestination;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                SmartLogger.Log($"[PlayerActionManager] {message}", LogCategory.General, this);
            }
        }

        private void LogWarning(string message)
        {
            SmartLogger.LogWarning($"[PlayerActionManager] {message}", LogCategory.General, this);
        }

        private void LogError(string message)
        {
            SmartLogger.LogError($"[PlayerActionManager] {message}", LogCategory.General, this);
        }

        #endregion
    }
} 
