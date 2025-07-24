using UnityEngine;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
using Dokkaebi.Grid;

namespace Dokkaebi.Zones
{
    /// <summary>
    /// Abstract base class for all zone implementations
    /// Contains common properties and functionality
    /// </summary>
    public abstract class ZoneBase : MonoBehaviour
    {
        [Header("Core Zone Properties")]
        [SerializeField] protected string zoneId;
        [SerializeField] protected string zoneType;
        [SerializeField] protected GridPosition position;
        [SerializeField] protected int remainingDuration;
        [SerializeField] protected int ownerUnitId = -1;
        [SerializeField] protected bool isActive = true;
        
        // Public properties
        public string ZoneId => zoneId;
        public string ZoneType => zoneType;
        public virtual GridPosition Position => position;
        public virtual int RemainingDuration => remainingDuration;
        public virtual int OwnerUnitId => ownerUnitId;
        public virtual bool IsActive => isActive;
        public virtual int Id => GetInstanceID();
        
        /// <summary>
        /// Initialize the zone with basic parameters
        /// </summary>
        public virtual void Initialize(string id, string type, GridPosition pos, int duration, int owner = -1)
        {
            zoneId = id;
            zoneType = type;
            position = pos;
            remainingDuration = duration;
            ownerUnitId = owner;
            isActive = true;
            
            SmartLogger.Log($"[ZoneBase.Initialize] Initialized zone '{type}' (ID: {id}) at {pos}", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Process zone's turn-based logic (duration, effects, etc.)
        /// </summary>
        public virtual void ProcessTurn()
        {
            if (!isActive) return;
            
            // Decrement duration for non-permanent zones
            if (remainingDuration > 0)
            {
                remainingDuration--;
                SmartLogger.Log($"[ZoneBase.ProcessTurn] Zone '{zoneType}' duration: {remainingDuration + 1} -> {remainingDuration}", LogCategory.Zone, this);
                
                if (remainingDuration <= 0)
                {
                    Deactivate();
                }
            }
        }
        
        /// <summary>
        /// Deactivate the zone
        /// </summary>
        public virtual void Deactivate()
        {
            isActive = false;
            SmartLogger.Log($"[ZoneBase.Deactivate] Zone '{zoneType}' (ID: {zoneId}) deactivated", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Check if a position is within this zone's area
        /// </summary>
        public abstract bool ContainsPosition(GridPosition pos);
        
        /// <summary>
        /// Get the effective radius/size of this zone
        /// </summary>
        public abstract int GetRadius();
        
        /// <summary>
        /// Set the zone's position
        /// </summary>
        public virtual void SetPosition(GridPosition newPosition)
        {
            position = newPosition;
            
            // Update world position if GridManager is available
            if (GridManager.Instance != null)
            {
                Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(newPosition);
                worldPosition.y += 0.1f; // Raise slightly above ground
                transform.position = worldPosition;
                SmartLogger.Log($"[ZoneBase.SetPosition] Set zone position to {newPosition}, world pos: {worldPosition}", LogCategory.Zone, this);
            }
        }
        
        /// <summary>
        /// Set remaining duration
        /// </summary>
        public virtual void SetDuration(int duration)
        {
            remainingDuration = duration;
        }
        
        /// <summary>
        /// Check if zone has expired
        /// </summary>
        public virtual bool IsExpired()
        {
            return remainingDuration <= 0;
        }
    }
} 