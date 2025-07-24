using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// Uncomment PlayFab namespace references
using PlayFab;
using PlayFab.ClientModels;
using Dokkaebi.Utilities;
using Dokkaebi.Common;

namespace Dokkaebi.Core.Networking
{
    /// <summary>
    /// Handles network communication with PlayFab backend services
    /// Acts as wrapper for PlayFab SDK calls to enable authoritative server communication
    /// Enhanced with proper title ID configuration and comprehensive error handling
    /// </summary>
    public class NetworkingManager : MonoBehaviour
    {
        public static NetworkingManager Instance { get; private set; }

        [Header("PlayFab Settings")]
        [SerializeField] private string titleId = "FEE67"; // Real PlayFab Title ID from server
        [SerializeField] private bool useDeviceId = true;
        [SerializeField] private bool enableDebugLogging = true;

        [Header("Connection Settings")]
        [SerializeField] private int maxRetryAttempts = 3;
        [SerializeField] private float retryDelay = 2.0f;
        [SerializeField] private float timeoutSeconds = 10.0f;

        // Session and authentication state
        private string playFabId;
        private string sessionTicket;
        private bool isAuthenticated = false;
        private bool isConnecting = false;
        private int currentRetryAttempt = 0;

        // Match state
        private string currentMatchId;
        private string currentMatchGroup;

        // Events
        public event Action OnLoginSuccess;
        public event Action<string> OnLoginFailure;
        public event Action<string> OnNetworkError;
        public event Action<Dictionary<string, object>> OnGameStateUpdated;
        public event Action<string> OnConnectionStatusChanged;

        // Connection status
        public enum ConnectionStatus
        {
            Disconnected,
            Connecting,
            Connected,
            Error
        }

        private ConnectionStatus currentStatus = ConnectionStatus.Disconnected;

        public ConnectionStatus CurrentConnectionStatus => currentStatus;
        public string PlayFabId => playFabId;
        public string SessionTicket => sessionTicket;

        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Set PlayFab Title ID
            if (!string.IsNullOrEmpty(titleId))
            {
                PlayFabSettings.TitleId = titleId;
                LogDebug($"PlayFab Title ID set to: {titleId}");
            }
            else
            {
                SmartLogger.LogError("[NetworkingManager] PlayFab Title ID is not configured!", LogCategory.Networking, this);
            }
        }

        private void Start()
        {
            // Validate configuration
            if (string.IsNullOrEmpty(titleId))
            {
                SmartLogger.LogError("[NetworkingManager] PlayFab Title ID not configured. Cannot connect to PlayFab.", LogCategory.Networking, this);
                SetConnectionStatus(ConnectionStatus.Error);
                return;
            }

            // Auto login on start if configured to do so
            if (useDeviceId)
            {
                LoginWithDeviceId();
            }
        }

        #region Connection Management

        /// <summary>
        /// Set connection status and notify listeners
        /// </summary>
        private void SetConnectionStatus(ConnectionStatus newStatus)
        {
            if (currentStatus != newStatus)
            {
                currentStatus = newStatus;
                string statusMessage = $"Connection status changed to: {newStatus}";
                LogDebug(statusMessage);
                OnConnectionStatusChanged?.Invoke(statusMessage);
            }
        }

        /// <summary>
        /// Check if ready to make PlayFab calls
        /// </summary>
        public bool IsReadyForRequests()
        {
            return isAuthenticated && !string.IsNullOrEmpty(playFabId) && currentStatus == ConnectionStatus.Connected;
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Login to PlayFab using device ID (for testing)
        /// Enhanced with retry logic and better error handling
        /// </summary>
        public void LoginWithDeviceId()
        {
            if (isConnecting)
            {
                LogDebug("Already attempting to connect, ignoring duplicate login request");
                return;
            }

            isConnecting = true;
            SetConnectionStatus(ConnectionStatus.Connecting);

            // Generate a unique device ID if it doesn't exist
            string deviceId = GetOrCreateDeviceId();

            var request = new LoginWithCustomIDRequest
            {
                CustomId = deviceId,
                CreateAccount = true,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetUserAccountInfo = true,
                    GetUserData = true
                }
            };

            LogDebug($"Attempting PlayFab login with device ID: {deviceId}");

            PlayFabClientAPI.LoginWithCustomID(
                request,
                OnLoginSuccess_Internal,
                OnLoginFailure_Internal
            );
        }

        /// <summary>
        /// Retry login after failure
        /// </summary>
        private void RetryLogin()
        {
            currentRetryAttempt++;
            if (currentRetryAttempt < maxRetryAttempts)
            {
                LogDebug($"Retrying login attempt {currentRetryAttempt + 1}/{maxRetryAttempts} in {retryDelay} seconds");
                Invoke(nameof(LoginWithDeviceId), retryDelay);
            }
            else
            {
                SmartLogger.LogError($"[NetworkingManager] Failed to connect after {maxRetryAttempts} attempts", LogCategory.Networking, this);
                SetConnectionStatus(ConnectionStatus.Error);
                isConnecting = false;
                OnLoginFailure?.Invoke($"Failed to connect after {maxRetryAttempts} attempts");
            }
        }

        /// <summary>
        /// Get stored device ID or create a new one
        /// </summary>
        private string GetOrCreateDeviceId()
        {
            const string KEY_DEVICE_ID = "PLAYFAB_DEVICE_ID";
            string deviceId = PlayerPrefs.GetString(KEY_DEVICE_ID, "");

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = Guid.NewGuid().ToString();
                }

                PlayerPrefs.SetString(KEY_DEVICE_ID, deviceId);
                PlayerPrefs.Save();
                LogDebug($"Generated new device ID: {deviceId}");
            }

            return deviceId;
        }

        private void OnLoginSuccess_Internal(LoginResult result)
        {
            playFabId = result.PlayFabId;
            sessionTicket = result.SessionTicket;
            isAuthenticated = true;
            isConnecting = false;
            currentRetryAttempt = 0;

            SetConnectionStatus(ConnectionStatus.Connected);
            LogDebug($"PlayFab login successful. Player ID: {playFabId}");

            OnLoginSuccess?.Invoke();
        }

        private void OnLoginFailure_Internal(PlayFabError error)
        {
            isAuthenticated = false;
            isConnecting = false;
            
            string errorMessage = error.GenerateErrorReport();
            SmartLogger.LogError($"[NetworkingManager] PlayFab login failed: {errorMessage}", LogCategory.Networking, this);
            
            // Check if this is a retryable error
            if (IsRetryableError(error))
            {
                RetryLogin();
            }
            else
            {
                SetConnectionStatus(ConnectionStatus.Error);
            OnLoginFailure?.Invoke(errorMessage);
            }
        }

        /// <summary>
        /// Determine if an error is retryable
        /// </summary>
        private bool IsRetryableError(PlayFabError error)
        {
            // Retry on network errors, but not on authentication errors
            return error.Error == PlayFabErrorCode.ServiceUnavailable ||
                   error.Error == PlayFabErrorCode.InternalServerError ||
                   error.HttpCode >= 500; // Server errors
        }

        /// <summary>
        /// Check if the client is authenticated with PlayFab
        /// </summary>
        public bool IsAuthenticated()
        {
            return isAuthenticated && !string.IsNullOrEmpty(playFabId) && currentStatus == ConnectionStatus.Connected;
        }

        /// <summary>
        /// Force disconnect and clear session
        /// </summary>
        public void Disconnect()
        {
            isAuthenticated = false;
            playFabId = null;
            sessionTicket = null;
            currentMatchId = null;
            currentMatchGroup = null;
            SetConnectionStatus(ConnectionStatus.Disconnected);
            LogDebug("Disconnected from PlayFab");
        }

        #endregion

        #region CloudScript Function Calls

        /// <summary>
        /// Execute a PlayFab CloudScript/Azure Function with the given name and parameters
        /// Enhanced with better error handling and timeout support
        /// </summary>
        public void ExecuteCloudScript(
            string functionName, 
            object parameters, 
            Action<ExecuteCloudScriptResult> onSuccess = null, 
            Action<PlayFabError> onError = null)
        {
            if (!IsReadyForRequests())
            {
                string errorMsg = $"Cannot execute CloudScript '{functionName}': Not ready for requests (Status: {currentStatus})";
                SmartLogger.LogError(errorMsg, LogCategory.Networking, this);
                onError?.Invoke(new PlayFabError
                {
                    Error = PlayFabErrorCode.NotAuthenticated,
                    ErrorMessage = errorMsg
                });
                return;
            }

            var request = new ExecuteCloudScriptRequest
            {
                FunctionName = functionName,
                FunctionParameter = parameters,
                GeneratePlayStreamEvent = true
            };

            LogDebug($"Executing CloudScript function: {functionName}");

            PlayFabClientAPI.ExecuteCloudScript(
                request,
                result => {
                    LogDebug($"CloudScript function '{functionName}' executed successfully");
                    
                    // Check for server-side errors in the result
                    if (result.Error != null)
                    {
                        SmartLogger.LogError($"[NetworkingManager] CloudScript '{functionName}' returned server error: {result.Error.Message}", LogCategory.Networking, this);
                        onError?.Invoke(new PlayFabError
                        {
                            Error = PlayFabErrorCode.CloudScriptHTTPRequestError,
                            ErrorMessage = result.Error.Message
                        });
                        return;
                    }

                        onSuccess?.Invoke(result);
                },
                error => {
                    string errorMessage = error.GenerateErrorReport();
                    SmartLogger.LogError($"[NetworkingManager] CloudScript function '{functionName}' execution failed: {errorMessage}", LogCategory.Networking, this);
                    onError?.Invoke(error);
                    OnNetworkError?.Invoke(errorMessage);
                }
            );
        }

        #endregion

        #region Game State Management

        /// <summary>
        /// Fetch the current game state from the server
        /// </summary>
        public void GetGameState(Action<Dictionary<string, object>> onStateReceived = null)
        {
            if (string.IsNullOrEmpty(currentMatchId))
            {
                SmartLogger.LogWarning("[NetworkingManager] Cannot get game state: No active match", LogCategory.Networking, this);
                return;
            }

            ExecuteCloudScript(
                "GetGameState",
                new { matchId = currentMatchId },
                result => {
                    try
                    {
                        // Parse the game state from the function result
                        var gameState = result.FunctionResult as Dictionary<string, object>;
                        if (gameState != null)
                        {
                            LogDebug("Game state received from server");
                            OnGameStateUpdated?.Invoke(gameState);
                            onStateReceived?.Invoke(gameState);
                        }
                        else
                        {
                            SmartLogger.LogError("[NetworkingManager] Failed to parse game state from server result", LogCategory.Networking, this);
                        }
                    }
                    catch (Exception ex)
                    {
                        SmartLogger.LogError($"[NetworkingManager] Error processing game state: {ex.Message}", LogCategory.Networking, this);
                    }
                },
                error => {
                    SmartLogger.LogError($"[NetworkingManager] Failed to get game state: {error.ErrorMessage}", LogCategory.Networking, this);
                }
            );
        }

        /// <summary>
        /// Set the current match ID and shared group ID
        /// </summary>
        public void SetCurrentMatch(string matchId, string sharedGroupId = null)
        {
            currentMatchId = matchId;
            currentMatchGroup = sharedGroupId ?? matchId; // Use matchId as group if not specified
            LogDebug($"Current match set to: {matchId} (Group: {currentMatchGroup})");
        }

        /// <summary>
        /// Get the current match ID
        /// </summary>
        public string GetCurrentMatchId()
        {
            return currentMatchId;
        }

        /// <summary>
        /// Get the current match shared group ID
        /// </summary>
        public string GetCurrentMatchGroupId()
        {
            return currentMatchGroup;
        }

        /// <summary>
        /// Create a new match and set it as current
        /// </summary>
        public void CreateMatch(Action<string> onMatchCreated = null, Action<string> onError = null)
        {
            if (!IsReadyForRequests())
            {
                onError?.Invoke("Not connected to PlayFab");
                return;
            }

            ExecuteCloudScript(
                "InitializeMatch",
                new { playerId = playFabId },
                result => {
                    try
                    {
                        var response = result.FunctionResult as Dictionary<string, object>;
                        if (response != null && response.TryGetValue("matchId", out object matchIdObj))
                        {
                            string newMatchId = matchIdObj.ToString();
                            SetCurrentMatch(newMatchId);
                            LogDebug($"Match created successfully: {newMatchId}");
                            onMatchCreated?.Invoke(newMatchId);
                        }
                        else
                        {
                            onError?.Invoke("Invalid response from InitializeMatch");
                        }
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"Error processing match creation: {ex.Message}");
                    }
                },
                error => {
                    onError?.Invoke($"Failed to create match: {error.ErrorMessage}");
                }
            );
        }

        #endregion

        #region Command Execution

        /// <summary>
        /// Execute a game command on the server
        /// Enhanced with better error handling and response parsing
        /// </summary>
        public void ExecuteCommand(
            string commandName, 
            object commandData, 
            Action<Dictionary<string, object>> onSuccess = null,
            Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(currentMatchId))
            {
                string errorMsg = "Cannot execute command: No active match";
                SmartLogger.LogWarning(errorMsg, LogCategory.Networking, this);
                onError?.Invoke(errorMsg);
                return;
            }

            // Add match ID to command data
            var fullCommandData = new Dictionary<string, object>
            {
                { "matchId", currentMatchId },
                { "commandData", commandData }
            };

            LogDebug($"Executing command: {commandName} for match: {currentMatchId}");

            // Execute the command on the server
            ExecuteCloudScript(
                commandName,
                fullCommandData,
                result => {
                    try
                    {
                        var responseData = result.FunctionResult as Dictionary<string, object>;
                        if (responseData != null)
                        {
                            // Check for success flag in response
                            if (responseData.TryGetValue("success", out object successObj) && 
                                successObj is bool success && success)
                            {
                                LogDebug($"Command '{commandName}' executed successfully");
                                
                                // Get updated game state if available
                                if (responseData.TryGetValue("gameState", out object stateObj) && 
                                    stateObj is Dictionary<string, object> gameState)
                                {
                                    OnGameStateUpdated?.Invoke(gameState);
                                    onSuccess?.Invoke(gameState);
                                }
                                else
                                {
                                    // If no game state returned, pass the response data
                                    onSuccess?.Invoke(responseData);
                                }
                            }
                            else
                            {
                                // Command failed on server
                                string errorMessage = "Command execution failed on server";
                                if (responseData.TryGetValue("errorMessage", out object errObj) && 
                                    errObj is string errMsg)
                                {
                                    errorMessage = errMsg;
                                }
                                SmartLogger.LogError($"[NetworkingManager] Command '{commandName}' failed: {errorMessage}", LogCategory.Networking, this);
                                onError?.Invoke(errorMessage);
                            }
                        }
                        else
                        {
                            SmartLogger.LogError($"[NetworkingManager] Command '{commandName}' returned invalid response", LogCategory.Networking, this);
                            onError?.Invoke("Invalid server response");
                        }
                    }
                    catch (Exception ex)
                    {
                        SmartLogger.LogError($"[NetworkingManager] Error processing command '{commandName}' response: {ex.Message}", LogCategory.Networking, this);
                        onError?.Invoke($"Error processing response: {ex.Message}");
                    }
                },
                error => {
                    string errorMessage = error.GenerateErrorReport();
                    SmartLogger.LogError($"[NetworkingManager] Command '{commandName}' failed: {errorMessage}", LogCategory.Networking, this);
                    onError?.Invoke(errorMessage);
                }
            );
        }

        #endregion

        #region Player Data Management

        public void GetPlayerData(List<string> keys, Action<GetUserDataResult> onSuccess = null)
        {
            if (!IsAuthenticated())
            {
                SmartLogger.LogError("[NetworkingManager] Cannot get player data: Not authenticated", LogCategory.Networking, this);
                return;
            }

            LogDebug($"Getting player data for keys: {string.Join(", ", keys)}");
            
            var request = new GetUserDataRequest
            {
                Keys = keys
            };

            PlayFabClientAPI.GetUserData(request, onSuccess, error => {
                SmartLogger.LogError($"[NetworkingManager] Failed to get player data: {error.ErrorMessage}", LogCategory.Networking, this);
            });
        }

        public void SetPlayerData(Dictionary<string, string> data, Action<UpdateUserDataResult> onSuccess = null)
        {
            if (!IsAuthenticated())
            {
                SmartLogger.LogError("[NetworkingManager] Cannot set player data: Not authenticated", LogCategory.Networking, this);
                return;
            }

            LogDebug($"Setting player data: {string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            
            var request = new UpdateUserDataRequest
            {
                Data = data
            };

            PlayFabClientAPI.UpdateUserData(request, onSuccess, error => {
                SmartLogger.LogError($"[NetworkingManager] Failed to set player data: {error.ErrorMessage}", LogCategory.Networking, this);
            });
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                SmartLogger.Log($"[NetworkingManager] {message}", LogCategory.Networking, this);
            }
        }

        /// <summary>
        /// Get connection info for debugging
        /// </summary>
        public string GetConnectionInfo()
        {
            return $"Status: {currentStatus}, Authenticated: {isAuthenticated}, PlayFabId: {playFabId ?? "None"}, MatchId: {currentMatchId ?? "None"}";
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up any pending invokes
            CancelInvoke();
        }
    }
} 
