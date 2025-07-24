# V3 Turn System Migration Guide

## Overview
This guide explains how to transition from the complex multi-phase `DokkaebiTurnSystemCore` to the simplified V3 Turn System.

## 🔄 **BEFORE vs AFTER**

### **Old System (DokkaebiTurnSystemCore)**
```
Turn Flow: 25+ seconds across 8+ phases
├── Opening (3s)
├── MovementPhase (15s) 
├── BufferPhase (2s)
├── AuraPhase1A (5s) - Player 1 only
├── AuraPhase1B (5s) - Player 2 only  
├── AuraPhase2A (5s) - Player 1 only
├── AuraPhase2B (5s) - Player 2 only
└── Resolution → EndTurn
```

### **New V3 System (V3TurnManager)**
```
Turn Flow: ~30 seconds across 3 phases  
├── ActionSelection (30s) - Both players act simultaneously
├── ActionResolution (2s) - Server processes actions
└── TurnComplete (1s) - Cleanup and next turn
```

## ⚙️ **Unity Inspector Settings Update**

### **For DokkaebiTurnSystemCore (Legacy Mode)**
Update your Unity Inspector settings to:

```
Phase Transition Delay: 0.5
Phase Durations:
├── Opening Phase Duration: 1        # Quick startup
├── Movement Phase Duration: 30      # Combined action phase  
├── Aura Charging Phase Duration: 0  # ❌ DISABLED (combined with movement)
└── Buffer Phase Duration: 1         # Quick cleanup
```

### **For V3TurnManager (Recommended)**
```
Action Selection Time Limit: 30      # Time for both players to submit
Phase Transition Delay: 0.5          # Smooth transitions
Enable Real-time Resolution: ✅       # Immediate when both submit
```

## 🎯 **Migration Steps**

### **Step 1: Update Inspector Settings** 
1. Select your `DokkaebiTurnSystemCore` in the scene
2. Update phase durations as shown above
3. Set `Aura Charging Phase Duration = 0` to disable it

### **Step 2: Transition to V3TurnManager (Recommended)**
1. Add `V3TurnManager` component to your scene
2. Configure action selection time limit (30 seconds)
3. Gradually migrate components to reference V3TurnManager
4. Test multiplayer functionality

### **Step 3: Update Code References**
```csharp
// Old way
if (turnSystem.CurrentPhase == TurnPhase.MovementPhase)
{
    // Handle movement
}
else if (turnSystem.CurrentPhase == TurnPhase.AuraPhase1A)
{
    // Handle aura
}

// New V3 way
if (v3TurnManager.CurrentPhase == V3TurnPhase.ActionSelection)
{
    // Handle all player actions (movement, abilities, etc.)
}
```

## 🚀 **Benefits of V3 System**

### **Simplified Gameplay**
- ✅ **Faster turns** - No waiting between movement/aura phases
- ✅ **Simultaneous actions** - Both players act at once
- ✅ **Combined phases** - Move and use abilities in same turn
- ✅ **Multiplayer ready** - Built for online PvP

### **Technical Benefits**  
- ✅ **Server-side validation** - Anti-cheat protection
- ✅ **Real-time resolution** - Turns end when both players submit
- ✅ **Cleaner code** - Fewer state transitions to manage
- ✅ **Mobile optimized** - Shorter attention spans, faster gameplay

## ⚠️ **Compatibility Notes**

### **Existing Code**
- Old `TurnPhase` enums are marked as `[Obsolete]`
- Compiler warnings will guide you to use `V3TurnPhase` instead
- Legacy components will still work but are not recommended

### **UI Components**
- Update UI components to listen for V3 events:
```csharp
// Update event subscriptions
v3TurnManager.OnTurnStarted.AddListener(OnTurnStarted);
v3TurnManager.OnPhaseChanged.AddListener(OnPhaseChanged);
```

## 🎮 **Recommended Configuration**

### **Scene Setup**
```
BattleScene:
├── V3TurnManager (Primary) ⭐
│   ├── Action Selection Time: 30s
│   ├── Enable Multiplayer: ✅
│   └── Real-time Resolution: ✅
├── DokkaebiTurnSystemCore (Legacy/Fallback) 
│   ├── Movement Phase: 30s
│   ├── Aura Phase: 0s (disabled)
│   └── Buffer Phase: 1s
└── PlayerActionManager (Connects both systems)
```

## 📝 **Next Steps**

1. **Immediate:** Update inspector settings to disable aura phases
2. **Short term:** Add V3TurnManager to your scene  
3. **Long term:** Migrate all components to use V3TurnManager
4. **Future:** Remove DokkaebiTurnSystemCore entirely

---

**🎯 Result: Your turn system will be faster, simpler, and ready for competitive multiplayer!** 