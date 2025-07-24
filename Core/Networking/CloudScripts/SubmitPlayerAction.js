/**
 * Enhanced PlayFab CloudScript: SubmitPlayerAction with comprehensive server-side validation
 * Handles all combat actions with authoritative validation and anti-cheat measures
 * Ensures game integrity through server-side logic
 */

// Game configuration constants
const GRID_SIZE = 10;
const MAX_MOVEMENT_RANGE = 3;
const MAX_ABILITY_RANGE = 5;
const TURN_TIME_LIMIT_MS = 60000; // 60 seconds
const MAX_ACTIONS_PER_TURN = 5;

handlers.SubmitPlayerAction = function(args, context) {
    try {
        // 1. AUTHENTICATION & AUTHORIZATION
        const playerId = context.currentPlayerId;
        if (!playerId) {
            return createErrorResponse("UNAUTHORIZED", "Invalid player authentication");
        }

        // 2. INPUT VALIDATION
        const validationResult = validateInput(args);
        if (!validationResult.isValid) {
            return createErrorResponse("INVALID_INPUT", validationResult.error);
        }

        const { matchId, action } = args;

        // 3. RETRIEVE CURRENT GAME STATE
        const gameState = getGameState(matchId);
        if (!gameState) {
            return createErrorResponse("MATCH_NOT_FOUND", "Game match does not exist");
        }

        // 4. VALIDATE PLAYER PARTICIPATION
        const playerNumber = getPlayerNumber(gameState, playerId);
        if (playerNumber === -1) {
            return createErrorResponse("NOT_IN_MATCH", "Player is not part of this match");
        }

        // 5. VALIDATE TURN STATE
        const turnValidation = validateTurnState(gameState, playerNumber);
        if (!turnValidation.isValid) {
            return createErrorResponse("INVALID_TURN_STATE", turnValidation.error);
        }

        // 6. VALIDATE ACTION BASED ON TYPE
        const actionValidation = validateAction(gameState, action, playerNumber);
        if (!actionValidation.isValid) {
            return createErrorResponse("INVALID_ACTION", actionValidation.error);
        }

        // 7. ANTI-CHEAT VALIDATION
        const antiCheatResult = performAntiCheatChecks(gameState, action, playerId);
        if (!antiCheatResult.isValid) {
            logCheatAttempt(playerId, matchId, action.actionType, antiCheatResult.cheatType);
            return createErrorResponse("CHEAT_DETECTED", "Invalid action detected");
        }

        // 8. APPLY ACTION TO GAME STATE
        const updatedGameState = applyActionToGameState(gameState, action, playerNumber);
        
        // 9. CHECK FOR TURN COMPLETION
        const bothPlayersReady = checkBothPlayersSubmitted(updatedGameState);
        
        if (bothPlayersReady) {
            // 10. RESOLVE TURN (Process all actions simultaneously)
            const resolvedState = resolveTurn(updatedGameState);
            
            // 11. CHECK GAME END CONDITIONS
            const gameEndResult = checkGameEndConditions(resolvedState);
            if (gameEndResult.gameEnded) {
                resolvedState.gameStatus = "COMPLETED";
                resolvedState.winner = gameEndResult.winner;
                resolvedState.endReason = gameEndResult.reason;
            }
            
            // 12. ADVANCE TURN
            if (!gameEndResult.gameEnded) {
                advanceToNextTurn(resolvedState);
            }
            
            // 13. SAVE STATE AND NOTIFY
            saveGameState(matchId, resolvedState);
            notifyPlayersOfStateUpdate(matchId, resolvedState);
            
            return createSuccessResponse(resolvedState, "Turn resolved successfully");
        } else {
            // Save intermediate state
            saveGameState(matchId, updatedGameState);
            return createSuccessResponse(updatedGameState, "Action submitted successfully");
        }

    } catch (error) {
        log.error("Error in SubmitPlayerAction: " + error.message, error);
        return createErrorResponse("INTERNAL_ERROR", "Server error occurred");
    }
};

/**
 * VALIDATION FUNCTIONS
 */

function validateInput(args) {
    if (!args.matchId || typeof args.matchId !== 'string') {
        return { isValid: false, error: "Missing or invalid matchId" };
    }
    
    if (!args.action || typeof args.action !== 'object') {
        return { isValid: false, error: "Missing or invalid action object" };
    }
    
    const { action } = args;
    
    if (!action.actionType || !['move', 'ability', 'pass'].includes(action.actionType)) {
        return { isValid: false, error: "Invalid action type" };
    }
    
    if (action.actionType !== 'pass') {
        if (!action.unitId || typeof action.unitId !== 'number') {
            return { isValid: false, error: "Missing or invalid unitId" };
        }
    }
    
    return { isValid: true };
}

function validateTurnState(gameState, playerNumber) {
    // Check if game is active
    if (gameState.gameStatus !== 'ACTIVE') {
        return { isValid: false, error: "Game is not in active state" };
    }
    
    // Check if it's the action submission phase
    if (gameState.currentPhase !== 'ActionSubmission') {
        return { isValid: false, error: "Not in action submission phase" };
    }
    
    // Check if player has already submitted for this turn
    const playerKey = `player${playerNumber}`;
    if (gameState[playerKey].hasSubmitted) {
        return { isValid: false, error: "Player has already submitted actions for this turn" };
    }
    
    // Check turn time limit
    const currentTime = Date.now();
    const turnStartTime = gameState.turnStartTime || currentTime;
    if (currentTime - turnStartTime > TURN_TIME_LIMIT_MS) {
        return { isValid: false, error: "Turn time limit exceeded" };
    }
    
    return { isValid: true };
}

function validateAction(gameState, action, playerNumber) {
    const { actionType, unitId, targetPosition, abilityId, targetUnitId } = action;
    
    // 1. Validate unit ownership
    if (actionType !== 'pass') {
        const unit = findUnit(gameState, unitId);
        if (!unit) {
            return { isValid: false, error: `Unit ${unitId} not found` };
        }
        
        if (unit.owner !== playerNumber) {
            return { isValid: false, error: "Player does not own this unit" };
        }
        
        // Check if unit is alive
        if (unit.currentHealth <= 0) {
            return { isValid: false, error: "Unit is destroyed and cannot act" };
        }
        
        // Check if unit has already acted this turn
        if (unit.hasActed) {
            return { isValid: false, error: "Unit has already acted this turn" };
        }
    }
    
    // 2. Validate specific action types
    switch (actionType) {
        case 'move':
            return validateMoveAction(gameState, unitId, targetPosition);
        case 'ability':
            return validateAbilityAction(gameState, unitId, abilityId, targetPosition, targetUnitId);
        case 'pass':
            return { isValid: true }; // Pass is always valid
        default:
            return { isValid: false, error: "Unknown action type" };
    }
}

function validateMoveAction(gameState, unitId, targetPosition) {
    if (!targetPosition || typeof targetPosition.x !== 'number' || typeof targetPosition.z !== 'number') {
        return { isValid: false, error: "Invalid target position" };
    }
    
    // Check bounds
    if (targetPosition.x < 0 || targetPosition.x >= GRID_SIZE || 
        targetPosition.z < 0 || targetPosition.z >= GRID_SIZE) {
        return { isValid: false, error: "Target position is out of bounds" };
    }
    
    const unit = findUnit(gameState, unitId);
    const currentPos = unit.position;
    
    // Check movement distance
    const distance = Math.abs(targetPosition.x - currentPos.x) + Math.abs(targetPosition.z - currentPos.z);
    if (distance > MAX_MOVEMENT_RANGE) {
        return { isValid: false, error: `Movement distance ${distance} exceeds maximum range ${MAX_MOVEMENT_RANGE}` };
    }
    
    // Check if target position is occupied
    if (isPositionOccupied(gameState, targetPosition)) {
        return { isValid: false, error: "Target position is occupied" };
    }
    
    // Check for path obstructions (simplified - in a real game you'd do pathfinding)
    if (!hasValidPath(gameState, currentPos, targetPosition)) {
        return { isValid: false, error: "Path to target is blocked" };
    }
    
    return { isValid: true };
}

function validateAbilityAction(gameState, unitId, abilityId, targetPosition, targetUnitId) {
    if (!abilityId || typeof abilityId !== 'string') {
        return { isValid: false, error: "Invalid ability ID" };
    }
    
    const unit = findUnit(gameState, unitId);
    
    // Check if unit has this ability
    if (!unit.abilities || !unit.abilities.includes(abilityId)) {
        return { isValid: false, error: "Unit does not have this ability" };
    }
    
    // Get ability data (this would be loaded from game configuration)
    const ability = getAbilityData(abilityId);
    if (!ability) {
        return { isValid: false, error: "Ability data not found" };
    }
    
    // Check aura cost
    const playerKey = `player${unit.owner}`;
    const playerData = gameState[playerKey];
    if (playerData.currentAura < ability.auraCost) {
        return { isValid: false, error: "Insufficient aura to use ability" };
    }
    
    // Validate targeting based on ability type
    if (ability.targetingType === 'position') {
        if (!targetPosition) {
            return { isValid: false, error: "Ability requires target position" };
        }
        
        // Check range to target position
        const distance = Math.abs(targetPosition.x - unit.position.x) + Math.abs(targetPosition.z - unit.position.z);
        if (distance > ability.range) {
            return { isValid: false, error: `Target position is out of range (${distance} > ${ability.range})` };
        }
    } else if (ability.targetingType === 'unit') {
        if (!targetUnitId) {
            return { isValid: false, error: "Ability requires target unit" };
        }
        
        const targetUnit = findUnit(gameState, targetUnitId);
        if (!targetUnit) {
            return { isValid: false, error: "Target unit not found" };
        }
        
        // Check range to target unit
        const distance = Math.abs(targetUnit.position.x - unit.position.x) + Math.abs(targetUnit.position.z - unit.position.z);
        if (distance > ability.range) {
            return { isValid: false, error: `Target unit is out of range (${distance} > ${ability.range})` };
        }
        
        // Check targeting restrictions (friendly/enemy)
        if (ability.canTargetFriendly === false && targetUnit.owner === unit.owner) {
            return { isValid: false, error: "Cannot target friendly units with this ability" };
        }
        if (ability.canTargetEnemy === false && targetUnit.owner !== unit.owner) {
            return { isValid: false, error: "Cannot target enemy units with this ability" };
        }
    }
    
    return { isValid: true };
}

/**
 * ANTI-CHEAT FUNCTIONS
 */

function performAntiCheatChecks(gameState, action, playerId) {
    // 1. Check for impossible actions
    if (action.actionType === 'move' && action.targetPosition) {
        const unit = findUnit(gameState, action.unitId);
        const distance = Math.abs(action.targetPosition.x - unit.position.x) + Math.abs(action.targetPosition.z - unit.position.z);
        if (distance > MAX_MOVEMENT_RANGE) {
            return { isValid: false, cheatType: "IMPOSSIBLE_MOVEMENT" };
        }
    }
    
    // 2. Check for rapid-fire submissions (spam protection)
    const lastSubmissionTime = getPlayerLastSubmissionTime(playerId);
    const currentTime = Date.now();
    if (lastSubmissionTime && (currentTime - lastSubmissionTime) < 100) { // 100ms minimum between actions
        return { isValid: false, cheatType: "RAPID_SUBMISSIONS" };
    }
    
    // 3. Check for duplicate actions
    const playerKey = `player${getPlayerNumber(gameState, playerId)}`;
    const existingActions = gameState[playerKey].submittedActions || [];
    for (let existingAction of existingActions) {
        if (JSON.stringify(existingAction) === JSON.stringify(action)) {
            return { isValid: false, cheatType: "DUPLICATE_ACTION" };
        }
    }
    
    // 4. Check action count limit
    if (existingActions.length >= MAX_ACTIONS_PER_TURN) {
        return { isValid: false, cheatType: "TOO_MANY_ACTIONS" };
    }
    
    return { isValid: true };
}

function logCheatAttempt(playerId, matchId, actionType, cheatType) {
    const cheatLog = {
        playerId: playerId,
        matchId: matchId,
        actionType: actionType,
        cheatType: cheatType,
        timestamp: Date.now()
    };
    
    log.error("CHEAT ATTEMPT DETECTED", cheatLog);
    
    // Store cheat attempt in player statistics
    const playerStats = server.GetUserInternalData({
        PlayFabId: playerId,
        Keys: ["cheatAttempts"]
    });
    
    let cheatAttempts = 0;
    if (playerStats.Data && playerStats.Data.cheatAttempts) {
        cheatAttempts = parseInt(playerStats.Data.cheatAttempts.Value) || 0;
    }
    cheatAttempts++;
    
    server.UpdateUserInternalData({
        PlayFabId: playerId,
        Data: {
            cheatAttempts: cheatAttempts.toString(),
            lastCheatAttempt: JSON.stringify(cheatLog)
        }
    });
    
    // Auto-ban if too many cheat attempts
    if (cheatAttempts >= 5) {
        server.BanUsers({
            Bans: [{
                PlayFabId: playerId,
                DurationInHours: 24,
                Reason: `Automated ban: ${cheatAttempts} cheat attempts detected`
            }]
        });
    }
}

/**
 * GAME STATE FUNCTIONS
 */

function getGameState(matchId) {
    try {
        const result = server.GetTitleInternalData({
            Keys: [matchId]
        });
        
        if (result.Data && result.Data[matchId]) {
            return JSON.parse(result.Data[matchId]);
        }
        return null;
    } catch (error) {
        log.error("Error retrieving game state: " + error.message);
        return null;
    }
}

function saveGameState(matchId, gameState) {
    try {
        gameState.lastUpdated = Date.now();
        
        server.SetTitleInternalData({
            Data: {
                [matchId]: JSON.stringify(gameState)
            }
        });
        
        log.info("Game state saved for match: " + matchId);
    } catch (error) {
        log.error("Error saving game state: " + error.message);
    }
}

function applyActionToGameState(gameState, action, playerNumber) {
    const updatedState = JSON.parse(JSON.stringify(gameState)); // Deep clone
    
    const playerKey = `player${playerNumber}`;
    if (!updatedState[playerKey].submittedActions) {
        updatedState[playerKey].submittedActions = [];
    }
    
    updatedState[playerKey].submittedActions.push(action);
    updatedState[playerKey].hasSubmitted = true;
    updatedState[playerKey].submissionTime = Date.now();
    
    return updatedState;
}

function resolveTurn(gameState) {
    const resolvedState = JSON.parse(JSON.stringify(gameState)); // Deep clone
    
    // Get all actions from both players
    const player1Actions = resolvedState.player1.submittedActions || [];
    const player2Actions = resolvedState.player2.submittedActions || [];
    
    // Process all actions (order matters for simultaneous resolution)
    const allActions = [];
    
    // Interleave actions for fair processing
    const maxActions = Math.max(player1Actions.length, player2Actions.length);
    for (let i = 0; i < maxActions; i++) {
        if (i < player1Actions.length) {
            allActions.push({ ...player1Actions[i], playerNumber: 1 });
        }
        if (i < player2Actions.length) {
            allActions.push({ ...player2Actions[i], playerNumber: 2 });
        }
    }
    
    // Execute each action
    for (let action of allActions) {
        executeAction(resolvedState, action);
    }
    
    // Apply end-of-turn effects (zone damage, aura regeneration, etc.)
    applyEndOfTurnEffects(resolvedState);
    
    return resolvedState;
}

function executeAction(gameState, action) {
    switch (action.actionType) {
        case 'move':
            executeMoveAction(gameState, action);
            break;
        case 'ability':
            executeAbilityAction(gameState, action);
            break;
        case 'pass':
            // No action needed for pass
            break;
    }
}

function executeMoveAction(gameState, action) {
    const unit = findUnit(gameState, action.unitId);
    if (unit && unit.currentHealth > 0) {
        // Clear old position in grid
        clearUnitFromGrid(gameState, unit);
        
        // Update unit position
        unit.position = action.targetPosition;
        unit.hasActed = true;
        
        // Set new position in grid
        setUnitInGrid(gameState, unit);
        
        log.info(`Unit ${action.unitId} moved to (${action.targetPosition.x}, ${action.targetPosition.z})`);
    }
}

function executeAbilityAction(gameState, action) {
    const unit = findUnit(gameState, action.unitId);
    if (!unit || unit.currentHealth <= 0) return;
    
    const ability = getAbilityData(action.abilityId);
    if (!ability) return;
    
    // Deduct aura cost
    const playerKey = `player${unit.owner}`;
    gameState[playerKey].currentAura = Math.max(0, gameState[playerKey].currentAura - ability.auraCost);
    
    // Apply ability effects
    switch (ability.effectType) {
        case 'damage':
            applyDamageEffect(gameState, action, ability);
            break;
        case 'heal':
            applyHealEffect(gameState, action, ability);
            break;
        case 'zone':
            createZone(gameState, action, ability);
            break;
        // Add more effect types as needed
    }
    
    unit.hasActed = true;
    log.info(`Unit ${action.unitId} used ability ${action.abilityId}`);
}

/**
 * HELPER FUNCTIONS
 */

function findUnit(gameState, unitId) {
    for (let unit of gameState.units) {
        if (unit.unitId === unitId) {
            return unit;
        }
    }
    return null;
}

function getPlayerNumber(gameState, playerId) {
    if (gameState.player1.playerId === playerId) return 1;
    if (gameState.player2.playerId === playerId) return 2;
    return -1;
}

function checkBothPlayersSubmitted(gameState) {
    return gameState.player1.hasSubmitted && gameState.player2.hasSubmitted;
}

function advanceToNextTurn(gameState) {
    gameState.currentTurn++;
    gameState.currentPhase = 'ActionSubmission';
    gameState.turnStartTime = Date.now();
    
    // Reset player submission states
    gameState.player1.hasSubmitted = false;
    gameState.player1.submittedActions = [];
    gameState.player2.hasSubmitted = false;
    gameState.player2.submittedActions = [];
    
    // Reset unit acted flags
    for (let unit of gameState.units) {
        unit.hasActed = false;
    }
    
    // Regenerate aura
    gameState.player1.currentAura = Math.min(gameState.player1.maxAura, gameState.player1.currentAura + 2);
    gameState.player2.currentAura = Math.min(gameState.player2.maxAura, gameState.player2.currentAura + 2);
}

function checkGameEndConditions(gameState) {
    // Check if all units of a player are destroyed
    const player1Units = gameState.units.filter(u => u.owner === 1 && u.currentHealth > 0);
    const player2Units = gameState.units.filter(u => u.owner === 2 && u.currentHealth > 0);
    
    if (player1Units.length === 0) {
        return { gameEnded: true, winner: 2, reason: "All Player 1 units destroyed" };
    }
    
    if (player2Units.length === 0) {
        return { gameEnded: true, winner: 1, reason: "All Player 2 units destroyed" };
    }
    
    // Check turn limit (prevent infinite games)
    if (gameState.currentTurn >= 50) {
        const p1Health = player1Units.reduce((sum, u) => sum + u.currentHealth, 0);
        const p2Health = player2Units.reduce((sum, u) => sum + u.currentHealth, 0);
        
        if (p1Health > p2Health) {
            return { gameEnded: true, winner: 1, reason: "Turn limit reached - Player 1 has more health" };
        } else if (p2Health > p1Health) {
            return { gameEnded: true, winner: 2, reason: "Turn limit reached - Player 2 has more health" };
        } else {
            return { gameEnded: true, winner: 0, reason: "Turn limit reached - Draw" };
        }
    }
    
    return { gameEnded: false };
}

function notifyPlayersOfStateUpdate(matchId, gameState) {
    // Send push notifications to both players about state update
    const notification = {
        Subject: "Game State Updated",
        Message: `Match ${matchId} has been updated. Turn: ${gameState.currentTurn}`,
        TargetPlayers: [gameState.player1.playerId, gameState.player2.playerId]
    };
    
    // This would use PlayFab's push notification system
    // server.SendPushNotification(notification);
}

function createErrorResponse(errorCode, message) {
    return {
        success: false,
        errorCode: errorCode,
        errorMessage: message,
        timestamp: Date.now()
    };
}

function createSuccessResponse(gameState, message) {
    return {
        success: true,
        message: message,
        gameState: gameState,
        timestamp: Date.now()
    };
}

// Additional helper functions would be implemented here...
function isPositionOccupied(gameState, position) {
    return gameState.units.some(unit => 
        unit.currentHealth > 0 && 
        unit.position.x === position.x && 
        unit.position.z === position.z
    );
}

function hasValidPath(gameState, fromPos, toPos) {
    // Simplified pathfinding check - in production, use proper A* pathfinding
    return true; // For now, assume all paths are valid
}

function getAbilityData(abilityId) {
    // This would load from game configuration
    // For demo purposes, return a sample ability
    return {
        id: abilityId,
        auraCost: 2,
        range: 3,
        effectType: 'damage',
        damage: 25,
        targetingType: 'unit',
        canTargetFriendly: false,
        canTargetEnemy: true
    };
}

function getPlayerLastSubmissionTime(playerId) {
    // This would track per-player submission timestamps
    return null; // Simplified for demo
}

function clearUnitFromGrid(gameState, unit) {
    // Update grid occupancy data
}

function setUnitInGrid(gameState, unit) {
    // Update grid occupancy data
}

function applyDamageEffect(gameState, action, ability) {
    const targetUnit = findUnit(gameState, action.targetUnitId);
    if (targetUnit && targetUnit.currentHealth > 0) {
        targetUnit.currentHealth = Math.max(0, targetUnit.currentHealth - ability.damage);
        log.info(`Unit ${action.targetUnitId} took ${ability.damage} damage, health now ${targetUnit.currentHealth}`);
    }
}

function applyHealEffect(gameState, action, ability) {
    const targetUnit = findUnit(gameState, action.targetUnitId);
    if (targetUnit && targetUnit.currentHealth > 0) {
        targetUnit.currentHealth = Math.min(targetUnit.maxHealth, targetUnit.currentHealth + ability.healing);
        log.info(`Unit ${action.targetUnitId} healed for ${ability.healing}, health now ${targetUnit.currentHealth}`);
    }
}

function createZone(gameState, action, ability) {
    // Create a zone effect at the target position
    const zone = {
        id: generateZoneId(),
        position: action.targetPosition,
        type: ability.zoneType,
        duration: ability.zoneDuration,
        effects: ability.zoneEffects,
        owner: getPlayerNumber(gameState, action.unitId)
    };
    
    if (!gameState.zones) {
        gameState.zones = [];
    }
    gameState.zones.push(zone);
    
    log.info(`Zone ${zone.id} created at (${action.targetPosition.x}, ${action.targetPosition.z})`);
}

function applyEndOfTurnEffects(gameState) {
    // Apply zone effects to units
    if (gameState.zones) {
        for (let zone of gameState.zones) {
            applyZoneEffectsToUnits(gameState, zone);
            zone.duration--;
        }
        
        // Remove expired zones
        gameState.zones = gameState.zones.filter(zone => zone.duration > 0);
    }
}

function applyZoneEffectsToUnits(gameState, zone) {
    const affectedUnits = gameState.units.filter(unit => 
        unit.currentHealth > 0 &&
        unit.position.x === zone.position.x && 
        unit.position.z === zone.position.z
    );
    
    for (let unit of affectedUnits) {
        // Apply zone effects based on zone type
        if (zone.type === 'damage') {
            unit.currentHealth = Math.max(0, unit.currentHealth - (zone.effects.damage || 5));
        } else if (zone.type === 'heal') {
            unit.currentHealth = Math.min(unit.maxHealth, unit.currentHealth + (zone.effects.healing || 5));
        }
    }
}

function generateZoneId() {
    return 'zone_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
} 