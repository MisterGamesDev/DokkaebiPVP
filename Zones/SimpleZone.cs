using UnityEngine;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;

namespace Dokkaebi.Zones
{
    /// <summary>
    /// Simple zone implementation for basic tracking and networking
    /// Replaces the old Zone class with cleaner inheritance
    /// </summary>
    public class SimpleZone : ZoneBase
    {
        [Header("Simple Zone Properties")]
        [SerializeField] private int size = 1; // Zone radius/size
        
        // Legacy compatibility properties for Vector2Int
        public Vector2Int Position_Vector2Int => new Vector2Int(position.x, position.z);
        public int Size => size;
        
        /// <summary>
        /// Initialize zone with Vector2Int position (legacy compatibility)
        /// </summary>
        public void Initialize(string id, string type, Vector2Int pos, int zoneSize, int duration, string ownerUnitIdString)
        {
            // Convert Vector2Int to GridPosition using the correct method
            GridPosition gridPos = GridPosition.FromVector2Int(pos);
            
            // Parse owner unit ID
            int ownerUnit = -1;
            if (!string.IsNullOrEmpty(ownerUnitIdString))
            {
                int.TryParse(ownerUnitIdString, out ownerUnit);
            }
            
            // Set size
            size = zoneSize;
            
            // Call base initialization
            base.Initialize(id, type, gridPos, duration, ownerUnit);
            
            SmartLogger.Log($"[SimpleZone.Initialize] Created simple zone '{type}' at {pos} with size {zoneSize}", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Check if a position is within this zone's area
        /// Uses Manhattan distance based on size
        /// </summary>
        public override bool ContainsPosition(GridPosition pos)
        {
            int dx = Mathf.Abs(pos.x - position.x);
            int dz = Mathf.Abs(pos.z - position.z);
            return dx + dz <= size;
        }
        
        /// <summary>
        /// Get the effective radius of this zone
        /// </summary>
        public override int GetRadius()
        {
            return size;
        }
        
        /// <summary>
        /// Set zone position using Vector2Int (legacy compatibility)
        /// </summary>
        public void SetPosition(Vector2Int newPosition)
        {
            GridPosition gridPos = GridPosition.FromVector2Int(newPosition);
            SetPosition(gridPos);
        }
        
        /// <summary>
        /// Reduce duration by one turn (legacy compatibility method)
        /// </summary>
        public void DecrementDuration()
        {
            if (remainingDuration > 0)
            {
                remainingDuration--;
                if (remainingDuration <= 0)
                {
                    Deactivate();
                }
            }
        }
    }
} 