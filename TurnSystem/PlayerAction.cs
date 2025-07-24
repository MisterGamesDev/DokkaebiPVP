using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Units;
using Dokkaebi.Common;

namespace Dokkaebi.TurnSystem
{
    /// <summary>
    /// Represents a single action that a player can take in the V3 turn system.
    /// Each turn, players select one action for one unit.
    /// </summary>
    [System.Serializable]
    public class PlayerAction
    {
        [Header("Action Definition")]
        public ActionType actionType;
        public ActionPriorityType priorityType;
        
        [Header("Unit & Target")]
        public DokkaebiUnit actingUnit;
        public Vector2Int targetPosition;
        public DokkaebiUnit targetUnit;
        
        [Header("Ability-Specific")]
        public AbilityData abilityData;
        
        [Header("Movement-Specific")]
        public Vector2Int[] movementPath;
        
        [Header("Resolution")]
        public bool isResolved = false;
        public bool wasCanceled = false;
        public string cancelReason = "";
        
        /// <summary>
        /// Creates a movement action
        /// </summary>
        public static PlayerAction CreateMoveAction(DokkaebiUnit unit, Vector2Int targetPos, Vector2Int[] path = null)
        {
            return new PlayerAction
            {
                actionType = ActionType.Move,
                priorityType = ActionPriorityType.Movement,
                actingUnit = unit,
                targetPosition = targetPos,
                movementPath = path
            };
        }
        
        /// <summary>
        /// Creates an ability action
        /// </summary>
        public static PlayerAction CreateAbilityAction(DokkaebiUnit unit, AbilityData ability, Vector2Int targetPos, DokkaebiUnit targetUnit = null)
        {
            return new PlayerAction
            {
                actionType = ActionType.UseAbility,
                priorityType = ability.priorityType,
                actingUnit = unit,
                abilityData = ability,
                targetPosition = targetPos,
                targetUnit = targetUnit
            };
        }
        
        /// <summary>
        /// Checks if this action can still be executed (unit not stunned, target still valid, etc.)
        /// </summary>
        public bool CanExecute()
        {
            if (actingUnit == null || !actingUnit.IsAlive)
                return false;
                
            // Check if unit is stunned or otherwise unable to act
            if (actingUnit.HasStatusEffect(StatusEffectType.Stun))
                return false;
                
            // Additional validation can be added here
            return true;
        }
        
        /// <summary>
        /// Marks this action as canceled with a reason
        /// </summary>
        public void Cancel(string reason)
        {
            wasCanceled = true;
            cancelReason = reason;
        }
    }
    
    /// <summary>
    /// The type of action being performed
    /// </summary>
    public enum ActionType
    {
        Move,
        UseAbility
    }
} 