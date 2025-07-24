using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Utilities;
using Dokkaebi.Units;
using Dokkaebi.Common;
using Dokkaebi.TurnSystem;
using Dokkaebi.Core.Networking;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Manages player Aura resources and their usage across the game.
    /// Centralizes aura-related functionality that was previously scattered.
    /// Integrates with turn system for automatic gains and PVP sync.
    /// </summary>
    public class AuraManager : MonoBehaviour
    {
        // Singleton pattern
        private static AuraManager instance;
        public static AuraManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AuraManager");
                    instance = go.AddComponent<AuraManager>();
                    // DontDestroyOnLoad is not supported in EditMode (tests)
                    if (Application.isPlaying)
                    {
                    DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("Aura Settings")]
        [SerializeField] private int baseAuraPerTurn = 2;
        [SerializeField] private int maxAuraPerTurn = 5;
        [SerializeField] private int maxAuraCapacity = 15;

        [Header("Gain Modifiers")]
        [SerializeField] private float unitBasedGainMultiplier = 0.1f; // Bonus per alive unit
        [SerializeField] private int minimumAuraGain = 1; // Minimum gain per turn
        [SerializeField] private int maximumAuraGain = 10; // Maximum gain per turn

        [Header("Networking")]
        [SerializeField] private bool enableNetworkSync = false; // Toggle for PVP mode
        [SerializeField] private bool isOfflineMode = true; // Local testing mode

        // Player aura tracking
        private int player1CurrentAura;
        private int player2CurrentAura;
        private int player1MaxAura;
        private int player2MaxAura;

        // Turn system integration
        private V3TurnManager turnManager;
        private GameStateManagerMultiplayer networkStateManager;

        // Events
        public delegate void AuraChangedHandler(int playerId, int oldValue, int newValue);
        public event AuraChangedHandler OnAuraChanged;

        public delegate void AuraGainedHandler(int playerId, int amount, string reason);
        public event AuraGainedHandler OnAuraGained;

        private void Awake()
        {
            SmartLogger.Log($"[AuraManager.Awake] Awake called on {gameObject.name} (InstanceID: {GetInstanceID()})", LogCategory.General, this);
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize aura values - FIXED: Start with proper initial aura
            player1MaxAura = maxAuraCapacity;
            player2MaxAura = maxAuraCapacity;
            player1CurrentAura = maxAuraCapacity; // Start with full aura instead of baseAuraPerTurn
            player2CurrentAura = maxAuraCapacity; // Start with full aura instead of baseAuraPerTurn
            
            SmartLogger.Log($"[AuraManager.Awake] Initialized aura values - P1: {player1CurrentAura}/{player1MaxAura}, P2: {player2CurrentAura}/{player2MaxAura}", LogCategory.Debug, this);
        }

        private void Start()
        {
            // Find and connect to turn system
            InitializeTurnSystemIntegration();
            InitializeNetworkingIntegration();
        }

        private void InitializeTurnSystemIntegration()
        {
            turnManager = FindFirstObjectByType<V3TurnManager>();
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.AddListener(OnTurnStarted);
                SmartLogger.Log("[AuraManager] Connected to V3TurnManager for turn-based aura gains", LogCategory.General, this);
            }
            else
            {
                SmartLogger.LogWarning("[AuraManager] V3TurnManager not found - aura gains will be manual only", LogCategory.General, this);
            }
        }

        private void InitializeNetworkingIntegration()
        {
            if (enableNetworkSync && !isOfflineMode)
            {
                networkStateManager = FindFirstObjectByType<GameStateManagerMultiplayer>();
                if (networkStateManager != null)
                {
                    networkStateManager.OnAuthoritativeStateReceived += OnNetworkStateReceived;
                    SmartLogger.Log("[AuraManager] Connected to GameStateManagerMultiplayer for PVP sync", LogCategory.Networking, this);
                }
                else
                {
                    SmartLogger.LogWarning("[AuraManager] GameStateManagerMultiplayer not found - PVP sync disabled", LogCategory.Networking, this);
                }
            }
        }

        /// <summary>
        /// Called when a new turn starts - automatically grants aura to both players
        /// </summary>
        private void OnTurnStarted(int turnNumber)
        {
            SmartLogger.Log($"[AuraManager.OnTurnStarted] Turn {turnNumber} started - calculating aura gains", LogCategory.Debug, this);
            
            // Calculate and apply aura gains for both players
            int player1Gain = CalculateAuraGain(true);
            int player2Gain = CalculateAuraGain(false);

            GainAuraForTurn(true, player1Gain, "Turn Start");
            GainAuraForTurn(false, player2Gain, "Turn Start");
        }

        /// <summary>
        /// Calculate aura gain for a player based on current game state and modifiers
        /// </summary>
        private int CalculateAuraGain(bool isPlayer1)
        {
            int baseGain = baseAuraPerTurn;
            
            // Apply unit-based modifier (more alive units = slightly more aura)
            var unitManager = UnitManager.Instance;
            if (unitManager != null)
            {
                var aliveUnits = unitManager.GetAliveUnits();
                int playerUnits = 0;
                foreach (var unit in aliveUnits)
                {
                    if (unit.IsPlayer() == isPlayer1)
                    {
                        playerUnits++;
                    }
                }
                
                int unitBonus = Mathf.FloorToInt(playerUnits * unitBasedGainMultiplier);
                baseGain += unitBonus;
                SmartLogger.Log($"[AuraManager] Player {(isPlayer1 ? "1" : "2")} unit bonus: {unitBonus} (from {playerUnits} alive units)", LogCategory.Debug, this);
            }

            // Apply caps
            int finalGain = Mathf.Clamp(baseGain, minimumAuraGain, maximumAuraGain);
            SmartLogger.Log($"[AuraManager] Player {(isPlayer1 ? "1" : "2")} calculated aura gain: {finalGain}", LogCategory.Debug, this);
            
            return finalGain;
        }

        /// <summary>
        /// Get the current aura for a player
        /// </summary>
        public int GetCurrentAura(bool isPlayer1)
        {
            return isPlayer1 ? player1CurrentAura : player2CurrentAura;
        }

        /// <summary>
        /// Get the maximum aura for a player
        /// </summary>
        public int GetMaxAura(bool isPlayer1)
        {
            return isPlayer1 ? player1MaxAura : player2MaxAura;
        }

        /// <summary>
        /// Modify a player's aura by the specified amount
        /// </summary>
        public void ModifyAura(bool isPlayer1, int amount)
        {
            SmartLogger.Log($"[AuraManager.ModifyAura] Entering for Player {(isPlayer1 ? "1" : "2")}, Amount: {amount}", LogCategory.Debug);
            int oldValue;
            int newValue;

            if (isPlayer1)
            {
                oldValue = player1CurrentAura;
                player1CurrentAura = Mathf.Clamp(player1CurrentAura + amount, 0, player1MaxAura);
                newValue = player1CurrentAura;
            }
            else
            {
                oldValue = player2CurrentAura;
                player2CurrentAura = Mathf.Clamp(player2CurrentAura + amount, 0, player2MaxAura);
                newValue = player2CurrentAura;
            }

            SmartLogger.Log($"[AuraManager.ModifyAura] Player {(isPlayer1 ? "1" : "2")} aura changed: {oldValue} -> {newValue} (modifier: {amount})", LogCategory.Debug);

            // FIXED: Always fire event, even for zero changes (for consistency with tests)
            try 
            {
            OnAuraChanged?.Invoke(isPlayer1 ? 1 : 2, oldValue, newValue);
                SmartLogger.Log($"[AuraManager.ModifyAura] OnAuraChanged event fired for Player {(isPlayer1 ? "1" : "2")}", LogCategory.Debug);
            }
            catch (System.Exception e)
            {
                SmartLogger.LogError($"[AuraManager.ModifyAura] Error firing OnAuraChanged event: {e.Message}", LogCategory.General, this);
            }

            // Sync to network if in PVP mode
            if (enableNetworkSync && !isOfflineMode && amount != 0)
            {
                SyncAuraToNetwork(isPlayer1, newValue);
            }
        }

        /// <summary>
        /// Check if a player has enough aura to perform an action
        /// </summary>
        public bool HasEnoughAura(bool isPlayer1, int cost)
        {
            return GetCurrentAura(isPlayer1) >= cost;
        }

        /// <summary>
        /// Enhanced aura gain method with custom amount and reason tracking
        /// </summary>
        public void GainAuraForTurn(bool isPlayer1, int amount, string reason)
        {
            string playerString = isPlayer1 ? "Player 1" : "Player 2";
            SmartLogger.Log($"[AuraManager.GainAuraForTurn] {playerString} gaining {amount} aura. Reason: {reason}", LogCategory.Debug);
            
            try
            {
                // Use ModifyAura to handle clamping and events
                ModifyAura(isPlayer1, amount);

                // FIXED: Fire gain event with reason - ensure it always fires
                try
                {
                    OnAuraGained?.Invoke(isPlayer1 ? 1 : 2, amount, reason);
                    SmartLogger.Log($"[AuraManager.GainAuraForTurn] OnAuraGained event fired for {playerString}, amount: {amount}, reason: {reason}", LogCategory.Debug);
                }
                catch (System.Exception eventException)
                {
                    SmartLogger.LogError($"[AuraManager.GainAuraForTurn] Error firing OnAuraGained event: {eventException.Message}", LogCategory.General, this);
                }
            }
            catch (System.Exception e)
            {
                SmartLogger.LogError($"[AuraManager.GainAuraForTurn] Exception caught while processing for {playerString}: {e.Message}\n{e.StackTrace}", LogCategory.General, this);
            }
        }

        /// <summary>
        /// Overloaded method for backward compatibility - uses calculated gain amount
        /// </summary>
        public void GainAuraForTurn(bool isPlayer1)
        {
            int gainAmount = CalculateAuraGain(isPlayer1);
            GainAuraForTurn(isPlayer1, gainAmount, "Automatic Turn Gain");
        }

        /// <summary>
        /// Set the maximum aura for a player
        /// </summary>
        public void SetMaxAura(bool isPlayer1, int maxAura)
        {
            if (isPlayer1)
            {
                player1MaxAura = maxAura;
                player1CurrentAura = Mathf.Min(player1CurrentAura, maxAura);
            }
            else
            {
                player2MaxAura = maxAura;
                player2CurrentAura = Mathf.Min(player2CurrentAura, maxAura);
            }

            SmartLogger.Log($"Player {(isPlayer1 ? "1" : "2")} max aura set to {maxAura}", LogCategory.Debug);
        }

        #region Networking Integration

        /// <summary>
        /// Handle network state updates for PVP synchronization
        /// </summary>
        private void OnNetworkStateReceived(Dictionary<string, object> serverState)
        {
            if (isOfflineMode) return;

            try
            {
                // Sync Player 1 aura
                if (serverState.TryGetValue("player1", out var p1Obj) && 
                    p1Obj is Dictionary<string, object> player1Data &&
                    player1Data.TryGetValue("currentAura", out var p1AuraObj))
                {
                    int serverAura1 = int.Parse(p1AuraObj.ToString());
                    if (player1CurrentAura != serverAura1)
                    {
                        int oldAura = player1CurrentAura;
                        player1CurrentAura = serverAura1;
                        OnAuraChanged?.Invoke(1, oldAura, serverAura1);
                        SmartLogger.Log($"[AuraManager] Synced Player 1 aura from network: {oldAura} -> {serverAura1}", LogCategory.Networking, this);
                    }
                }

                // Sync Player 2 aura
                if (serverState.TryGetValue("player2", out var p2Obj) && 
                    p2Obj is Dictionary<string, object> player2Data &&
                    player2Data.TryGetValue("currentAura", out var p2AuraObj))
                {
                    int serverAura2 = int.Parse(p2AuraObj.ToString());
                    if (player2CurrentAura != serverAura2)
                    {
                        int oldAura = player2CurrentAura;
                        player2CurrentAura = serverAura2;
                        OnAuraChanged?.Invoke(2, oldAura, serverAura2);
                        SmartLogger.Log($"[AuraManager] Synced Player 2 aura from network: {oldAura} -> {serverAura2}", LogCategory.Networking, this);
                    }
                }
            }
            catch (System.Exception e)
            {
                SmartLogger.LogError($"[AuraManager] Error syncing aura from network: {e.Message}", LogCategory.Networking, this);
            }
        }

        /// <summary>
        /// Sync local aura changes to the network (for PVP)
        /// </summary>
        private void SyncAuraToNetwork(bool isPlayer1, int newValue)
        {
            // This would typically send an aura update command to the server
            // For now, we'll log it as the networking integration is still being developed
            SmartLogger.Log($"[AuraManager] Would sync Player {(isPlayer1 ? "1" : "2")} aura to network: {newValue}", LogCategory.Networking, this);
        }

        /// <summary>
        /// Enable or disable networking sync (useful for testing)
        /// </summary>
        public void SetNetworkMode(bool enableNetwork, bool offline = false)
        {
            enableNetworkSync = enableNetwork;
            isOfflineMode = offline;
            SmartLogger.Log($"[AuraManager] Network mode set - EnableSync: {enableNetwork}, OfflineMode: {offline}", LogCategory.Networking, this);
        }

        #endregion

        #region Anti-Cheat Validation

        /// <summary>
        /// Validate aura modification for anti-cheat (impossible actions)
        /// </summary>
        public bool ValidateAuraModification(bool isPlayer1, int amount, string reason)
        {
            int currentAura = GetCurrentAura(isPlayer1);
            int maxAura = GetMaxAura(isPlayer1);

            SmartLogger.Log($"[AuraManager.ValidateAuraModification] Player {(isPlayer1 ? "1" : "2")}: Current={currentAura}, Amount={amount}, Reason='{reason}'", LogCategory.Debug);

            // Check for impossible negative values (after modification)
            if (currentAura + amount < 0)
            {
                SmartLogger.LogWarning($"[AuraManager] ANTI-CHEAT: Player {(isPlayer1 ? "1" : "2")} attempted impossible negative aura: {currentAura} + {amount} = {currentAura + amount}", LogCategory.Networking, this);
                return false;
            }

            // FIXED: More reasonable high value check - allow legitimate gains up to max + reasonable bonus
            if (amount > 0 && currentAura + amount > maxAura + maxAuraPerTurn * 2) // Allow some flexibility for turn gains
            {
                SmartLogger.LogWarning($"[AuraManager] ANTI-CHEAT: Player {(isPlayer1 ? "1" : "2")} attempted impossible high aura: {currentAura} + {amount} = {currentAura + amount} (max allowed: {maxAura + maxAuraPerTurn * 2})", LogCategory.Networking, this);
                return false;
            }

            // FIXED: More reasonable large deduction check - abilities can cost up to max capacity
            if (amount < 0 && Mathf.Abs(amount) > maxAuraCapacity)
            {
                SmartLogger.LogWarning($"[AuraManager] ANTI-CHEAT: Player {(isPlayer1 ? "1" : "2")} attempted impossible large deduction: {amount} (max allowed: -{maxAuraCapacity})", LogCategory.Networking, this);
                return false;
            }

            SmartLogger.Log($"[AuraManager.ValidateAuraModification] Validation passed for Player {(isPlayer1 ? "1" : "2")}", LogCategory.Debug);
            return true;
        }

        #endregion

        #region Player Aura Access Methods (for GameStateManagerMultiplayer)

        /// <summary>
        /// Get current aura for a specific player (used by GameStateManagerMultiplayer)
        /// </summary>
        public int GetPlayerAura(bool isPlayer1)
        {
            return isPlayer1 ? player1CurrentAura : player2CurrentAura;
        }

        /// <summary>
        /// Set aura for a specific player directly (used by server sync)
        /// </summary>
        public void SetPlayerAura(bool isPlayer1, int newValue)
        {
            int oldValue = isPlayer1 ? player1CurrentAura : player2CurrentAura;
            
            if (isPlayer1)
            {
                player1CurrentAura = Mathf.Clamp(newValue, 0, player1MaxAura);
            }
            else
            {
                player2CurrentAura = Mathf.Clamp(newValue, 0, player2MaxAura);
            }

                    // Trigger events
        OnAuraChanged?.Invoke(isPlayer1 ? 1 : 2, oldValue, newValue);
        SmartLogger.Log($"[AuraManager] Server sync - Player {(isPlayer1 ? 1 : 2)} aura set to {newValue}", LogCategory.Networking, this);
        }

        /// <summary>
        /// Get max aura for a specific player (used by GameStateManagerMultiplayer)
        /// </summary>
        public int GetPlayerMaxAura(bool isPlayer1)
        {
            return isPlayer1 ? player1MaxAura : player2MaxAura;
        }

        /// <summary>
        /// Set max aura for a specific player (used by server sync)
        /// </summary>
        public void SetPlayerMaxAura(bool isPlayer1, int newMaxValue)
        {
            if (isPlayer1)
            {
                player1MaxAura = Mathf.Max(0, newMaxValue);
                // Clamp current aura if it exceeds new max
                if (player1CurrentAura > player1MaxAura)
                {
                    int oldCurrent = player1CurrentAura;
                    player1CurrentAura = player1MaxAura;
                    OnAuraChanged?.Invoke(1, oldCurrent, player1CurrentAura);
                }
            }
            else
            {
                player2MaxAura = Mathf.Max(0, newMaxValue);
                // Clamp current aura if it exceeds new max
                if (player2CurrentAura > player2MaxAura)
                {
                    int oldCurrent = player2CurrentAura;
                    player2CurrentAura = player2MaxAura;
                    OnAuraChanged?.Invoke(2, oldCurrent, player2CurrentAura);
                }
            }

            SmartLogger.Log($"[AuraManager] Server sync - Player {(isPlayer1 ? 1 : 2)} max aura set to {newMaxValue}", LogCategory.Networking, this);
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (turnManager != null)
            {
                turnManager.OnTurnStarted.RemoveListener(OnTurnStarted);
            }

            if (networkStateManager != null)
            {
                networkStateManager.OnAuthoritativeStateReceived -= OnNetworkStateReceived;
            }
        }
    }
} 