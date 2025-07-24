using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Abilities.Specific
{
    [CreateAssetMenu(fileName = "KarmicTetherAbility", menuName = "Dokkaebi/Abilities/Karmic Tether")]
    public class KarmicTetherAbility : AbilityData
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            abilityId = "KarmicTether";
            priorityType = ActionPriorityType.Defensive_Utility; // V3 Turn System priority - utility ability
        }
    }
} 