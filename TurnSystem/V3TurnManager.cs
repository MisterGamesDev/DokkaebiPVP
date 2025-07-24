using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Dokkaebi.Interfaces;
using Dokkaebi.Core;

namespace Dokkaebi.TurnSystem
{
    /// <summary>
    /// Manages the V3 simplified simultaneous turn system.
    /// Each turn, both players select one action for one unit, then actions resolve simultaneously.
    /// </summary>
    public class V3TurnManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private ActionResolutionSystem actionResolutionSystem;
        [SerializeField] private GameController gameController;
        [SerializeField] private UnitManager unitManager;
        
        [Header("Turn State")]
        [SerializeField] private int currentTurnNumber = 1;
        [SerializeField] private V3TurnPhase currentPhase = V3TurnPhase.ActionSelection;
        [SerializeField] private float actionSelectionTimeLimit = 30f; // seconds
        
        [Header("Player Actions")]
        [SerializeField] private PlayerAction player1Action;
        [SerializeField] private PlayerAction player2Action;
        [SerializeField] private bool player1ActionLocked = false;
        [SerializeField] private bool player2ActionLocked = false;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;
        
        // Events
        [Header("Events")]
        public UnityEvent<int> OnTurnStarted;
        public UnityEvent<V3TurnPhase> OnPhaseChanged;
        public UnityEvent<PlayerAction, PlayerAction> OnActionsRevealed;
        public UnityEvent OnTurnCompleted;
        public UnityEvent<string> OnGameEnded;
        
        private float actionSelectionTimer;
        
        /// <summary>
        /// Current turn phase
        /// </summary>
        public V3TurnPhase CurrentPhase => currentPhase;
        
        /// <summary>
        /// Current turn number
        /// </summary>
        public int CurrentTurnNumber => currentTurnNumber;
        
        /// <summary>
        /// Whether both players have locked in their actions
        /// </summary>
        public bool BothActionsLocked => player1ActionLocked && player2ActionLocked;
        
        /// <summary>
        /// Gets the remaining time for action selection
        /// </summary>
        public float RemainingSelectionTime => actionSelectionTimer;
        
        /// <summary>
        /// Starts a new turn
        /// </summary>
        public void StartNewTurn()
        {
            LogDebug($"Starting turn {currentTurnNumber}");
            
            // Reset turn state
            ResetTurnState();
            
            // Set phase to action selection
            SetPhase(V3TurnPhase.ActionSelection);
            
            // Start selection timer
            actionSelectionTimer = actionSelectionTimeLimit;
            
            // Notify listeners
            OnTurnStarted?.Invoke(currentTurnNumber);
        }
        
        /// <summary>
        /// Submits an action for a player
        /// </summary>
        public bool SubmitPlayerAction(int playerId, PlayerAction action)
        {
            if (currentPhase != V3TurnPhase.ActionSelection)
            {
                LogDebug($"Cannot submit action - not in action selection phase");
                return false;
            }
            
            if (action == null)
            {
                LogDebug($"Cannot submit null action for player {playerId}");
                return false;
            }
            
            switch (playerId)
            {
                case 1:
                    if (player1ActionLocked)
                    {
                        LogDebug("Player 1 action already locked");
                        return false;
                    }
                    player1Action = action;
                    player1ActionLocked = true;
                    LogDebug($"Player 1 action locked: {action.actionType} with {action.actingUnit?.name}");
                    break;
                    
                case 2:
                    if (player2ActionLocked)
                    {
                        LogDebug("Player 2 action already locked");
                        return false;
                    }
                    player2Action = action;
                    player2ActionLocked = true;
                    LogDebug($"Player 2 action locked: {action.actionType} with {action.actingUnit?.name}");
                    break;
                    
                default:
                    LogDebug($"Invalid player ID: {playerId}");
                    return false;
            }
            
            // Check if both actions are now locked
            if (BothActionsLocked)
            {
                ProceedToResolution();
            }
            
            return true;
        }
        
        /// <summary>
        /// Forces the turn to proceed even if not all actions are locked (for timeout)
        /// </summary>
        public void ForceResolution()
        {
            LogDebug("Forcing turn resolution due to timeout or manual trigger");
            
            // Create default actions for players who didn't submit
            if (!player1ActionLocked)
            {
                LogDebug("Player 1 did not submit action - skipping turn");
                player1Action = null;
            }
            
            if (!player2ActionLocked)
            {
                LogDebug("Player 2 did not submit action - skipping turn");
                player2Action = null;
            }
            
            ProceedToResolution();
        }
        
        /// <summary>
        /// Proceeds to the resolution phase
        /// </summary>
        private void ProceedToResolution()
        {
            SetPhase(V3TurnPhase.ActionResolution);
            
            // Reveal actions to players
            OnActionsRevealed?.Invoke(player1Action, player2Action);
            
            // Collect valid actions
            var actionsToResolve = new List<PlayerAction>();
            if (player1Action != null) actionsToResolve.Add(player1Action);
            if (player2Action != null) actionsToResolve.Add(player2Action);
            
            // Resolve actions
            if (actionResolutionSystem != null)
            {
                actionResolutionSystem.ResolveActions(actionsToResolve);
            }
            else
            {
                Debug.LogError("ActionResolutionSystem not found!");
            }
            
            // Check for immediate game end after action resolution
            if (gameController != null)
            {
                gameController.CheckWinLossConditions();
            }
            
            // Complete the turn
            CompleteTurn();
        }
        
        /// <summary>
        /// Completes the current turn and prepares for the next
        /// </summary>
        private void CompleteTurn()
        {
            SetPhase(V3TurnPhase.TurnComplete);
            
            LogDebug($"Turn {currentTurnNumber} completed");
            OnTurnCompleted?.Invoke();
            
            // Process end-of-turn effects
            ProcessEndOfTurnEffects();
            
            // Check for game end conditions
            if (CheckGameEndConditions())
            {
                return; // Game ended, don't start next turn
            }
            
            // Prepare for next turn
            currentTurnNumber++;
            
            // Auto-start next turn after a brief delay
            Invoke(nameof(StartNewTurn), 2f);
        }
        
        /// <summary>
        /// Process end-of-turn effects including cooldown reduction
        /// </summary>
        private void ProcessEndOfTurnEffects()
        {
            LogDebug("Processing end-of-turn effects");
            
            if (unitManager == null)
            {
                LogDebug("UnitManager not found, skipping end-of-turn processing");
                return;
            }
            
            // Get all alive units and reduce their cooldowns
            var allUnits = unitManager.GetAliveUnits();
            foreach (var unit in allUnits)
            {
                if (unit != null)
                {
                    LogDebug($"Reducing cooldowns for unit: {unit.GetUnitName()}");
                    unit.ReduceCooldowns();
                    
                    // Also process any other end-of-turn effects
                    unit.UpdateCooldowns(); // This might be redundant with ReduceCooldowns, but ensures consistency
                }
            }
            
            LogDebug("End-of-turn effects processing complete");
        }
        
        /// <summary>
        /// Checks if the game should end
        /// </summary>
        private bool CheckGameEndConditions()
        {
            // Check with GameController for win/lose conditions
            if (gameController != null && gameController.IsGameOver())
            {
                int winner = gameController.GetWinner();
                string winnerText = winner == 1 ? "Player 1" : "Player 2";
                EndGame($"{winnerText} wins!");
                return true;
            }
            
            // Fallback: check if we've reached a maximum turn limit
            const int maxTurns = 100;
            if (currentTurnNumber >= maxTurns)
            {
                EndGame("Turn limit reached - Draw!");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Ends the game
        /// </summary>
        private void EndGame(string reason)
        {
            LogDebug($"Game ended: {reason}");
            SetPhase(V3TurnPhase.GameEnded);
            OnGameEnded?.Invoke(reason);
        }
        
        /// <summary>
        /// Sets the current phase and notifies listeners
        /// </summary>
        private void SetPhase(V3TurnPhase newPhase)
        {
            if (currentPhase != newPhase)
            {
                currentPhase = newPhase;
                LogDebug($"Phase changed to: {newPhase}");
                OnPhaseChanged?.Invoke(newPhase);
            }
        }
        
        /// <summary>
        /// Resets the turn state for a new turn
        /// </summary>
        private void ResetTurnState()
        {
            player1Action = null;
            player2Action = null;
            player1ActionLocked = false;
            player2ActionLocked = false;
            actionSelectionTimer = actionSelectionTimeLimit;
        }
        
        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[V3TurnManager] {message}");
            }
        }
        
        /// <summary>
        /// Initialize dependencies
        /// </summary>
        private void Awake()
        {
            if (actionResolutionSystem == null)
                actionResolutionSystem = FindFirstObjectByType<ActionResolutionSystem>();
                
            if (gameController == null)
                gameController = FindFirstObjectByType<GameController>();
                
            if (unitManager == null)
                unitManager = FindFirstObjectByType<UnitManager>();
        }
        
        /// <summary>
        /// Start the first turn when the game begins
        /// </summary>
        private void Start()
        {
            StartNewTurn();
        }
        
        /// <summary>
        /// Handle action selection timer
        /// </summary>
        private void Update()
        {
            if (currentPhase == V3TurnPhase.ActionSelection && !BothActionsLocked)
            {
                actionSelectionTimer -= Time.deltaTime;
                
                if (actionSelectionTimer <= 0f)
                {
                    LogDebug("Action selection time limit reached");
                    ForceResolution();
                }
            }
        }
    }
    
    /// <summary>
    /// Phases of the V3 turn system
    /// </summary>
    public enum V3TurnPhase
    {
        ActionSelection,    // Players are selecting their actions
        ActionResolution,   // Actions are being resolved
        TurnComplete,       // Turn is complete, preparing for next
        GameEnded          // Game has ended
    }
} 