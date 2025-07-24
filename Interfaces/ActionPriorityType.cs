using System.ComponentModel;

namespace Dokkaebi.Interfaces
{
    /// <summary>
    /// Defines the priority order for action resolution in the V3 turn system.
    /// Lower numeric values resolve first.
    /// </summary>
    public enum ActionPriorityType
    {
        [Description("Reactive actions that trigger automatically in response to conditions.")]
        Reactive = 0,
        
        [Description("Defensive buffs, healing, and utility/control abilities.")]
        Defensive_Utility = 1,
        
        [Description("Standard unit movement actions.")]
        Movement = 2,
        
        [Description("Offensive abilities that primarily deal damage.")]
        Offensive = 3
    }
} 