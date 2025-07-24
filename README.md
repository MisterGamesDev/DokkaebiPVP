# Dokkaebi - 3D Turn-Based Strategy with Multiplayer-First Architecture

## 1. Introduction

Dokkaebi is a **multiplayer-first** 3D Turn-Based Strategy game designed for competitive PvP combat. Built for PC (Steam) and Mobile (iOS/Android) with full cross-platform multiplayer support using PlayFab's authoritative server architecture. Matches feature intense 6v6 squad combat on dynamic 10x10 grid battlefields.

**🎯 Core Focus:** Online PvP with server-side validation, anti-cheat protection, and seamless cross-platform play.

## 2. Architecture Overview

### **Multiplayer-First Design**
- **Authoritative Server:** All game logic validated server-side using PlayFab CloudScript
- **Real-time Synchronization:** Game state continuously synchronized between clients
- **Anti-cheat Protection:** Comprehensive server-side validation prevents cheating
- **Cross-platform:** Seamless play between PC and mobile devices
- **Offline Mode:** Available for testing only - production prioritizes online multiplayer

### **Turn System (V3 Multiplayer)**
- **Simultaneous Submission:** Both players submit actions simultaneously
- **Server Resolution:** Actions processed and validated server-side
- **Real-time Updates:** Players see immediate feedback when both players submit
- **Turn Timer:** Automatic progression when both players submit or timer expires

## 3. Technology Stack

### **Core Engine**
* **Engine:** Unity 6 (Version 6000.0.36f1)
* **Language:** C# (.NET Standard 2.1)
* **Pathfinding:** A* Pathfinding Project (integrated)

### **Multiplayer Infrastructure** 🌐
* **Backend:** PlayFab (Microsoft Azure)
* **CloudScript:** Azure Functions for server-side game logic
* **Authentication:** PlayFab authentication system
* **Matchmaking:** PlayFab matchmaking services
* **Real-time Communication:** PlayFab multiplayer services

### **Game Systems**
* **Input:** Unity Input System Package
* **UI:** Unity UI with TextMeshPro (multiplayer-aware components)
* **Logging:** SmartLogger system with network-aware categories
* **State Management:** Authoritative server state with client synchronization

## 4. Multiplayer Features

### **🔒 Server-Side Validation**
- Movement range and pathfinding validation
- Ability targeting and resource cost validation
- Anti-cheat detection with automated penalties
- Turn timing and action limit enforcement
- Statistical anomaly detection

### **🚀 Real-time Features**
- Instant feedback when both players submit actions
- Live game state synchronization
- Player ready status indicators
- Turn timer with automatic progression
- Connection state monitoring

### **🛡️ Anti-Cheat System**
- Impossible action detection
- Rapid submission protection
- Duplicate action prevention
- Statistical pattern analysis
- Automated penalty system with progressive bans

## 5. Core Gameplay Features

### **Combat System**
* **Units:** 6v6 Dokkaebi units with Origin/Calling combinations
* **Grid Combat:** 10x10 battlefield with tactical positioning
* **Resources:** Aura system for abilities, MP for Overload states
* **Zones:** Dynamic battlefield modification through zone creation

### **Turn Flow (V3 System)**
1. **Action Submission Phase:** Players simultaneously choose actions
2. **Validation Phase:** Server validates all submitted actions
3. **Resolution Phase:** Server processes actions and updates game state
4. **Synchronization Phase:** Updated state sent to all clients
5. **Next Turn:** Cycle repeats until victory conditions met

### **Abilities & Combat**
* **Validated Targeting:** Server ensures all targets are legal
* **Resource Management:** Aura costs validated server-side
* **Overload States:** Powerful abilities when MP ≥ 7
* **Zone Interactions:** Server-controlled zone effects and duration

## 6. Project Structure

```
Assets/Scripts/
├── Core/
│   ├── GameController.cs              # Game initialization
│   ├── UnitManager.cs                 # Unit spawning and management
│   └── Networking/                    # 🌐 Multiplayer core
│       ├── NetworkingManager.cs       # PlayFab integration
│       ├── GameStateManagerMultiplayer.cs  # State synchronization
│       └── CloudScripts/              # Server-side logic
│           ├── SubmitPlayerAction.js  # Action processing
│           ├── GetGameState.js        # State retrieval
│           └── ValidationModule.js    # Anti-cheat system
├── TurnSystem/
│   ├── V3TurnManager.cs               # Local turn system
│   ├── V3TurnManagerMultiplayer.cs    # 🌐 Networked turn system
│   └── PlayerAction.cs                # Network-serializable actions
├── Units/
│   └── DokkaebiUnit.cs                # Unit logic with network sync
├── Grid/
│   └── GridManager.cs                 # Grid with network awareness
└── UI/
    └── MultiplayerUI/                 # Player status and turn info
```

## 7. Getting Started

### **Prerequisites**
- Unity 6 (Version 6000.0.36f1)
- PlayFab account and Title ID
- Visual Studio Code or Visual Studio 2022

### **Setup Instructions**

1. **Clone the Repository**
```bash
git clone [repository-url]
cd DokkaebiTBT/Dokkaebi
```

2. **PlayFab Configuration**
```bash
# Set up your PlayFab credentials in:
# Assets/Scripts/Core/Networking/PlayFabConfig.cs
```

3. **Open in Unity**
- Open Unity Hub
- Add project from `DokkaebiTBT/Dokkaebi`
- Ensure Unity 6 is selected

4. **Scene Setup**
- Open `BattleScene.unity`
- Ensure all managers are properly assigned
- Test multiplayer connectivity

### **Testing Multiplayer**
```csharp
// Test with multiple Unity instances
// Build and run two clients connecting to PlayFab
// Or use Unity's multi-client testing tools
```

## 8. Development Priorities

### **✅ Completed (Multiplayer Pivot)**
- [x] V3TurnManagerMultiplayer with PlayFab integration
- [x] Server-side action validation and anti-cheat system
- [x] Real-time game state synchronization
- [x] Player ready status and turn progression UI
- [x] Network-aware PlayerAction serialization

### **🔄 In Progress**
- [ ] Zone system server-side validation
- [ ] Status effect synchronization
- [ ] Matchmaking integration
- [ ] Mobile platform optimization

### **📋 Upcoming**
- [ ] Ranked matchmaking system
- [ ] Player progression and statistics
- [ ] Spectator mode
- [ ] Tournament system
- [ ] Advanced anti-cheat analytics

## 9. Multiplayer Architecture Details

### **Client-Server Communication**
```
Client 1 ←→ PlayFab CloudScript ←→ Client 2
          ↑                      ↑
    Action Validation      State Sync
    Anti-cheat Check      Real-time Updates
```

### **Game State Flow**
1. Clients submit actions to PlayFab
2. Server validates actions using ValidationModule
3. Server resolves turn and updates authoritative state
4. Updated state synchronized to all clients
5. Clients apply visual updates based on server state

### **Security Features**
- **Server Authority:** All game logic runs server-side
- **Input Validation:** Every action validated before processing
- **Cheat Detection:** Multiple layers of anti-cheat protection
- **Automated Moderation:** Progressive penalty system for violations

## 10. Contributing

### **Code Standards**
- Follow established C# naming conventions
- Use SmartLogger for all debug output
- Implement network-aware components for multiplayer features
- Validate all player inputs server-side

### **Testing Multiplayer Features**
```csharp
// Always test with real network latency
// Use PlayFab's testing environment
// Verify anti-cheat detection works correctly
```

### **Pull Request Guidelines**
- Test multiplayer functionality thoroughly
- Ensure server-side validation for new features
- Update documentation for API changes
- Include both local and networked test cases

## 11. Performance & Optimization

### **Network Optimization**
- Minimize data sent between client and server
- Use efficient serialization for PlayerActions
- Implement state compression for large game states
- Cache frequently accessed data locally

### **Mobile Considerations**
- Optimize for limited network bandwidth
- Handle connection interruptions gracefully
- Reduce battery usage with efficient sync intervals
- Maintain 60fps on mid-range mobile devices

---

**🎮 Ready to battle online! Dokkaebi's multiplayer-first architecture ensures fair, competitive gameplay with robust anti-cheat protection and seamless cross-platform experiences.**