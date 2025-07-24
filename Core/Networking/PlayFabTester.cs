using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dokkaebi.Utilities;
using Dokkaebi.Common;

namespace Dokkaebi.Core.Networking
{
    /// <summary>
    /// Enhanced utility class for testing PlayFab integration
    /// Tests connection, match creation, and command execution
    /// </summary>
    public class PlayFabTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkingManager networkManager;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI connectionInfoText;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button createMatchButton;
        [SerializeField] private Button testCommandButton;
        [SerializeField] private Button getGameStateButton;

        [Header("Test Settings")]
        [SerializeField] private bool autoLoginOnStart = true;
        [SerializeField] private float statusUpdateInterval = 1.0f;

        private GameStateManager gameStateManager;
        private Coroutine statusUpdateCoroutine;

        private void Awake()
        {
            if (networkManager == null)
                networkManager = FindFirstObjectByType<NetworkingManager>();
        }

        private void Start()
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();

            // Subscribe to network events
            SubscribeToEvents();
            
            // Set up button listeners
            SetupButtonListeners();

            // Initial UI state
            UpdateButtonStates();
            UpdateStatusText("PlayFab Tester initialized");

            // Start status update coroutine
            statusUpdateCoroutine = StartCoroutine(UpdateStatusPeriodically());

            // Auto login if enabled
            if (autoLoginOnStart && networkManager != null)
            {
                UpdateStatusText("Auto-login enabled, attempting connection...");
                networkManager.LoginWithDeviceId();
            }
        }

        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnLoginSuccess += HandleLoginSuccess;
                networkManager.OnLoginFailure += HandleLoginFailure;
                networkManager.OnNetworkError += HandleNetworkError;
                networkManager.OnGameStateUpdated += HandleGameStateUpdate;
                networkManager.OnConnectionStatusChanged += HandleConnectionStatusChanged;
            }
            }

        private void SetupButtonListeners()
        {
            if (loginButton != null)
                loginButton.onClick.AddListener(HandleLoginButtonClick);

            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(HandleDisconnectButtonClick);

            if (createMatchButton != null)
                createMatchButton.onClick.AddListener(HandleCreateMatchButtonClick);

            if (testCommandButton != null)
                testCommandButton.onClick.AddListener(HandleTestCommandClick);

            if (getGameStateButton != null)
                getGameStateButton.onClick.AddListener(HandleGetGameStateClick);
        }

        private void OnDestroy()
        {
            // Stop coroutine
            if (statusUpdateCoroutine != null)
            {
                StopCoroutine(statusUpdateCoroutine);
            }

            // Unsubscribe from events
            if (networkManager != null)
            {
                networkManager.OnLoginSuccess -= HandleLoginSuccess;
                networkManager.OnLoginFailure -= HandleLoginFailure;
                networkManager.OnNetworkError -= HandleNetworkError;
                networkManager.OnGameStateUpdated -= HandleGameStateUpdate;
                networkManager.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            }

            // Remove button listeners
            RemoveButtonListeners();
        }

        private void RemoveButtonListeners()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(HandleLoginButtonClick);

            if (disconnectButton != null)
                disconnectButton.onClick.RemoveListener(HandleDisconnectButtonClick);

            if (createMatchButton != null)
                createMatchButton.onClick.RemoveListener(HandleCreateMatchButtonClick);

            if (testCommandButton != null)
                testCommandButton.onClick.RemoveListener(HandleTestCommandClick);

            if (getGameStateButton != null)
                getGameStateButton.onClick.RemoveListener(HandleGetGameStateClick);
        }

        #region Button Handlers

        private void HandleLoginButtonClick()
        {
            if (networkManager != null)
            {
                UpdateStatusText("Attempting to login to PlayFab...");
                networkManager.LoginWithDeviceId();
            }
            else
            {
                UpdateStatusText("ERROR: NetworkingManager not found!");
            }
        }

        private void HandleDisconnectButtonClick()
        {
            if (networkManager != null)
            {
                networkManager.Disconnect();
                UpdateStatusText("Disconnected from PlayFab");
                UpdateButtonStates();
            }
        }

        private void HandleCreateMatchButtonClick()
        {
            if (networkManager != null && networkManager.IsAuthenticated())
            {
                UpdateStatusText("Creating new match...");
                networkManager.CreateMatch(
                    matchId => {
                        UpdateStatusText($"Match created successfully: {matchId}");
                        UpdateButtonStates();
                    },
                    error => {
                        UpdateStatusText($"Failed to create match: {error}");
                    }
                );
            }
            else
            {
                UpdateStatusText("ERROR: Not connected to PlayFab!");
            }
        }

        private void HandleTestCommandClick()
        {
            if (networkManager != null && networkManager.IsAuthenticated())
            {
                UpdateStatusText("Testing command execution...");
                
                // Test the HealthCheck function
                networkManager.ExecuteCloudScript(
                    "HealthCheck",
                    new { testParam = "Hello from Dokkaebi Tester!", timestamp = System.DateTime.UtcNow.ToString() },
                    result => {
                        UpdateStatusText("HealthCheck executed successfully!");
                        SmartLogger.Log($"[PlayFabTester] HealthCheck result: {result.FunctionResult}", LogCategory.Networking, this);
                    },
                    error => {
                        UpdateStatusText($"HealthCheck ERROR: {error.ErrorMessage}");
                        SmartLogger.LogError($"[PlayFabTester] HealthCheck error: {error.ErrorMessage}", LogCategory.Networking, this);
                    }
                );
            }
            else
            {
                UpdateStatusText("ERROR: Not authenticated with PlayFab!");
            }
        }

        private void HandleGetGameStateClick()
        {
            if (networkManager != null && networkManager.IsAuthenticated())
            {
                string currentMatch = networkManager.GetCurrentMatchId();
                if (string.IsNullOrEmpty(currentMatch))
                {
                    UpdateStatusText("ERROR: No active match. Create a match first.");
                    return;
                }

                UpdateStatusText("Fetching game state...");
                networkManager.GetGameState(gameState => {
                    UpdateStatusText($"Game state received with {gameState.Count} properties");
                    SmartLogger.Log($"[PlayFabTester] Game state keys: {string.Join(", ", gameState.Keys)}", LogCategory.Networking, this);
                });
            }
            else
            {
                UpdateStatusText("ERROR: Not authenticated with PlayFab!");
            }
        }

        #endregion

        #region Event Handlers

        private void HandleLoginSuccess()
        {
            UpdateStatusText("✅ PlayFab login SUCCESS!");
            UpdateButtonStates();
            
            SmartLogger.Log("[PlayFabTester] Login successful, connection established", LogCategory.Networking, this);
        }

        private void HandleLoginFailure(string error)
        {
            UpdateStatusText($"❌ PlayFab login FAILED: {error}");
            UpdateButtonStates();
            
            SmartLogger.LogError($"[PlayFabTester] Login failed: {error}", LogCategory.Networking, this);
        }

        private void HandleNetworkError(string error)
        {
            UpdateStatusText($"🔥 Network ERROR: {error}");
            SmartLogger.LogError($"[PlayFabTester] Network error: {error}", LogCategory.Networking, this);
        }

        private void HandleGameStateUpdate(Dictionary<string, object> gameState)
        {
            UpdateStatusText($"🔄 Game state updated! ({gameState.Count} properties)");
            SmartLogger.Log($"[PlayFabTester] Received game state with keys: {string.Join(", ", gameState.Keys)}", LogCategory.Networking, this);
        }

        private void HandleConnectionStatusChanged(string statusMessage)
        {
            SmartLogger.Log($"[PlayFabTester] {statusMessage}", LogCategory.Networking, this);
        }

        #endregion

        #region UI Updates

        private void UpdateStatusText(string message)
        {
            string timestampedMessage = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            SmartLogger.Log($"[PlayFabTester] {message}", LogCategory.Networking, this);
            
            if (statusText != null)
                statusText.text = timestampedMessage;
        }

        private void UpdateButtonStates()
        {
            bool isAuthenticated = networkManager != null && networkManager.IsAuthenticated();
            bool hasMatch = !string.IsNullOrEmpty(networkManager?.GetCurrentMatchId());

            if (loginButton != null)
                loginButton.interactable = !isAuthenticated;

            if (disconnectButton != null)
                disconnectButton.interactable = isAuthenticated;

            if (createMatchButton != null)
                createMatchButton.interactable = isAuthenticated;

            if (testCommandButton != null)
                testCommandButton.interactable = isAuthenticated;

            if (getGameStateButton != null)
                getGameStateButton.interactable = isAuthenticated && hasMatch;
        }

        private IEnumerator UpdateStatusPeriodically()
        {
            while (true)
            {
                yield return new WaitForSeconds(statusUpdateInterval);
                
                if (connectionInfoText != null && networkManager != null)
                {
                    string connectionInfo = networkManager.GetConnectionInfo();
                    connectionInfoText.text = $"Connection Info:\n{connectionInfo}";
                }
            }
        }

        #endregion

        #region Test Utilities

        /// <summary>
        /// Test ability command submission (for integration testing)
        /// </summary>
        public void TestAbilityCommand()
        {
            if (networkManager == null || !networkManager.IsAuthenticated())
            {
                UpdateStatusText("Cannot test ability: Not authenticated");
                return;
            }

            if (string.IsNullOrEmpty(networkManager.GetCurrentMatchId()))
            {
                UpdateStatusText("Cannot test ability: No active match");
                return;
            }

            // Create a test ability command
            var testAbilityCommand = new Dictionary<string, object>
            {
                { "unitId", "test-unit-1" },
                { "abilityIndex", 0 },
                { "targetX", 5 },
                { "targetY", 5 },
                { "playerId", 1 },
                { "abilityType", "TestAbility" }
            };

            UpdateStatusText("Testing ability command execution...");

            networkManager.ExecuteCommand(
                "ExecuteAbility",
                testAbilityCommand,
                response => {
                    UpdateStatusText("✅ Ability command executed successfully!");
                    SmartLogger.Log("[PlayFabTester] Ability command response received", LogCategory.Networking, this);
                },
                error => {
                    UpdateStatusText($"❌ Ability command failed: {error}");
                    SmartLogger.LogError($"[PlayFabTester] Ability command error: {error}", LogCategory.Networking, this);
                }
            );
        }

        /// <summary>
        /// Comprehensive connection test
        /// </summary>
        public void RunConnectionTest()
        {
            StartCoroutine(ConnectionTestSequence());
        }

        private IEnumerator ConnectionTestSequence()
        {
            UpdateStatusText("🔄 Starting connection test sequence...");
            
            // Test 1: Authentication
            UpdateStatusText("Test 1: Testing authentication...");
            bool authPassed = networkManager.IsAuthenticated();
            
            if (!authPassed)
            {
                UpdateStatusText("❌ Test 1 failed: Not authenticated");
                yield break;
            }
            
            UpdateStatusText("✅ Test 1 passed: Authentication successful");
            
            // Test 2: Match creation
            UpdateStatusText("Test 2: Testing match creation...");
            bool matchCreated = false;
            string matchError = null;
            
            networkManager.CreateMatch(
                result => { matchCreated = true; },
                error => { matchError = error; }
            );

            // Wait for match creation
            float timeout = 10f;
            while (!matchCreated && string.IsNullOrEmpty(matchError) && timeout > 0)
        {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }

            if (!matchCreated)
            {
                UpdateStatusText($"❌ Test 2 failed: Match creation failed - {matchError ?? "timeout"}");
                yield break;
            }

            UpdateStatusText("✅ Test 2 passed: Match created successfully");

            // Test 3: Health check
            UpdateStatusText("Test 3: Testing CloudScript execution...");
            bool healthCheckPassed = false;

            networkManager.ExecuteCloudScript(
                "HealthCheck",
                new { test = true },
                result => { healthCheckPassed = true; },
                error => { }
            );

            // Wait for health check
            timeout = 10f;
            while (!healthCheckPassed && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }

            if (!healthCheckPassed)
            {
                UpdateStatusText("❌ Test 3 failed: HealthCheck CloudScript failed");
                yield break;
            }

            UpdateStatusText("✅ All tests passed! PlayFab integration is working correctly.");
        }

        #endregion

        #region Public API for other systems

        /// <summary>
        /// Get current connection status for other systems
        /// </summary>
        public bool IsConnectedToPlayFab()
        {
            return networkManager != null && networkManager.IsAuthenticated();
        }

        /// <summary>
        /// Get current match ID for other systems
        /// </summary>
        public string GetCurrentMatchId()
        {
            return networkManager?.GetCurrentMatchId();
        }

        #endregion
    }
} 
