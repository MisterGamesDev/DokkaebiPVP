using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi.Core.Networking.Commands
{
    /// <summary>
    /// Command for ending the current turn phase
    /// </summary>
    public class EndTurnCommand : CommandBase
    {
        // Required for deserialization
        public EndTurnCommand() : base() { }

        public override string CommandType => "endTurn";

        public override Dictionary<string, object> Serialize()
        {
            return base.Serialize();
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            base.Deserialize(data);
        }

        public override bool Validate()
        {
            // TODO: Add validation logic, e.g., check if it's the correct player's turn.
            return true;
        }

        public override void Execute()
        {
            var turnSystem = Object.FindFirstObjectByType<DokkaebiTurnSystemCore>();
            if (turnSystem != null)
            {
                // This command should only be executed by the current player ending their turn.
                // The Turn System should handle which player's turn it becomes.
                // End the current phase
                turnSystem.NextPhase();
            }
            else
            {
                Debug.LogWarning("[EndTurnCommand] Executed, but TurnSystem not found.");
            }
        }
    }
} 