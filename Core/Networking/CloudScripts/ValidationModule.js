/**
 * PlayFab CloudScript Validation Module
 * Comprehensive server-side validation and anti-cheat system
 * Used by all game action handlers for consistent validation
 */

// Export validation functions for use in other CloudScript files
var ValidationModule = {
    
    // Configuration constants
    GRID_SIZE: 10,
    MAX_MOVEMENT_RANGE: 3,
    MAX_ABILITY_RANGE: 5,
    TURN_TIME_LIMIT_MS: 60000,
    MAX_ACTIONS_PER_TURN: 5,
    MIN_ACTION_INTERVAL_MS: 100,

    /**
     * Main validation entry point
     */
    validatePlayerAction: function(gameState, action, playerId) {
        try {
            // 1. Basic input validation
            var inputValidation = this.validateActionInput(action);
            if (!inputValidation.isValid) {
                return inputValidation;
            }

            // 2. Player authorization
            var playerNumber = this.getPlayerNumber(gameState, playerId);
            if (playerNumber === -1) {
                return { isValid: false, error: "Player not in match", code: "NOT_IN_MATCH" };
            }

            // 3. Turn state validation
            var turnValidation = this.validateTurnState(gameState, playerNumber);
            if (!turnValidation.isValid) {
                return turnValidation;
            }

            // 4. Action-specific validation
            var actionValidation = this.validateSpecificAction(gameState, action, playerNumber);
            if (!actionValidation.isValid) {
                return actionValidation;
            }

            // 5. Anti-cheat checks
            var antiCheatResult = this.performAntiCheatValidation(gameState, action, playerId);
            if (!antiCheatResult.isValid) {
                this.logSecurityViolation(playerId, action, antiCheatResult.violationType);
                return { isValid: false, error: "Security violation detected", code: "SECURITY_VIOLATION" };
            }

            return { isValid: true };

        } catch (error) {
            log.error("Validation error: " + error.message);
            return { isValid: false, error: "Validation system error", code: "VALIDATION_ERROR" };
        }
    },

    /**
     * Validate action input structure
     */
    validateActionInput: function(action) {
        if (!action || typeof action !== 'object') {
            return { isValid: false, error: "Invalid action object", code: "INVALID_INPUT" };
        }

        if (!action.actionType || !['move', 'ability', 'pass'].includes(action.actionType)) {
            return { isValid: false, error: "Invalid action type", code: "INVALID_ACTION_TYPE" };
        }

        // Validate action-specific required fields
        switch (action.actionType) {
            case 'move':
                if (!this.isValidPosition(action.targetPosition)) {
                    return { isValid: false, error: "Invalid target position for move", code: "INVALID_POSITION" };
                }
                break;
                
            case 'ability':
                if (!action.abilityId || typeof action.abilityId !== 'string') {
                    return { isValid: false, error: "Invalid ability ID", code: "INVALID_ABILITY_ID" };
                }
                break;
        }

        if (action.actionType !== 'pass') {
            if (!action.unitId || typeof action.unitId !== 'number') {
                return { isValid: false, error: "Invalid unit ID", code: "INVALID_UNIT_ID" };
            }
        }

        return { isValid: true };
    },

    /**
     * Validate turn state and timing
     */
    validateTurnState: function(gameState, playerNumber) {
        // Check game status
        if (gameState.gameStatus !== 'ACTIVE') {
            return { isValid: false, error: "Game is not active", code: "GAME_NOT_ACTIVE" };
        }

        // Check phase
        if (gameState.currentPhase !== 'ActionSubmission') {
            return { isValid: false, error: "Not in action submission phase", code: "WRONG_PHASE" };
        }

        // Check if player already submitted
        var playerKey = 'player' + playerNumber;
        if (gameState[playerKey].hasSubmitted) {
            return { isValid: false, error: "Already submitted for this turn", code: "ALREADY_SUBMITTED" };
        }

        // Check turn time limit
        var currentTime = Date.now();
        var turnStartTime = gameState.turnStartTime || currentTime;
        if (currentTime - turnStartTime > this.TURN_TIME_LIMIT_MS) {
            return { isValid: false, error: "Turn time expired", code: "TIME_EXPIRED" };
        }

        return { isValid: true };
    },

    /**
     * Validate specific action types
     */
    validateSpecificAction: function(gameState, action, playerNumber) {
        if (action.actionType === 'pass') {
            return { isValid: true };
        }

        // Find and validate unit
        var unit = this.findUnit(gameState, action.unitId);
        if (!unit) {
            return { isValid: false, error: "Unit not found", code: "UNIT_NOT_FOUND" };
        }

        // Check unit ownership
        if (unit.owner !== playerNumber) {
            return { isValid: false, error: "Unit not owned by player", code: "UNIT_NOT_OWNED" };
        }

        // Check unit state
        if (unit.currentHealth <= 0) {
            return { isValid: false, error: "Unit is destroyed", code: "UNIT_DESTROYED" };
        }

        if (unit.hasActed) {
            return { isValid: false, error: "Unit already acted", code: "UNIT_ALREADY_ACTED" };
        }

        // Action-specific validation
        switch (action.actionType) {
            case 'move':
                return this.validateMoveAction(gameState, unit, action.targetPosition);
            case 'ability':
                return this.validateAbilityAction(gameState, unit, action);
            default:
                return { isValid: false, error: "Unknown action type", code: "UNKNOWN_ACTION" };
        }
    },

    /**
     * Validate movement action
     */
    validateMoveAction: function(gameState, unit, targetPosition) {
        // Check position bounds
        if (!this.isPositionInBounds(targetPosition)) {
            return { isValid: false, error: "Target position out of bounds", code: "OUT_OF_BOUNDS" };
        }

        // Check movement range
        var distance = this.calculateDistance(unit.position, targetPosition);
        if (distance > this.MAX_MOVEMENT_RANGE) {
            return { isValid: false, error: "Movement range exceeded", code: "RANGE_EXCEEDED" };
        }

        // Check if position is occupied
        if (this.isPositionOccupied(gameState, targetPosition)) {
            return { isValid: false, error: "Position occupied", code: "POSITION_OCCUPIED" };
        }

        // Check for valid path (basic obstruction check)
        if (!this.hasValidMovementPath(gameState, unit.position, targetPosition)) {
            return { isValid: false, error: "Path blocked", code: "PATH_BLOCKED" };
        }

        return { isValid: true };
    },

    /**
     * Validate ability action
     */
    validateAbilityAction: function(gameState, unit, action) {
        // Get ability data
        var ability = this.getAbilityConfiguration(action.abilityId);
        if (!ability) {
            return { isValid: false, error: "Ability not found", code: "ABILITY_NOT_FOUND" };
        }

        // Check if unit has ability
        if (!unit.abilities || unit.abilities.indexOf(action.abilityId) === -1) {
            return { isValid: false, error: "Unit doesn't have ability", code: "ABILITY_NOT_AVAILABLE" };
        }

        // Check aura cost
        var playerKey = 'player' + unit.owner;
        if (gameState[playerKey].currentAura < ability.auraCost) {
            return { isValid: false, error: "Insufficient aura", code: "INSUFFICIENT_AURA" };
        }

        // Validate targeting
        return this.validateAbilityTargeting(gameState, unit, action, ability);
    },

    /**
     * Validate ability targeting
     */
    validateAbilityTargeting: function(gameState, unit, action, ability) {
        switch (ability.targetingType) {
            case 'self':
                // No additional validation needed
                return { isValid: true };

            case 'position':
                if (!action.targetPosition) {
                    return { isValid: false, error: "Target position required", code: "TARGET_POSITION_REQUIRED" };
                }
                
                var distance = this.calculateDistance(unit.position, action.targetPosition);
                if (distance > ability.range) {
                    return { isValid: false, error: "Target out of range", code: "TARGET_OUT_OF_RANGE" };
                }
                return { isValid: true };

            case 'unit':
                if (!action.targetUnitId) {
                    return { isValid: false, error: "Target unit required", code: "TARGET_UNIT_REQUIRED" };
                }
                
                var targetUnit = this.findUnit(gameState, action.targetUnitId);
                if (!targetUnit) {
                    return { isValid: false, error: "Target unit not found", code: "TARGET_UNIT_NOT_FOUND" };
                }
                
                var unitDistance = this.calculateDistance(unit.position, targetUnit.position);
                if (unitDistance > ability.range) {
                    return { isValid: false, error: "Target unit out of range", code: "TARGET_OUT_OF_RANGE" };
                }
                
                // Check targeting restrictions
                var isFriendly = targetUnit.owner === unit.owner;
                if (isFriendly && !ability.canTargetFriendly) {
                    return { isValid: false, error: "Cannot target friendly", code: "CANNOT_TARGET_FRIENDLY" };
                }
                if (!isFriendly && !ability.canTargetEnemy) {
                    return { isValid: false, error: "Cannot target enemy", code: "CANNOT_TARGET_ENEMY" };
                }
                
                return { isValid: true };

            default:
                return { isValid: false, error: "Invalid targeting type", code: "INVALID_TARGETING_TYPE" };
        }
    },

    /**
     * Anti-cheat validation system
     */
    performAntiCheatValidation: function(gameState, action, playerId) {
        var violations = [];

        // 1. Check for impossible actions
        if (this.detectImpossibleAction(gameState, action)) {
            violations.push("IMPOSSIBLE_ACTION");
        }

        // 2. Check submission timing (spam protection)
        if (this.detectRapidSubmission(playerId)) {
            violations.push("RAPID_SUBMISSION");
        }

        // 3. Check for duplicate actions
        if (this.detectDuplicateAction(gameState, action, playerId)) {
            violations.push("DUPLICATE_ACTION");
        }

        // 4. Check action count limits
        if (this.detectTooManyActions(gameState, playerId)) {
            violations.push("TOO_MANY_ACTIONS");
        }

        // 5. Check for statistical anomalies
        if (this.detectStatisticalAnomalies(gameState, action, playerId)) {
            violations.push("STATISTICAL_ANOMALY");
        }

        if (violations.length > 0) {
            return { 
                isValid: false, 
                violationType: violations[0],
                allViolations: violations
            };
        }

        return { isValid: true };
    },

    /**
     * Security violation logging and handling
     */
    logSecurityViolation: function(playerId, action, violationType) {
        var violation = {
            playerId: playerId,
            timestamp: Date.now(),
            violationType: violationType,
            action: JSON.stringify(action),
            severity: this.getViolationSeverity(violationType)
        };

        log.error("SECURITY_VIOLATION", violation);

        // Update player's violation history
        this.updatePlayerViolationHistory(playerId, violation);

        // Apply automated penalties if needed
        this.applyAutomatedPenalty(playerId, violationType);
    },

    /**
     * Helper Functions
     */
    
    findUnit: function(gameState, unitId) {
        for (var i = 0; i < gameState.units.length; i++) {
            if (gameState.units[i].unitId === unitId) {
                return gameState.units[i];
            }
        }
        return null;
    },

    getPlayerNumber: function(gameState, playerId) {
        if (gameState.player1.playerId === playerId) return 1;
        if (gameState.player2.playerId === playerId) return 2;
        return -1;
    },

    isValidPosition: function(position) {
        return position && 
               typeof position.x === 'number' && 
               typeof position.z === 'number';
    },

    isPositionInBounds: function(position) {
        return position.x >= 0 && position.x < this.GRID_SIZE &&
               position.z >= 0 && position.z < this.GRID_SIZE;
    },

    calculateDistance: function(pos1, pos2) {
        return Math.abs(pos1.x - pos2.x) + Math.abs(pos1.z - pos2.z);
    },

    isPositionOccupied: function(gameState, position) {
        for (var i = 0; i < gameState.units.length; i++) {
            var unit = gameState.units[i];
            if (unit.currentHealth > 0 && 
                unit.position.x === position.x && 
                unit.position.z === position.z) {
                return true;
            }
        }
        return false;
    },

    hasValidMovementPath: function(gameState, fromPos, toPos) {
        // Simplified pathfinding check
        // In production, implement proper A* pathfinding with obstacle detection
        return true;
    },

    getAbilityConfiguration: function(abilityId) {
        // This would load from game configuration/data
        // For demo purposes, return sample configurations
        var abilities = {
            'fireball': {
                auraCost: 3,
                range: 4,
                targetingType: 'position',
                effectType: 'damage',
                damage: 30
            },
            'heal': {
                auraCost: 2,
                range: 2,
                targetingType: 'unit',
                canTargetFriendly: true,
                canTargetEnemy: false,
                effectType: 'heal',
                healing: 25
            }
        };
        
        return abilities[abilityId] || null;
    },

    // Anti-cheat detection methods
    detectImpossibleAction: function(gameState, action) {
        if (action.actionType === 'move') {
            var unit = this.findUnit(gameState, action.unitId);
            var distance = this.calculateDistance(unit.position, action.targetPosition);
            return distance > this.MAX_MOVEMENT_RANGE;
        }
        return false;
    },

    detectRapidSubmission: function(playerId) {
        // Check player's last submission time
        var lastSubmission = this.getPlayerLastSubmissionTime(playerId);
        if (lastSubmission) {
            return (Date.now() - lastSubmission) < this.MIN_ACTION_INTERVAL_MS;
        }
        return false;
    },

    detectDuplicateAction: function(gameState, action, playerId) {
        var playerNumber = this.getPlayerNumber(gameState, playerId);
        var playerKey = 'player' + playerNumber;
        var existingActions = gameState[playerKey].submittedActions || [];
        
        var actionString = JSON.stringify(action);
        for (var i = 0; i < existingActions.length; i++) {
            if (JSON.stringify(existingActions[i]) === actionString) {
                return true;
            }
        }
        return false;
    },

    detectTooManyActions: function(gameState, playerId) {
        var playerNumber = this.getPlayerNumber(gameState, playerId);
        var playerKey = 'player' + playerNumber;
        var actionCount = (gameState[playerKey].submittedActions || []).length;
        return actionCount >= this.MAX_ACTIONS_PER_TURN;
    },

    detectStatisticalAnomalies: function(gameState, action, playerId) {
        // Implement statistical analysis for detecting unusual patterns
        // This could include perfect accuracy, inhuman reaction times, etc.
        return false;
    },

    getViolationSeverity: function(violationType) {
        var severityMap = {
            'IMPOSSIBLE_ACTION': 'HIGH',
            'RAPID_SUBMISSION': 'MEDIUM',
            'DUPLICATE_ACTION': 'LOW',
            'TOO_MANY_ACTIONS': 'MEDIUM',
            'STATISTICAL_ANOMALY': 'HIGH'
        };
        return severityMap[violationType] || 'LOW';
    },

    updatePlayerViolationHistory: function(playerId, violation) {
        try {
            var playerData = server.GetUserInternalData({
                PlayFabId: playerId,
                Keys: ["violations", "violationCount"]
            });

            var violations = [];
            var count = 0;

            if (playerData.Data) {
                if (playerData.Data.violations) {
                    violations = JSON.parse(playerData.Data.violations.Value || '[]');
                }
                if (playerData.Data.violationCount) {
                    count = parseInt(playerData.Data.violationCount.Value || '0');
                }
            }

            violations.push(violation);
            count++;

            // Keep only last 10 violations to prevent data bloat
            if (violations.length > 10) {
                violations = violations.slice(-10);
            }

            server.UpdateUserInternalData({
                PlayFabId: playerId,
                Data: {
                    violations: JSON.stringify(violations),
                    violationCount: count.toString(),
                    lastViolation: JSON.stringify(violation)
                }
            });

        } catch (error) {
            log.error("Error updating violation history: " + error.message);
        }
    },

    applyAutomatedPenalty: function(playerId, violationType) {
        try {
            var playerStats = server.GetUserInternalData({
                PlayFabId: playerId,
                Keys: ["violationCount"]
            });

            var violationCount = 0;
            if (playerStats.Data && playerStats.Data.violationCount) {
                violationCount = parseInt(playerStats.Data.violationCount.Value || '0');
            }

            // Apply progressive penalties
            if (violationCount >= 10) {
                // Permanent ban for repeat offenders
                server.BanUsers({
                    Bans: [{
                        PlayFabId: playerId,
                        IPAddress: null,
                        Reason: "Automated ban: Multiple security violations detected"
                    }]
                });
            } else if (violationCount >= 5) {
                // 24-hour ban
                server.BanUsers({
                    Bans: [{
                        PlayFabId: playerId,
                        DurationInHours: 24,
                        Reason: "Automated ban: Security violations detected"
                    }]
                });
            } else if (violationCount >= 3) {
                // 1-hour ban
                server.BanUsers({
                    Bans: [{
                        PlayFabId: playerId,
                        DurationInHours: 1,
                        Reason: "Automated timeout: Security violations detected"
                    }]
                });
            }

        } catch (error) {
            log.error("Error applying penalty: " + error.message);
        }
    },

    getPlayerLastSubmissionTime: function(playerId) {
        try {
            var result = server.GetUserInternalData({
                PlayFabId: playerId,
                Keys: ["lastSubmissionTime"]
            });

            if (result.Data && result.Data.lastSubmissionTime) {
                return parseInt(result.Data.lastSubmissionTime.Value);
            }
        } catch (error) {
            log.error("Error getting last submission time: " + error.message);
        }
        return null;
    },

    updatePlayerLastSubmissionTime: function(playerId) {
        try {
            server.UpdateUserInternalData({
                PlayFabId: playerId,
                Data: {
                    lastSubmissionTime: Date.now().toString()
                }
            });
        } catch (error) {
            log.error("Error updating submission time: " + error.message);
        }
    }
};

// Make the module available to other CloudScript files
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ValidationModule;
} 