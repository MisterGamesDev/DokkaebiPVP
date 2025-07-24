using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Dokkaebi.TurnSystem;
using Dokkaebi.Units;
using Dokkaebi.Core.Data;
using Dokkaebi.Grid;
using Dokkaebi.Core;

namespace Dokkaebi.UI
{
    /// <summary>
    /// UI component for selecting actions in the V3 turn system
    /// </summary>
    public class V3ActionSelectionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject actionSelectionPanel;
        [SerializeField] private TextMeshProUGUI turnNumberText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI selectedUnitText;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button abilityButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Transform abilityButtonContainer;
        [SerializeField] private GameObject abilityButtonPrefab;
        
        [Header("Dependencies")]
        [SerializeField] private V3TurnManager turnManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private InputManager inputManager;
        
        [Header("State")]
        [SerializeField] private DokkaebiUnit selectedUnit;
        [SerializeField] private ActionType selectedActionType;
        [SerializeField] private AbilityData selectedAbility;
        [SerializeField] private Vector2Int targetPosition;
        [SerializeField] private bool actionLocked = false;
        [SerializeField] private bool isTargetingGrid = false;
        
        private List<DokkaebiUnit> playerUnits = new List<DokkaebiUnit>();
        private List<Button> abilityButtons = new List<Button>();
        
        /// <summary>
        /// Initialize the UI
        /// </summary>
        private void Start()
        {
            SetupEventListeners();
            RefreshPlayerUnits();
            
            // Find dependencies if not assigned
            if (inputManager == null)
                inputManager = FindFirstObjectByType<InputManager>();
            
            // Hide panel initially
            if (actionSelectionPanel != null)
                actionSelectionPanel.SetActive(false);
        }
        
        /// <summary>
        /// Setup event listeners
        /// </summary>
        private void SetupEventListeners()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.AddListener(OnTurnStarted);
                turnManager.OnPhaseChanged.AddListener(OnPhaseChanged);
            }
            
            if (moveButton != null)
                moveButton.onClick.AddListener(() => SelectActionType(ActionType.Move));
                
            if (abilityButton != null)
                abilityButton.onClick.AddListener(() => SelectActionType(ActionType.UseAbility));
                
            if (confirmButton != null)
                confirmButton.onClick.AddListener(ConfirmAction);
        }
        
        /// <summary>
        /// Called when a new turn starts
        /// </summary>
        private void OnTurnStarted(int turnNumber)
        {
            if (turnNumberText != null)
                turnNumberText.text = $"Turn {turnNumber}";
                
            ResetActionSelection();
            RefreshPlayerUnits();
            
            // Refresh ability buttons if a unit is selected to show updated cooldowns
            if (selectedUnit != null && selectedActionType == ActionType.UseAbility)
            {
                ShowAbilitySelection();
            }
        }
        
        /// <summary>
        /// Called when the turn phase changes
        /// </summary>
        private void OnPhaseChanged(V3TurnPhase phase)
        {
            switch (phase)
            {
                case V3TurnPhase.ActionSelection:
                    ShowActionSelection();
                    break;
                    
                case V3TurnPhase.ActionResolution:
                case V3TurnPhase.TurnComplete:
                    HideActionSelection();
                    break;
            }
        }
        
        /// <summary>
        /// Show the action selection UI
        /// </summary>
        private void ShowActionSelection()
        {
            if (actionSelectionPanel != null)
                actionSelectionPanel.SetActive(true);
                
            actionLocked = false;
            UpdateUI();
        }
        
        /// <summary>
        /// Hide the action selection UI
        /// </summary>
        private void HideActionSelection()
        {
            if (actionSelectionPanel != null)
                actionSelectionPanel.SetActive(false);
        }
        
        /// <summary>
        /// Refresh the list of player units
        /// </summary>
        private void RefreshPlayerUnits()
        {
            playerUnits.Clear();
            var allUnits = FindObjectsByType<DokkaebiUnit>(FindObjectsSortMode.None);
            playerUnits.AddRange(allUnits.Where(unit => unit.IsPlayerControlled && unit.IsAlive));
        }
        
        /// <summary>
        /// Select a unit for action
        /// </summary>
        public void SelectUnit(DokkaebiUnit unit)
        {
            if (actionLocked || unit == null || !unit.IsAlive || !unit.IsPlayerControlled)
                return;
                
            selectedUnit = unit;
            UpdateUI();
        }
        
        /// <summary>
        /// Select an action type
        /// </summary>
        private void SelectActionType(ActionType actionType)
        {
            if (actionLocked || selectedUnit == null)
                return;
                
            selectedActionType = actionType;
            
            if (actionType == ActionType.UseAbility)
            {
                ShowAbilitySelection();
            }
            else
            {
                selectedAbility = null;
                // Start grid targeting for move actions
                if (actionType == ActionType.Move)
                {
                    StartGridTargeting();
                }
            }
            
            UpdateUI();
        }
        
        /// <summary>
        /// Show ability selection buttons
        /// </summary>
        private void ShowAbilitySelection()
        {
            ClearAbilityButtons();
            
            if (selectedUnit == null || abilityButtonContainer == null || abilityButtonPrefab == null)
                return;
                
            var abilities = selectedUnit.GetAbilities();
            
            foreach (var ability in abilities)
            {
                var buttonObj = Instantiate(abilityButtonPrefab, abilityButtonContainer);
                var button = buttonObj.GetComponent<Button>();
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                
                bool canUse = selectedUnit.CanUseAbility(ability);
                int cooldownRemaining = selectedUnit.GetRemainingCooldown(ability.abilityId);
                
                if (text != null)
                {
                    string buttonText = ability.displayName;
                    if (cooldownRemaining > 0)
                    {
                        buttonText += $" ({cooldownRemaining})";
                    }
                    text.text = buttonText;
                    
                    // Change text color based on availability
                    text.color = canUse ? Color.white : Color.gray;
                }
                    
                if (button != null)
                {
                    button.interactable = canUse;
                    if (canUse)
                    {
                        button.onClick.AddListener(() => SelectAbility(ability));
                    }
                    abilityButtons.Add(button);
                }
            }
        }
        
        /// <summary>
        /// Clear ability selection buttons
        /// </summary>
        private void ClearAbilityButtons()
        {
            foreach (var button in abilityButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }
            abilityButtons.Clear();
        }
        
        /// <summary>
        /// Select a specific ability
        /// </summary>
        private void SelectAbility(AbilityData ability)
        {
            if (actionLocked)
                return;
                
            selectedAbility = ability;
            
            // Start grid targeting for abilities
            if (ability != null)
            {
                StartGridTargeting();
            }
            
            UpdateUI();
        }
        
        /// <summary>
        /// Start grid targeting mode
        /// </summary>
        private void StartGridTargeting()
        {
            isTargetingGrid = true;
            Debug.Log($"[V3ActionSelectionUI] Started grid targeting for {selectedActionType}");
            
            // Subscribe to input events
            if (inputManager != null)
            {
                inputManager.OnGridCoordHovered += OnGridHovered;
            }
        }
        
        /// <summary>
        /// Stop grid targeting mode
        /// </summary>
        private void StopGridTargeting()
        {
            isTargetingGrid = false;
            
            // Unsubscribe from input events
            if (inputManager != null)
            {
                inputManager.OnGridCoordHovered -= OnGridHovered;
            }
        }
        
        /// <summary>
        /// Handle grid coordinate being hovered
        /// </summary>
        private void OnGridHovered(Vector2Int? gridCoord)
        {
            if (!isTargetingGrid || !gridCoord.HasValue)
                return;
                
            // Update target position preview
            targetPosition = gridCoord.Value;
        }
        
        /// <summary>
        /// Handle grid coordinate being clicked (called by Update method)
        /// </summary>
        private void HandleGridClick()
        {
            if (!isTargetingGrid || !Input.GetMouseButtonDown(0))
                return;
                
            // Raycast to get clicked position
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                if (gridManager != null)
                {
                    var gridPos = gridManager.WorldToGridPosition(hit.point);
                    targetPosition = new Vector2Int(gridPos.x, gridPos.z);
                    
                    Debug.Log($"[V3ActionSelectionUI] Selected target position: {targetPosition}");
                    
                    // Stop targeting and enable confirm button
                    StopGridTargeting();
                    UpdateUI();
                }
            }
        }
        
        /// <summary>
        /// Confirm the selected action
        /// </summary>
        private void ConfirmAction()
        {
            if (actionLocked || selectedUnit == null || turnManager == null)
                return;
                
            PlayerAction action = null;
            
            switch (selectedActionType)
            {
                case ActionType.Move:
                    // Use the selected target position
                    action = PlayerAction.CreateMoveAction(selectedUnit, targetPosition);
                    break;
                    
                case ActionType.UseAbility:
                    if (selectedAbility != null)
                    {
                        // Double-check that the ability can still be used (cooldown might have changed)
                        if (!selectedUnit.CanUseAbility(selectedAbility))
                        {
                            Debug.LogWarning($"[V3ActionSelectionUI] Cannot confirm ability {selectedAbility.displayName}: ability no longer usable");
                            
                            // Refresh the ability selection to show updated state
                            ShowAbilitySelection();
                            return;
                        }
                        
                        // Use the selected target position for abilities
                        action = PlayerAction.CreateAbilityAction(selectedUnit, selectedAbility, targetPosition);
                    }
                    break;
            }
            
            if (action != null)
            {
                bool success = turnManager.SubmitPlayerAction(1, action); // Assuming player 1
                if (success)
                {
                    actionLocked = true;
                    StopGridTargeting(); // Ensure targeting is stopped
                    UpdateUI();
                }
                else
                {
                    Debug.LogWarning("[V3ActionSelectionUI] Failed to submit action to turn manager");
                }
            }
        }
        
        /// <summary>
        /// Reset action selection for new turn
        /// </summary>
        private void ResetActionSelection()
        {
            selectedUnit = null;
            selectedActionType = ActionType.Move;
            selectedAbility = null;
            targetPosition = Vector2Int.zero;
            actionLocked = false;
            StopGridTargeting();
            ClearAbilityButtons();
        }
        
        /// <summary>
        /// Update the UI display
        /// </summary>
        private void UpdateUI()
        {
            // Update selected unit text
            if (selectedUnitText != null)
            {
                string unitText = selectedUnit != null ? $"Selected: {selectedUnit.GetUnitName()}" : "Select a unit";
                
                if (isTargetingGrid)
                {
                    string actionText = selectedActionType == ActionType.Move ? "Move" : 
                                       selectedAbility != null ? selectedAbility.displayName : "Ability";
                    unitText += $"\nTargeting: {actionText} - Click on grid";
                }
                else if (selectedActionType != ActionType.Move && targetPosition != Vector2Int.zero)
                {
                    unitText += $"\nTarget: ({targetPosition.x}, {targetPosition.y})";
                }
                
                selectedUnitText.text = unitText;
            }
            
            // Update button states
            if (moveButton != null)
                moveButton.interactable = !actionLocked && selectedUnit != null && !isTargetingGrid;
                
            if (abilityButton != null)
                abilityButton.interactable = !actionLocked && selectedUnit != null && !isTargetingGrid &&
                    selectedUnit.GetAbilities().Any(a => selectedUnit.CanUseAbility(a));
                    
            if (confirmButton != null)
            {
                bool hasValidTarget = targetPosition != Vector2Int.zero;
                bool canConfirm = !actionLocked && selectedUnit != null && !isTargetingGrid && hasValidTarget &&
                    (selectedActionType == ActionType.Move || 
                     (selectedActionType == ActionType.UseAbility && selectedAbility != null));
                confirmButton.interactable = canConfirm;
            }
        }
        
        /// <summary>
        /// Update timer display and handle grid targeting
        /// </summary>
        private void Update()
        {
            if (timerText != null && turnManager != null)
            {
                // Use actual remaining time from V3TurnManager
                if (turnManager.CurrentPhase == V3TurnPhase.ActionSelection)
                {
                    int remainingSeconds = Mathf.CeilToInt(turnManager.RemainingSelectionTime);
                    timerText.text = $"{remainingSeconds}s";
                    
                    // Color the timer based on remaining time
                    if (remainingSeconds <= 5)
                    {
                        // Flash red for last 5 seconds
                        float flash = Mathf.PingPong(Time.time * 4, 1);
                        timerText.color = Color.Lerp(Color.red, Color.white, flash);
                    }
                    else if (remainingSeconds <= 10)
                    {
                        timerText.color = Color.yellow;
                    }
                    else
                    {
                        timerText.color = Color.white;
                    }
                }
                else
                {
                    timerText.text = "";
                }
            }
            
            // Handle grid click input
            HandleGridClick();
        }
        
        /// <summary>
        /// Cleanup
        /// </summary>
        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.RemoveListener(OnTurnStarted);
                turnManager.OnPhaseChanged.RemoveListener(OnPhaseChanged);
            }
            
            // Ensure we stop grid targeting
            StopGridTargeting();
        }
    }
} 