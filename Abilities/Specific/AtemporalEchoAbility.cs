using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Abilities.Specific
{
    [CreateAssetMenu(fileName = "AtemporalEchoAbility", menuName = "Dokkaebi/Abilities/Atemporal Echo")]
    public class AtemporalEchoAbility : AbilityData
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            abilityId = "AtemporalEcho";
            priorityType = ActionPriorityType.Defensive_Utility; // V3 Turn System priority - utility ability
        }
    }
} 