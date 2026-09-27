# Placement system

The old 3D placement implementation is replaced. `GridBuildingSystem3D`,
`PlacedObject_Done`, `BuildingGhost`, and `GridXZ` retain their names and script GUIDs
so existing scenes, prefabs, shops, navigation, and save records still resolve.
The separate 2D demonstration is not the active game placement system.

## Player interaction

- Select an object in the existing shop, drag its preview, then confirm.
- Green/red preview indicates footprint validity. Press **R** or the rotation button
  for a quarter turn. **Escape** cancels.
- Select existing furniture to move it. Its old footprint stays reserved until
  confirmation; cancellation keeps the original pose and occupancy.
- Drag a floor rectangle and confirm to paint. The remove-floor brush erases only
  floors. Furniture and floors have independent occupancy.
- Existing sell controls and Last Orders place/move/rotate/delete commands use the
  replacement service. The existing command stack retains its undo window.

## Authoring

`PlacedObjectTypeSO` remains the item definition: prefab, visual, rectangular
footprint, economy, and gameplay data. Width/height are grid cells. For example,
a 2-by-3 object occupies six cells, becoming 3-by-2 after rotation. Legacy floor
assets with zero dimensions are interpreted as 1-by-1.

Use **Create > Food Street > Placement** to create:

- **Grid Settings**: dimensions, cell size, world origin, valid/invalid colors.
  Assign to the scene's `GridBuildingSystem3D`. Explicit initialization from
  `SceneGridInitializer` still takes precedence over the default dimensions.
- **Placement Profile**: shared rotation policy, floor support requirement, and
  optional rule assets. Assign to an item's `placementProfile`. No profile keeps
  existing items compatible: quarter turns allowed, floor support optional.
- **Rules > Require Adjacent Object**: require a specified neighbor along an edge.
  Derive from `PlacementRule` for additional pure validation policies. Rules must
  not mutate occupancy, spend currency, or allocate temporary collections.

Prefab pivots follow the existing corner-pivot convention, with local +X/+Z
covering the footprint at zero rotation. `GridLayout.Pivot` handles rotation
offsets. `PivotToCell` reverses this for existing world-position save records.
Use a dedicated visual prefab without gameplay scripts. Materials must support
`_BaseColor` or `_Color` for preview tint. Render scale is authored on the prefab;
changing cell size changes snapping, not the model's physical scale.

No scene migration is required. Grid Settings and profiles are optional. Existing
grid visuals remain connected; scenes without one get a single generated line
mesh while placing. Its runtime fallback uses URP Unlit / Sprites Default; ensure
the applicable shader is included when configuring shader stripping for a build.

## Modules and extension points

| Module | Responsibility |
| --- | --- |
| `GridLayout`, `GridFootprint` | World/cell conversion, extents and rotation offsets |
| `GridXZ<T>` | Flat row-major storage and changed-cell notifications |
| `PlacementService` | Validation, placement, self-overlapping moves, registration |
| `PlacementProfile`, `PlacementRule` | Reusable authoring policies |
| `FloorBrush` | Bounded rectangle painting and pooled preview lifetime |
| `PlacementPools` | Runtime instance pools, separate from definition assets |
| `PlacementPointer` | Cached UI hit testing with reusable buffers |
| `BuildingGhost`, `GridOverlay` | Preview tint/pose and grid presentation |
| `GridBuildingSystem3D` | Existing shop, input and save integration |

Validation and occupancy updates take O(footprint area), without temporary cell
lists. `GetGridPositionList` remains an allocating compatibility API for existing
gameplay callers. UI raycasting occurs once per frame. Unchanged floor rectangles
do no preview work; changed rectangles reuse visual instances. Navigation receives
only changed-cell events. Pool registries reset between Play sessions.

Use `Placement.Validate` to explain a failure, `TryPlace` to create, and `TryMove`
to relocate/rotate. `Register` is a trusted scene-authoring import operation, not
a player-facing placement method. `DestroySelf` releases only the target's cells.
The service handles spatial legality; shop/build commands handle their own costs.
The floor brush is for unit tiles and skips invalid cells independently.

This implements Sims-style snapped furniture and floor placement on a single XZ
plot. Wall-edge topology, room generation, multiple storeys, and free-angle/free
placement are not implemented. Walls remain ordinary footprint-based buildables.

## Verification

`python Tools/Placement/verify_placement.py` builds game source and the NUnit placement
tests against installed Unity assemblies, then runs the engine-independent grid
geometry/storage checks. Requires generated Unity `.csproj` files, installed
Unity assemblies, Python, and .NET 9. Output is written beneath `Temp`.

Run `PlacementRegressionTests` in Unity's **Test Runner > EditMode** for occupancy,
failed moves, rotation, floor preservation, support rules, cancellation, invalid
prefabs, and adjacency. Compilation of these tests does not execute them.

Verification during implementation: game/test source compiled with zero errors;
16,781 engine-independent assertions passed. A full generated-project build was
blocked by two `CS0118` errors in Unity's AI Assistant AssetGenerators package.
The connected Unity MCP tool reported its connection revoked, so Editor tests,
Play Mode interaction, rendering, and player builds have not been verified.
