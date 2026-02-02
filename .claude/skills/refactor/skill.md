---
name: refactor
description: Refactor a Retrograde procedural generation pass using established patterns and checklist
---

# Retrograde Code Refactoring Skill

When the user asks you to refactor a file (typically a procedural generation pass), follow this guide systematically. Apply patterns incrementally and verify the build after each step.

## Refactoring Checklist

### Before Starting
- Read through the entire file to understand the algorithm
- Identify the main stages/phases of the algorithm
- Note deeply nested loops (3+ levels)
- Find scattered magic numbers and configuration values
- Check for unused variables, constants, and dead code

### Step 1: Extract Configuration (Easy Win)
- Move magic numbers to class-level constants
- Group constants by category with comments
- Use `const` for true constants, `readonly` for instance values
- Use descriptive names: `MaxCandidatePrefabsPerConnector` over `maxTries`

### Step 2: Method Extraction (Core Work)
- Identify innermost nested loop and extract to a method with a descriptive name
- Name methods by intent: `EvaluatePrefabRotations` not `ProcessRotations`
- Add XML documentation summary explaining what, why, and return values
- Repeat for outer loops, working outward
- Each method should do one thing well
- Verify build compiles after each extraction

### Step 3: Parameter Reduction (Polish)
- Identify methods with 5+ parameters
- Create context objects that bundle related state:
  - Include: related state, frequently co-passed data, mutable planning state
  - Keep separate: method-specific inputs, output/return values, computed values
- Name contexts clearly: `PlacementContext` describes its purpose
- Update call sites to use context

### Step 4: Remove Unused Code
- Check for unused constants, variables, and collections
- Trust IDE/compiler warnings (CS0169, CS0414, CS0219)
- Remove code kept only for "consistency" with other files
- Remove "just in case" code without an actual ticket
- Don't remove: public API, reflection-accessed code, or code with a documented TODO and ticket number

### Step 5: Documentation (Finish)
- Add class/method XML summaries
- Add numbered stage comments to the main algorithm (Stage 1, Stage 2, etc.)
- Add strategy comments at key decision points explaining WHY, not WHAT
- Keep comments concise - one line is often enough

### Step 6: Utility Extraction (Optional)
- Check existing utils before writing new helpers:
  - `ConnectorUtils.cs` - Connector manipulation, rotation, compatibility
  - `ConnectorSelectionUtil.cs` - Connector selection strategies
  - `MathUtil.cs` - Math operations
  - `ScoringUtil.cs` - Plan scoring
  - `BridgeUtil.cs` - Bridge logic
  - `RoomUtils.cs` - Room prefab selection
  - `PlacementUtil.cs` - Placement operations
  - `RetrogradeUtils.cs` - General utilities
- Only extract to utils if the same logic appears in 2+ passes
- Use static classes with verb-based method names

### Verification
- Build succeeds with 0 errors
- Main method is ~30-50 lines (not 200+)
- No method has 10+ parameters
- Max nesting is 2 levels
- Configuration is organized at class top
- Algorithm flow is clear from comments

## Key Patterns

### Context Objects
Bundle 5+ related parameters into a context class:
```csharp
private class PlacementContext
{
    public List<PlacedRoom> PlannedRooms { get; set; }
    public P3Float ClusterCenter { get; set; }
    // ... related state
}

// 14 params -> 2 params
private static Result? TryPlace(OpenConnector target, PlacementContext context)
```

### Stage Comments
```csharp
// Stage 1: Multi-plan generation
// Stage 2: Iterative room placement loop
//   Stage 2a: Select next connector
//   Stage 2b: Find best room
//   Stage 2c: Apply placement
// Stage 3: Evaluate plan
// Stage 4: Apply best plan
```

### Result Objects
Return structured results instead of out parameters:
```csharp
private class BossPlacementResult
{
    public RoomPrefab Prefab { get; set; }
    public int Rotation { get; set; }
    public P3Float Position { get; set; }
}
```

## After Refactoring
- Update the Refactoring History section below with what was done and the impact

## Refactoring History

### BossTopologyPass.cs
- Extracted configuration constants, added XML docs and stage comments
- Created `BossPlacementResult` result object to replace out parameters
- Extracted `TryPlaceBossRoomAtConnector` method

### BridgingTopologyPass.cs
- **Constants**: Normalized all config to `const` with PascalCase naming, grouped by category
- **Context object**: Created `BridgePlacementContext` to bundle 11 parameters (planned rooms, connectors, placements, used prefab IDs, room utils, district info, yMin) into a single context passed through the placement pipeline
- **Result object**: Created `BridgePlacementResult` replacing 3 `out` parameters with a structured return type
- **Method extraction**: Broke `TryPlaceBridgeBetween` (4-level nesting) into `EvaluatePrefabRotation` and `BuildBridgePlacementResult`; extracted `IsValidBridgePair` and `AcceptBridgePlacement` from `PlanBridges`
- **Parameter reduction**: `PlanBridges` 11 params → 1 (context); `TryPlaceBridgeBetween` 13 params (10 in + 3 out) → 3 params returning a result object
- **Documentation**: Added XML summaries to class and all extracted methods; added stage comments (Stages 1-4, 2a-2c)
- **Impact**: Max nesting reduced from 4 to 2 levels; main loop body reads as high-level steps

### ConnectorSealingPass.cs
- **Constants**: Promoted `positionTolerance` and `startPosTolerance` to class-level `const` fields; removed tolerance parameters from all helper methods
- **Method extraction**: Broke `SealUnconnectedPlacedMarkers` (~90 lines, 3 stages) into `CollectPlacedConnectors`, `FindMatchedConnectors`, and `AreConnectorsMated`; extracted `TrySealConnector` to encapsulate the strict→relaxed blocker placement pattern
- **Dead code**: Removed empty `if (!placed)` block with placeholder comment
- **Documentation**: Added XML summaries to class and all extracted methods; added stage comments (Stages 1-2, 2a-2c)
- **Impact**: Max nesting reduced from 3 to 2 levels; `SealUnconnectedPlacedMarkers` reads as three high-level stages; helper signatures simplified by removing repeated tolerance parameters
