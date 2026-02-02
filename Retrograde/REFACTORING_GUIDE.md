# Retrograde Code Refactoring Guide

## Overview
This guide documents best practices and patterns learned from refactoring complex procedural generation code, specifically from improving `TrunkTopologyPass.cs`. Use this as a reference when cleaning up and improving readability of similar procedural generation passes.

---

## Table of Contents
1. [Method Extraction for Nested Loops](#method-extraction-for-nested-loops)
2. [Configuration Organization](#configuration-organization)
3. [Parameter Reduction with Context Objects](#parameter-reduction-with-context-objects)
4. [Documentation Strategy](#documentation-strategy)
5. [Utility Extraction for Shared Logic](#utility-extraction-for-shared-logic)
6. [Quick Reference Checklist](#quick-reference-checklist)

---

## Method Extraction for Nested Loops

### Problem
Long methods with deeply nested loops (4-5 levels) become difficult to understand and maintain.

### Solution
Extract inner loops into well-named helper methods, working from innermost to outermost.

### Example: Before
```csharp
// 200+ line method with 4-5 levels of nesting
while (roomsPlaced < maxRooms && openConnectors.Count > 0)
{
    var target = SelectConnector(...);

    for (int prefabTry = 0; prefabTry < maxCandidates; prefabTry++)
    {
        var prefab = GetPrefab(...);

        for (int rotation = 0; rotation < 4; rotation++)
        {
            // 30+ lines of collision checking, scoring, etc.
            var connectors = GetConnectors(prefab, rotation);

            foreach (var connector in connectors)
            {
                // Complex compatibility checks
                // Position calculation
                // Collision detection
                // Bridge score calculation
            }
        }
    }
}
```

### Example: After
```csharp
// Clean 30-line method with clear structure
while (roomsPlaced < maxRooms && openConnectors.Count > 0)
{
    var target = SelectConnector(...);

    // Single method call replaces 100+ lines of nested logic
    var placementResult = TryPlaceRoomOnConnector(target, context);

    if (placementResult == null)
    {
        openConnectors.Add(target);
        continue;
    }

    ApplyPlacement(placementResult);
}

// Extracted method handles prefab selection
private static PlacementResult? TryPlaceRoomOnConnector(...)
{
    for (int prefabTry = 0; prefabTry < maxCandidates; prefabTry++)
    {
        var candidate = EvaluatePrefabRotations(...);
        // Compare candidates, return best
    }
}

// Extracted method handles rotation evaluation
private static CandidatePlacement? EvaluatePrefabRotations(...)
{
    for (int rotation = 0; rotation < 4; rotation++)
    {
        // Focused logic for one concern
    }
}
```

### Benefits
- **Readability**: Each method has a single, clear purpose
- **Testability**: Smaller methods are easier to test in isolation
- **Reusability**: Extracted methods can be reused elsewhere
- **Cognitive Load**: Reduced from tracking 100+ lines to ~10 lines per method

### Guidelines
1. **Extract from innermost loop first**: Start with the deepest nesting
2. **Name methods by intent**: `EvaluatePrefabRotations` is clearer than `ProcessRotations`
3. **Keep extractions focused**: Each method should do one thing well
4. **Add XML documentation**: Explain what, why, and return values

---

## Configuration Organization

### Problem
Magic numbers and configuration values scattered throughout methods make tuning difficult.

### Solution
Move all configuration constants to the top of the class, organized by category.

### Example: Before
```csharp
public void RunPass(DungeonState state)
{
    int maxAttempts = 1000;              // buried in method
    float collisionPadding = -0.5f;
    int maxCandidates = 8;
    float bridgeMaxSpan = 40f;
    // ... hundreds of lines later
    if (attempts > maxAttempts) break;
}
```

### Example: After
```csharp
public class TrunkTopologyPass : IGenPass
{
    // Room placement limits
    private readonly int maxRoomsToPlace = 10;
    private const int MaxAttempts = 1000;

    // Collision and spacing parameters
    private const float CollisionPadding = -0.5f;

    // Prefab selection parameters
    private const int MaxCandidatePrefabsPerConnector = 8;

    // Bridge placement constraints
    private const float BridgeMaxHorizontalSpan = 40f;
    private const float BridgeMaxVerticalOffset = 8f;

    public void RunPass(DungeonState state)
    {
        // Clean initialization, references constants above
    }
}
```

### Benefits
- **Single source of truth**: All tuning parameters in one place
- **Easy to adjust**: No hunting through code to find values
- **Better documentation**: Each constant has a clear purpose comment
- **Type safety**: Use `const` where possible for compile-time checking

### Guidelines
1. **Group by category**: Organize related constants together
2. **Use descriptive names**: `MaxCandidatePrefabsPerConnector` over `maxTries`
3. **Add comments**: Explain units and purpose
4. **Use const when possible**: Prefer `const` over `readonly` for true constants
5. **Make instance-specific values readonly**: Values set in constructor should be `readonly`

---

## Parameter Reduction with Context Objects

### Problem
Methods with 10+ parameters are hard to read, maintain, and call correctly.

### Solution
Create context objects that bundle related state, drastically reducing parameter counts.

### Example: Before
```csharp
// 14 parameters - overwhelming!
private static PlacementResult? TryPlaceRoomOnConnector(
    OpenConnector target,
    P3Float clusterCenter,
    List<string> requiredPrefabs,
    HashSet<string> usedPrefabIds,
    RoomUtils roomUtils,
    List<PlacedRoom> plannedRooms,
    List<OpenConnector> plannedOpenConnectors,
    float yMin,
    float collisionPadding,
    float bridgeMaxHorizontalSpan,
    float bridgeMaxVerticalOffset,
    HashSet<string> bridgePrefabKeys,
    string districtType,
    int maxCandidates)
{
    // method implementation
}
```

### Example: After
```csharp
// Context object bundles related state
private class PlacementContext
{
    public List<PlacedRoom> PlannedRooms { get; set; }
    public List<OpenConnector> PlannedOpenConnectors { get; set; }
    public float YMin { get; set; }
    public HashSet<string> BridgePrefabKeys { get; set; }
    public string DistrictType { get; set; }
    public P3Float ClusterCenter { get; set; }
    public List<string> RequiredPrefabs { get; set; }
    public HashSet<string> UsedPrefabIds { get; set; }
    public RoomUtils RoomUtils { get; set; }
}

// 2 parameters - clean and readable!
private static PlacementResult? TryPlaceRoomOnConnector(
    OpenConnector target,
    PlacementContext context)
{
    // method implementation - access context.PlannedRooms, etc.
}
```

### Context Object Design

#### What to Include
- **Related state**: Parameters that logically belong together
- **Frequently passed together**: Data that travels as a group
- **Mutable planning state**: Lists and collections that get updated

#### What to Keep Separate
- **Method-specific inputs**: Like `target` in the example above
- **Output/return values**: Don't bundle outputs with inputs
- **Computed values**: Calculate these in the method, don't pre-compute

### Benefits
- **Cleaner signatures**: 14 parameters → 2 parameters
- **Easier to maintain**: Adding state doesn't require updating all call sites
- **Better encapsulation**: Related data travels together
- **Improved readability**: Call sites are much simpler
- **Reduced cognitive load**: Focus on key inputs, not parameter ordering

### Guidelines
1. **Name contexts clearly**: `PlacementContext` describes its purpose
2. **Group logically**: Bundle parameters that represent a cohesive concept
3. **Keep contexts focused**: Don't create "god objects" with everything
4. **Use properties**: Make fields properties for clarity
5. **Initialize with defaults**: Provide sensible defaults where possible

### Real-World Results
```csharp
// Before: Call site with 14 arguments
var result = TryPlaceRoomOnConnector(
    target, clusterCenter, requiredPrefabs, usedPrefabIds,
    roomUtils, plannedRooms, plannedOpenConnectors, yMin,
    collisionPadding, bridgeMaxHorizontalSpan,
    bridgeMaxVerticalOffset, bridgePrefabKeys,
    districtType, maxCandidates);

// After: Clean, readable call site
var result = TryPlaceRoomOnConnector(target, context);
```

---

## Documentation Strategy

### Problem
Complex algorithms are hard to understand without context about stages and strategy.

### Solution
Add staged comments that explain the "what" and "why" at decision points.

### Documentation Layers

#### 1. Class-Level Documentation
```csharp
/// <summary>
/// Main algorithm: Generates trunk room layouts by iteratively placing rooms on open connectors.
/// Uses a multi-plan approach to find the best layout based on scoring criteria.
/// </summary>
public void RunPass(DungeonState state)
```

#### 2. Stage Comments
```csharp
// Stage 1: Multi-plan generation - run multiple planning attempts to find optimal layout
var bestOutcome = PlanRunner.RunBest<TrunkPlanMeta>(maxPlans, planAttempt =>
{
    // Stage 2: Iterative room placement loop
    while (roomsPlaced < maxRooms && openConnectors.Count > 0)
    {
        // Stage 2a: Select next connector to expand from
        // Strategy: choose farthest connector from cluster center to encourage sprawl

        // Stage 2b: Find best room for this connector

        // Stage 2c: Accept the placement and update state
    }

    // Stage 3: Evaluate this plan using multiple scoring metrics

    // Stage 4: Apply the best plan to the dungeon state
}
```

#### 3. Decision Point Comments
```csharp
// Choose connector that points most outward (away from cluster center)
var chosen = ChooseMostOutwardConnector(compatible, targetPos, clusterCenter);

// Validate placement: check for collisions and height constraints
if (IsBelowYMin(candidateAabb, yMin))
    continue;

// Calculate how many new connector pairs could be bridged with this placement
int bridgeScore = CountBridgeablePairs(...);
```

#### 4. Method Documentation
```csharp
/// <summary>
/// Evaluates all rotations (0°, 90°, 180°, 270°) of a prefab to find the best placement.
/// For each rotation: finds compatible connectors, checks collisions, calculates bridge score.
/// Returns the rotation with the highest bridge score, or null if no valid placement exists.
/// </summary>
```

### Guidelines
1. **Explain strategy, not syntax**: Say WHY, not just WHAT
2. **Document non-obvious decisions**: If it took thinking, document it
3. **Use stages for complex flows**: Break algorithms into numbered stages
4. **Keep comments concise**: One line is often enough
5. **Update comments when code changes**: Stale comments are worse than none

---

## Utility Extraction for Shared Logic

### Problem
After refactoring multiple passes, you may notice similar helper methods appearing in different classes. Duplicated logic across passes makes maintenance harder and increases the risk of bugs.

### Solution
Extract commonly used helper methods into utility classes, creating a shared library of reusable components.

### Existing Utility Files in Retrograde

The codebase already has several utility classes organized by responsibility:

- **`ConnectorUtils.cs`** - Connector manipulation, rotation, compatibility checking
- **`ConnectorSelectionUtil.cs`** - Strategies for selecting connectors (e.g., farthest, nearest, outward)
- **`MathUtil.cs`** - Mathematical operations (distance, geometry, etc.)
- **`ScoringUtil.cs`** - Plan scoring and evaluation metrics
- **`BridgeUtil.cs`** - Bridge prefab management and bridging logic
- **`RoomUtils.cs`** - Room prefab selection and management
- **`PlacementUtil.cs`** - Object placement operations
- **`RetrogradeUtils.cs`** - General-purpose utilities

### When to Extract to Utils

#### Extract When:
1. **Logic appears in 2+ passes**: If you write the same helper method twice, extract it
2. **Method is pure/stateless**: Methods that don't depend on instance state are ideal candidates
3. **Clearly defined responsibility**: Method fits naturally into an existing util category
4. **Reusable across contexts**: Logic isn't tightly coupled to a specific pass

#### Keep in Pass When:
1. **Used only once**: Single-use helpers can stay private in the pass
2. **Tightly coupled to pass logic**: Methods that deeply depend on pass-specific state
3. **Still evolving**: Wait until the pattern stabilizes before extracting
4. **Pass-specific strategy**: When the logic is inherently tied to one algorithm

### Example: ConnectorSelectionUtil

After refactoring `BossTopologyPass`, we created `SelectFarthestConnectorFromStart()`. If `TrunkTopologyPass` or other passes need similar connector selection strategies, this becomes a candidate for extraction:

```csharp
// Before: Duplicated in multiple passes
private static int SelectFarthestConnectorFromStart(List<OpenConnector> connectors, P3Float startingPosition)
{
    int bestIndex = -1;
    float bestDistSq = float.MinValue;
    for (int i = 0; i < connectors.Count; i++)
    {
        float distSq = MathUtil.DistanceSquared(connectors[i].WorldPos, startingPosition);
        if (distSq > bestDistSq)
        {
            bestDistSq = distSq;
            bestIndex = i;
        }
    }
    return bestIndex;
}

// After: Extracted to ConnectorSelectionUtil
public static class ConnectorSelectionUtil
{
    /// <summary>
    /// Selects the connector farthest from a reference position.
    /// Common use case: Place rooms at dungeon extremities.
    /// </summary>
    public static int SelectFarthest(List<OpenConnector> connectors, P3Float referencePosition)
    {
        int bestIndex = -1;
        float bestDistSq = float.MinValue;
        for (int i = 0; i < connectors.Count; i++)
        {
            float distSq = MathUtil.DistanceSquared(connectors[i].WorldPos, referencePosition);
            if (distSq > bestDistSq)
            {
                bestDistSq = distSq;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    /// <summary>
    /// Selects the connector nearest to a reference position.
    /// Common use case: Place connecting rooms close to existing structures.
    /// </summary>
    public static int SelectNearest(List<OpenConnector> connectors, P3Float referencePosition)
    {
        // Similar implementation with MinValue instead of MaxValue
    }

    /// <summary>
    /// Selects the connector pointing most outward from a center point.
    /// Common use case: Encourage sprawling dungeon layouts.
    /// </summary>
    public static OpenConnector SelectMostOutward(
        List<OpenConnector> connectors,
        P3Float connectorPosition,
        P3Float centerPosition)
    {
        // Implementation that considers connector direction
    }
}
```

### Naming Conventions for Utils

1. **File names end in `Util` or `Utils`**: `ConnectorUtils`, `MathUtil`
2. **Static classes**: Most utils should be static classes with static methods
3. **Verb-based method names**: `SelectFarthest`, `CalculateDistance`, `FilterCompatible`
4. **XML documentation required**: Explain purpose, common use cases, parameters
5. **Group related methods**: Keep all connector selection in one util class

### Refactoring Workflow with Utils

When refactoring a pass:

1. **First pass**: Extract methods within the pass class (as we did with `BossTopologyPass`)
2. **Second pass**: Identify reusable methods that appear in multiple passes
3. **Third pass**: Extract to appropriate util class and update all call sites
4. **Fourth pass**: Add additional overloads or variations as needed

### Benefits

- **DRY principle**: Single source of truth for common operations
- **Easier testing**: Utility methods can be unit tested in isolation
- **Discoverability**: Developers know where to find common operations
- **Consistency**: Same logic used everywhere produces consistent behavior
- **Maintenance**: Bug fixes in utils automatically fix all passes

### Guidelines

1. **Don't extract prematurely**: Wait until you see the pattern twice
2. **Make utils cohesive**: Each util class should have a clear, focused purpose
3. **Keep utils stateless**: Prefer pure functions over stateful utilities
4. **Document use cases**: Help other developers understand when to use each util
5. **Consider performance**: Heavily-called utils should be optimized

---

## Removing Unused Variables and Dead Code

### Problem
Over time, code evolves and variables/constants that were once used become obsolete. Unused code creates noise, confuses future developers, and makes the codebase harder to maintain.

### Solution
Regularly audit for and remove unused variables, constants, and code. Modern IDEs and compilers provide warnings to help identify these.

### Types of Unused Code

#### 1. Unused Constants
Constants that were planned for future use or left over from copy-paste:

```csharp
// Before: Constants marked as unused by IDE
private const float BridgeMaxHorizontalSpan = 40f;  // IDE: unused
private const float BridgeMaxVerticalOffset = 8f;   // IDE: unused

// After: Removed entirely
// (If truly needed later, it's in version control)
```

#### 2. Unused Variables
Variables created but never read:

```csharp
// Before: Variable created but never used
var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);
// ... rest of method never references bridgePrefabKeys

// After: Remove if not needed
// (Or use it if it was supposed to be used)
```

#### 3. Unused Collections
Data structures that are populated but never queried:

```csharp
// Before: Collection filled but never read
var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var room in state.placedRooms)
{
    usedPrefabIds.Add(room.Prefab.PrefabEditorId);
}
// ... usedPrefabIds is never checked anywhere

// After: Remove entirely
// (This was likely intended for a feature that wasn't implemented)
```

### When to Remove vs. Keep

#### Remove When:
1. **IDE/Compiler marks as unused**: Trust the tooling
2. **No clear purpose documented**: If you can't explain why it's there, remove it
3. **Copy-paste artifact**: Code copied from another file but not relevant here
4. **Incomplete feature**: Partially implemented feature that was abandoned
5. **Defensive programming gone wrong**: "We might need this" without actual need

#### Keep When (Rare):
1. **Public API**: Part of interface that external code might use
2. **Reflection/Serialization**: Accessed via reflection or serialization frameworks
3. **Configuration template**: Showing what's available even if not used here
4. **Future sprint work**: ONLY if:
   - There's an actual ticket/issue
   - Document it clearly: `// TODO: Issue #123 - Will use for feature X`
   - Review quarterly and remove if stale

### Real Example from BossTopologyPass

#### Issue Found:
```csharp
// Bridge placement constraints (unused for boss, but kept for consistency)
private const float BridgeMaxHorizontalSpan = 40f;
private const float BridgeMaxVerticalOffset = 8f;
```

**Problem:** Comment says "kept for consistency" but:
- Boss pass doesn't use bridges
- No interface requiring these constants
- Other passes that DO use bridges define their own values
- Creates false impression that boss pass handles bridges

**Decision:** Remove them. Consistency is not a valid reason to keep unused code.

#### Code Smell Patterns:

```csharp
// ❌ Bad: "Consistency" excuse
private const int MaxValue = 100; // Unused here, but TrunkPass has it

// ❌ Bad: "Might need later" excuse
private List<string> potentiallyUsefulData = new(); // Just in case

// ❌ Bad: Copy-paste without cleanup
// Copied from TrunkTopologyPass
private HashSet<string> usedPrefabIds; // Not relevant for boss rooms

// ✅ Good: Actually document if keeping for valid reason
// TODO: Issue #456 - Will use for prefab variation tracking (Sprint 23)
private HashSet<string> usedPrefabIds;
```

### Detection Strategies

#### 1. Use IDE Warnings
- Visual Studio: Look for gray/dimmed code
- Rider: Yellow underlines
- VS Code: Grayed out identifiers

#### 2. Compiler Warnings
```bash
# Build with warnings enabled
dotnet build /warnaserror:CS0169,CS0414,CS0649
```

Common warning codes:
- `CS0169`: Field is never used
- `CS0414`: Field is assigned but never used
- `CS0219`: Variable is assigned but never used
- `CS8618`: Non-nullable field must contain non-null value

#### 3. Manual Code Review
Questions to ask:
- Is this read anywhere in the file?
- Does anything depend on this value?
- If I remove it, what breaks?
- Why was this added? (Check git history)

### Cleanup Process

1. **Identify**: Run build, note warnings
2. **Verify**: Search for all references (IDE "Find All References")
3. **Test**: Comment out and build/test
4. **Remove**: Delete confidently
5. **Commit**: Clear commit message: "Remove unused variables"

### Benefits

- **Reduced cognitive load**: Less code to understand
- **Clearer intent**: Only relevant code remains
- **Easier refactoring**: Less code to update when changing logic
- **Better git history**: Changes are more focused
- **Caught bugs**: Sometimes unused variables reveal logic errors

### Example: BossTopologyPass Cleanup

**Before (with unused code):**
```csharp
private const float CollisionPadding = -1.5f;
private const float BridgeMaxHorizontalSpan = 40f;  // Unused
private const float BridgeMaxVerticalOffset = 8f;   // Unused

public void RunPass(DungeonState state)
{
    var bridgePrefabKeys = state.BridgePrefabKeys ??= ...;  // Unused

    var usedPrefabIds = new HashSet<string>();  // Unused
    foreach (var room in state.placedRooms)
        usedPrefabIds.Add(room.Prefab.PrefabEditorId);
}
```

**After (cleaned up):**
```csharp
private const float CollisionPadding = -1.5f;

public void RunPass(DungeonState state)
{
    // Direct, focused code
}
```

**Result:** 5 lines removed, code intent is clearer

### Integration with Refactoring Checklist

Add unused code removal as part of your refactoring workflow:

1. **After method extraction**: Check if old variables are still needed
2. **After utility extraction**: Remove duplicated constants
3. **After parameter reduction**: Check if local variables became redundant
4. **Final cleanup**: One pass to remove all unused code

### Common Pitfalls

1. **Over-zealous removal**: Don't remove code that's used via reflection
2. **Removing too much**: Keep public APIs even if unused internally
3. **Not checking tests**: Unused in main code, but test helpers might use it
4. **Future-proofing**: Resist keeping code "just in case"

---

## Quick Reference Checklist

Use this checklist when refactoring procedural generation passes:

### Before Starting
- [ ] Read through entire file to understand the algorithm
- [ ] Identify the main stages/phases of the algorithm
- [ ] Note deeply nested loops (3+ levels)
- [ ] Find scattered magic numbers and configuration values

### Refactoring Steps

#### 1. Extract Configuration (Easy Win)
- [ ] Move magic numbers to class-level constants
- [ ] Group constants by category with comments
- [ ] Use `const` for true constants, `readonly` for instance values
- [ ] Add descriptive names and comments

#### 2. Method Extraction (Core Work)
- [ ] Identify innermost nested loop
- [ ] Extract to method with descriptive name
- [ ] Add XML documentation summary
- [ ] Repeat for outer loops, working outward
- [ ] Verify each extraction compiles and tests pass

#### 3. Parameter Reduction (Polish)
- [ ] Identify methods with 5+ parameters
- [ ] Group related parameters into context object(s)
- [ ] Keep method-specific inputs as separate parameters
- [ ] Update call sites to use context
- [ ] Verify build succeeds

#### 4. Documentation (Finish)
- [ ] Add class/method XML summaries
- [ ] Add stage comments to main algorithm
- [ ] Add strategy comments at key decision points
- [ ] Add inline comments for complex logic
- [ ] Review that comments explain WHY, not WHAT

#### 5. Utility Extraction (Optional - Do Later)
- [ ] Identify helper methods that might be reused in other passes
- [ ] Check if similar logic exists in other passes
- [ ] If found in 2+ places, extract to appropriate util class
- [ ] Update all call sites to use the util method
- [ ] Add XML documentation explaining use cases

### Verification
- [ ] Build succeeds with 0 errors
- [ ] All tests pass
- [ ] Main method is ~30-50 lines (not 200+)
- [ ] No method has 10+ parameters
- [ ] Configuration is organized at class top
- [ ] Algorithm flow is clear from comments

---

## Key Takeaways

### The Four Pillars of Clean Procedural Code
1. **Method Extraction**: Break complex nested logic into focused helper methods
2. **Configuration Organization**: Centralize tuning parameters at class level
3. **Parameter Reduction**: Use context objects to bundle related state
4. **Utility Extraction**: Move reusable methods to shared util classes (after initial refactoring)

### When to Apply These Patterns
- Methods longer than 50 lines with nested loops
- Magic numbers scattered through methods
- Methods with 5+ parameters
- Complex algorithms hard to understand at a glance
- Similar helper methods appearing in multiple passes (extract to utils)

### Incremental Improvement
You don't have to do everything at once:
1. Start with easy wins (move constants to top)
2. Extract one nested loop to see the pattern
3. Add documentation as you go
4. Reduce parameters when signatures become unwieldy
5. Extract to utils after refactoring 2+ passes (identify common patterns)

### Real Results from TrunkTopologyPass
- **Main method**: 250 lines → 70 lines (72% reduction)
- **Max nesting**: 5 levels → 2 levels
- **TryPlaceRoomOnConnector**: 14 params → 2 params (86% reduction)
- **EvaluatePrefabRotations**: 12 params → 4 params (67% reduction)
- **Build time**: No impact (same performance)
- **Readability**: Significantly improved

### Real Results from BossTopologyPass
- **Main method**: 210 lines → 155 lines (26% reduction)
- **Max nesting**: 4 levels → 2 levels
- **Extracted methods**: 2 focused helper methods (SelectFarthestConnectorFromStart, TryPlaceBossRoomAtConnector)
- **Configuration**: 5 constants organized at class level
- **Documentation**: Full XML docs and stage comments added
- **Build**: 0 errors, existing warnings unchanged
- **Readability**: Significantly improved

---

## Additional Resources

### Related Files
- `TrunkTopologyPass.cs` - Fully refactored example (complex multi-room placement)
- `BossTopologyPass.cs` - Fully refactored example (single room placement)
- `DistrictTopologyPass.cs` - Similar pattern, candidate for refactoring

### Patterns to Explore Next
- **Builder Pattern**: For complex object construction
- **Strategy Pattern**: For swappable algorithms
- **Template Method**: For passes with similar structure

---

## Refactoring History

### BossTopologyPass (2026-02-02)

#### First Pass - Initial Refactoring
Applied all four refactoring patterns to clean up boss room placement logic:

**Key Improvements:**
1. **Configuration Organization**: Extracted 5 magic numbers to class-level constants with clear names and comments
2. **Method Extraction**:
   - `SelectFarthestConnectorFromStart()` - Encapsulates connector selection strategy
   - `TryPlaceBossRoomAtConnector()` - Encapsulates rotation evaluation and placement logic
3. **Result Objects**: Created `BossPlacementResult` class to cleanly return placement data
4. **Documentation**: Added XML docs and 4-stage comment structure explaining the algorithm flow

**Impact:**
- Main loop reduced from ~100 lines to ~20 lines
- Nesting reduced from 4 levels to 2 levels
- Algorithm flow is now clear at a glance
- Each method has a single, well-documented responsibility

#### Second Pass - Utility Extraction & Cleanup
Applied refactoring guide checklist again to find additional improvements:

**Additional Improvements:**
1. **Utility Extraction**: Replaced custom `SelectFarthestConnectorFromStart()` with existing `ConnectorSelectionUtil.ChooseFarthestOpenConnector()`
   - Discovered TrunkTopologyPass was already using the utility version
   - Eliminated 20 lines of duplicate code
   - Now using shared, tested implementation
2. **Code Cleanup**: Removed unused `usedPrefabIds` collection (8 lines)
3. **Field Modifiers**: Made `district` field `readonly` for immutability

**Impact:**
- Removed 28 lines of duplicate/unused code
- Reduced from 320 lines → 292 lines (9% reduction)
- Reduced warnings from 376 → 375
- Better adherence to DRY principle
- Consistent with TrunkTopologyPass implementation

**Lessons Learned:**
- Always check existing utils before writing new helper methods
- Running the refactoring checklist multiple times reveals incremental improvements
- Utility extraction often happens after seeing patterns across multiple files

### TrunkTopologyPass (2026-02-02)
Initial refactoring that established the patterns documented in this guide.

---

*Last Updated: 2026-02-02*
*Based on TrunkTopologyPass and BossTopologyPass refactorings*
