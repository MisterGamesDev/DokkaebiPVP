using UnityEngine;
using TMPro;
using Dokkaebi.Core;
using Dokkaebi.Grid;

namespace Dokkaebi.UI
{
    public class DebugUIController : MonoBehaviour {
        public TextMeshProUGUI debugText;
        private DokkaebiTurnSystemCore turnSystem;
        private GridManager gridManager;
        
        void Start() {
            turnSystem = FindFirstObjectByType<DokkaebiTurnSystemCore>();
            gridManager = FindFirstObjectByType<GridManager>();
        }
        
        void Update() {
            if (turnSystem != null && debugText != null) {
                debugText.text = $"Turn: {turnSystem.CurrentTurn}\n" +
                                $"Phase: {turnSystem.CurrentPhase}\n" +
                                $"Active Player: {turnSystem.ActivePlayerId}";
            }
        }
    }
}