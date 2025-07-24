using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dokkaebi.Core;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.TurnSystem;

namespace Dokkaebi.UI
{
    public class TurnPhaseUI : MonoBehaviour
    {
        [Header("Phase Display")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI turnNumberText;
        [SerializeField] private TextMeshProUGUI activePlayerText;
        // [SerializeField] private TextMeshProUGUI phaseTimerText; // Replaced by icon + seconds text

        [Header("Phase Timer Visuals")]
        [SerializeField] private Image phaseTimerIconImage;
        [SerializeField] private TextMeshProUGUI phaseTimerSecondsText;

        [Header("Phase Icons")]
        [SerializeField] private GameObject[] phaseIcons;
        [SerializeField] private Color activePhaseColor = Color.yellow;
        [SerializeField] private Color inactivePhaseColor = Color.gray;

        // Replace ITurnSystem with V3TurnManager
        private V3TurnManager turnManager;

        private void Awake()
        {
            turnManager = FindFirstObjectByType<V3TurnManager>();
        }

        private void OnEnable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.AddListener(HandleTurnStarted);
                turnManager.OnPhaseChanged.AddListener(HandlePhaseChanged);
            }
        }

        private void Start()
        {
            // Display initial state when UI becomes active
            if (turnManager != null)
            {
                // Get initial state from turn system
                int initialTurn = turnManager.CurrentTurnNumber;
                HandleTurnStarted(initialTurn);
            }
        }

        private void Update()
        {
            if (turnManager != null && turnManager.CurrentPhase == V3TurnPhase.ActionSelection)
            {
                UpdatePhaseTimer(turnManager.RemainingSelectionTime);
            }
            else
            {
                // Hide timer outside selection phase
                if (phaseTimerIconImage != null) phaseTimerIconImage.gameObject.SetActive(false);
                if (phaseTimerSecondsText != null) phaseTimerSecondsText.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.RemoveListener(HandleTurnStarted);
                turnManager.OnPhaseChanged.RemoveListener(HandlePhaseChanged);
            }
        }

        private void HandleTurnStarted(int turnNumber)
        {
            if (turnNumberText != null)
            {
                turnNumberText.text = $"Round {turnNumber}";
            }
            // Hide active player text as it's simultaneous
            if (activePlayerText != null) activePlayerText.gameObject.SetActive(false);
            // Hide phase icons
            foreach (var icon in phaseIcons) icon.SetActive(false);
        }

        private void HandlePhaseChanged(V3TurnPhase phase)
        {
            // Only show timer during ActionSelection
            bool showTimer = phase == V3TurnPhase.ActionSelection;
            if (phaseTimerIconImage != null) phaseTimerIconImage.gameObject.SetActive(showTimer);
            if (phaseTimerSecondsText != null) phaseTimerSecondsText.gameObject.SetActive(showTimer);
            if (phaseText != null) phaseText.text = ""; // Hide phase name
        }

        private void UpdatePhaseTimer(float remainingTime)
        {
            // Check if the new UI elements are assigned
            bool visualsAssigned = phaseTimerIconImage != null && phaseTimerSecondsText != null;

            if (visualsAssigned)
            {
                if (remainingTime > 0)
                {
                    int seconds = Mathf.CeilToInt(remainingTime);
                    phaseTimerSecondsText.text = $"{seconds}s"; // Set only seconds
                    
                    // Color the timer text based on remaining time
                    if (seconds <= 5)
                    {
                        // Flash red for last 5 seconds
                        float flash = Mathf.PingPong(Time.time * 4, 1);
                        phaseTimerSecondsText.color = Color.Lerp(Color.red, Color.white, flash);
                    }
                    else if (seconds <= 10)
                    {
                        phaseTimerSecondsText.color = Color.yellow;
                    }
                    else
                    {
                        phaseTimerSecondsText.color = Color.white;
                    }
                    
                    // Show icon and text
                    phaseTimerIconImage.gameObject.SetActive(true);
                    phaseTimerSecondsText.gameObject.SetActive(true);
                }
                else
                {
                    // Hide icon and text
                    phaseTimerIconImage.gameObject.SetActive(false);
                    phaseTimerSecondsText.gameObject.SetActive(false);
                }
            }
        }
    }
} 

