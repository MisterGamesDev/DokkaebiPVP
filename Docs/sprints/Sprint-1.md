# Sprint-1 Plan  (2024-12-XX → 2024-12-XX) ✅ **COMPLETED**

## Goal
Deliver a fully playable local 1-vs-1 match using the new V3 simultaneous Turn System.

## Acceptance Criteria ✅ **ALL ACHIEVED + EXCEEDED**
- [x] Actions resolve by `priorityType` and Speed stat ✅
- [x] Overload checks gate abilities correctly ✅
- [x] Game ends when one team has 0 living units ✅
- [x] Unit & ability cooldowns function ✅
- [x] "Select tile" UI works with V3 flow ✅
- [x] All new/changed logic covered by ≥ 10 unit tests ✅ **EXCEEDED: 46 tests created**

## **BONUS ACHIEVEMENTS (Not Originally Planned)**
- [x] Complete PlayFab integration with server-authoritative architecture
- [x] Comprehensive test suite (EditMode + PlayMode testing)
- [x] MockServerValidator for local development
- [x] Full UI integration (TurnPhaseUI, PlayerResourceUI, AbilitySelectionUI)
- [x] Status effects and zone management systems
- [x] Advanced pathfinding and movement validation

## Tasks
| # | Task | Context | Owner | Est | Notes |
|---|------|---------|-------|-----|-------|
| 1 | Add `speed` & `IsInOverload()` to `DokkaebiUnit` | Combat Core |  | 4 h | ✅ DONE |
| 2 | Integrate Speed tie-break in `ActionResolutionSystem` | Combat Core |  | 3 h | ✅ DONE |
| 3 | Implement win/lose check in `GameController` | Combat Core |  | 3 h | ✅ DONE |
| 4 | Hook V3ActionSelectionUI → grid targeting | UI / Combat |  | 6 h | ✅ DONE |
| 5 | Add ability/unit cooldown enforcement | Ability System |  | 4 h | ✅ DONE |
| 6 | Unit tests: GridPosition, ActionResolution, StatusEffectSystem | Shared Kernel |  | 6 h | ✅ EXCEEDED: 46 comprehensive tests |
| 7 | Exploratory play-test & bug-fix pass | All |  | 6 h | ✅ DONE + Automated testing |

_Mark owners + exact dates during sprint kickoff._

## Cautionary Advice
• Be wary of new null-reference paths when Speed/Overload is still `default`; guard clauses first.  
• UI grid targeting may require event re-routing—avoid hard-coding scene references.

## Test Plan
1. Editor unit tests (NUnit) for logic items above.  
2. Play-mode test: scripted sequence where both players issue conflicting Move/Attack actions.  
3. Manual sanity: run a full match, verify turn counter increments & end-game UI appears.

## References
- architecture.md § Bounded-Context Map  
- TurnSystem/V3_IMPLEMENTATION_NOTES.md  
- milestones.md 