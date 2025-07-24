using System;

namespace Dokkaebi.Common
{
    /// <summary>
    /// Common enums for cross-module reference to avoid circular dependencies.
    /// </summary>
    
    /// <summary>
    /// Phases within a game turn - Simplified V3 System
    /// </summary>
    public enum TurnPhase
    {
        // V3 Simplified System
        ActionSelection,    // Players simultaneously select actions (move, abilities, etc.)
        ActionResolution,   // Server processes and resolves all actions
        TurnComplete,       // Turn cleanup and preparation for next turn
        
        // Legacy phases (deprecated - use V3TurnPhase instead)
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        Opening,            
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        MovementPhase,      
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        BufferPhase,        
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        AuraPhase1A,       
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        AuraPhase1B,       
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        AuraPhase2A,       
        [System.Obsolete("Use V3TurnPhase.ActionSelection instead")]
        AuraPhase2B,       
        [System.Obsolete("Use V3TurnPhase.ActionResolution instead")]
        Resolution,        
        [System.Obsolete("Use V3TurnPhase.TurnComplete instead")]
        EndTurn,          
        
        // Game end state
        GameOver          
    }

    /// <summary>
    /// Types of zones
    /// </summary>
    public enum ZoneType
    {
        Damage,
        Healing,
        SpeedBuff,
        SpeedDebuff,
        DamageBuff,
        DamageDebuff,
        Block,
        Teleport,
        Vision,
        Stealth,
        Trap
    }

    /// <summary>
    /// Team types for units
    /// </summary>
    public enum TeamType
    {
        Player1,
        Player2,
        Neutral,
        Environment
    }

    /// <summary>
    /// Types of targeting for abilities
    /// </summary>
    public enum TargetingType
    {
        Point,           // Target a specific point on the grid
        Unit,            // Target a specific unit
        Area,            // Target an area (like a circle or square)
        Line,            // Target a line from the caster
        Cone,            // Target a cone from the caster
        Self,            // Target the caster
        AllUnits,        // Target all units in the game
        AlliedUnits,     // Target all allied units
        EnemyUnits       // Target all enemy units
    }

    /// <summary>
    /// Targeting relationship requirements for abilities
    /// </summary>
    public enum TargetRelationship
    {
        Any,            // Can target any unit
        Ally,           // Can only target allied units
        Enemy,          // Can only target enemy units
        Self,           // Can only target self
        NotSelf         // Can target any unit except self
    }
} 