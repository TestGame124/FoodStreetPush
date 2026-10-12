#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Placement;
using NUnit.Framework;
using UnityEngine;
using GridLayout = FoodStreet.Placement.GridLayout;

public class PlacementRegressionTests
{
    readonly List<Object> owned = new List<Object>();
    GridXZ<GridBuildingSystem3D.GridObject> cells;
    PlacementService service;

    [SetUp] public void SetUp()
    {
        cells = new GridXZ<GridBuildingSystem3D.GridObject>(8, 6, .5f, new Vector3(-4, 2, -3),
            (g, x, z) => new GridBuildingSystem3D.GridObject(g, x, z));
        service = new PlacementService(cells);
    }
    [TearDown] public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    PlacedObjectTypeSO Type(int width = 1, int height = 1, bool floor = false)
    {
        var type = ScriptableObject.CreateInstance<PlacedObjectTypeSO>(); owned.Add(type);
        type.width = width; type.height = height;
        type.categoryType = floor ? ObjectCategory.Floor : ObjectCategory.Decoration;
        return type;
    }
    PlacedObject_Done Place(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir dir = PlacedObjectTypeSO.Dir.Down)
    {
        var go = new GameObject("Placement test"); owned.Add(go);
        var placed = go.AddComponent<PlacedObject_Done>();
        placed.ConfigurePlacement(type, origin, dir);
        placed.RestorePlacementPose(service.Layout.Pivot(type, origin, dir));
        service.Register(placed);
        return placed;
    }
    PlacementProfile Profile(PlacedObjectTypeSO type)
    {
        var profile = ScriptableObject.CreateInstance<PlacementProfile>(); owned.Add(profile);
        type.placementProfile = profile; return profile;
    }

    [Test] public void ShiftedGridUsesFloorForNegativeCoordinates()
    {
        Assert.AreEqual(new Vector2Int(-1, -1), service.Layout.WorldToCell(new Vector3(-4.01f, 20, -3.01f)));
        Assert.AreEqual(new Vector3(-3, 2, -1.5f), service.Layout.CellToWorld(new Vector2Int(2, 3)));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void RotatedNonSquareFootprintAndSavePivotRoundTrip(int turn)
    {
        var type = Type(2, 3); var origin = new Vector2Int(2, 1); var dir = (PlacedObjectTypeSO.Dir)turn;
        var pivot = service.Layout.Pivot(type, origin, dir);
        Assert.AreEqual(origin, service.Layout.PivotToCell(type, pivot, dir));
        Assert.AreEqual((turn & 1) == 0 ? new Vector2Int(2, 3) : new Vector2Int(3, 2), type.GetFootprintSize(dir));
        Assert.AreEqual(6, type.GetGridPositionList(origin, dir).Count);
    }
    [Test] public void EdgeRejectsWholeFootprintInsteadOfClamping()
    {
        Assert.AreEqual(PlacementFailure.OutsideGrid, service.Validate(Type(2, 3), new Vector2Int(7, 4), PlacedObjectTypeSO.Dir.Down));
        Assert.AreEqual(PlacementFailure.OutsideGrid, service.Validate(Type(), new Vector2Int(-1, 0), PlacedObjectTypeSO.Dir.Down));
    }
    [Test] public void InvalidSolidDefinitionIsRejected()
    {
        Assert.AreEqual(PlacementFailure.InvalidDefinition, service.Validate(Type(0, 0), Vector2Int.zero, PlacedObjectTypeSO.Dir.Down));
    }
    [Test] public void LegacyFloorsHaveOneCellFootprint()
    {
        var type = Type(0, 0, true);
        Assert.AreEqual(Vector2Int.one, type.GetFootprintSize(PlacedObjectTypeSO.Dir.Down));
        Assert.AreEqual(1, type.GetGridPositionList(Vector2Int.zero, PlacedObjectTypeSO.Dir.Down).Count);
        Assert.AreEqual(PlacementFailure.None, service.Validate(type, Vector2Int.zero, PlacedObjectTypeSO.Dir.Down));
    }
    [Test] public void BlockedMoveLeavesOriginalPoseAndOccupancyUntouched()
    {
        var moved = Place(Type(2, 1), Vector2Int.zero);
        var blocker = Place(Type(), new Vector2Int(2, 0));
        Vector3 before = moved.transform.position;
        Assert.IsFalse(service.TryMove(moved, new Vector2Int(1, 0), moved.Dir));
        Assert.AreEqual(before, moved.transform.position);
        Assert.AreSame(moved, service.GetAt(Vector2Int.zero));
        Assert.AreSame(moved, service.GetAt(new Vector2Int(1, 0)));
        Assert.AreSame(blocker, service.GetAt(new Vector2Int(2, 0)));
    }
    [Test] public void OverlappingSelfMoveSucceedsAndClearsOnlyOldCells()
    {
        var moved = Place(Type(2, 1), Vector2Int.zero);
        Assert.IsTrue(service.TryMove(moved, new Vector2Int(1, 0), moved.Dir));
        Assert.IsNull(service.GetAt(Vector2Int.zero));
        Assert.AreSame(moved, service.GetAt(new Vector2Int(1, 0)));
        Assert.AreSame(moved, service.GetAt(new Vector2Int(2, 0)));
    }
    [Test] public void RotationUpdatesPivotAndFootprintTogether()
    {
        var moved = Place(Type(2, 1), Vector2Int.zero);
        Assert.IsTrue(service.TryMove(moved, Vector2Int.zero, PlacedObjectTypeSO.Dir.Left));
        Assert.AreEqual(new Vector3(-4, 2, -2), moved.transform.position);
        Assert.IsNull(service.GetAt(new Vector2Int(1, 0)));
        Assert.AreSame(moved, service.GetAt(new Vector2Int(0, 1)));
    }
    [Test] public void FurnitureMovePreservesUnderlyingFloor()
    {
        var floor = Place(Type(1, 1, true), Vector2Int.zero);
        var solid = Place(Type(), Vector2Int.zero);
        Assert.AreSame(solid, cells.GetGridObject(0, 0).GetPlacedObject());
        Assert.IsTrue(service.TryMove(solid, Vector2Int.one, solid.Dir));
        Assert.AreSame(floor, cells.GetGridObject(0, 0).GetPlacedObject());
        Assert.AreSame(floor, service.GetAt(Vector2Int.zero, true));
    }
    [Test] public void RemovingFloorDoesNotClearFurniture()
    {
        var floor = Place(Type(1, 1, true), Vector2Int.zero);
        var solid = Place(Type(), Vector2Int.zero);
        service.Unregister(floor);
        Assert.IsNull(service.GetAt(Vector2Int.zero, true));
        Assert.AreSame(solid, service.GetAt(Vector2Int.zero));
    }
    [Test] public void FloorRequirementChecksEveryFootprintCell()
    {
        var type = Type(2, 1); Profile(type).requiresFloor = true;
        Place(Type(1, 1, true), Vector2Int.zero);
        Assert.AreEqual(PlacementFailure.FloorRequired, service.Validate(type, Vector2Int.zero, PlacedObjectTypeSO.Dir.Down));
        Place(Type(1, 1, true), new Vector2Int(1, 0));
        Assert.AreEqual(PlacementFailure.None, service.Validate(type, Vector2Int.zero, PlacedObjectTypeSO.Dir.Down));
    }
    [Test] public void RotationPolicyIsEnforcedByService()
    {
        var type = Type(); Profile(type).allowRotation = false;
        Assert.AreEqual(PlacementFailure.RotationNotAllowed, service.Validate(type, Vector2Int.zero, PlacedObjectTypeSO.Dir.Left));
    }
    [Test] public void DuplicateUnregisterCannotRemoveAnotherObject()
    {
        var first = Place(Type(), Vector2Int.zero); service.Unregister(first);
        var second = Place(Type(), Vector2Int.zero); service.Unregister(first);
        Assert.AreSame(second, service.GetAt(Vector2Int.zero));
    }
    [Test] public void InvalidPrefabDoesNotOccupyCells()
    {
        Assert.IsFalse(service.TryPlace(Type(), Vector2Int.zero, PlacedObjectTypeSO.Dir.Down, out var placed));
        Assert.IsNull(placed); Assert.IsNull(service.GetAt(Vector2Int.zero));
    }
    [Test] public void CustomAdjacencyRuleRejectsDiagonalAndAcceptsSharedEdge()
    {
        var type = Type(); var neighborType = Type();
        var rule = ScriptableObject.CreateInstance<AdjacentObjectRule>(); owned.Add(rule);
        rule.requiredNeighbor = neighborType;
        Profile(type).rules = new PlacementRule[] { rule };
        Place(neighborType, Vector2Int.zero);
        Assert.AreEqual(PlacementFailure.RuleRejected, service.Validate(type, Vector2Int.one, PlacedObjectTypeSO.Dir.Down));
        Assert.AreEqual(PlacementFailure.None, service.Validate(type, new Vector2Int(1, 0), PlacedObjectTypeSO.Dir.Down));
    }
    [Test] public void CancelKeepsReservedCellsAndOriginalPose()
    {
        var host = new GameObject("Test grid"); owned.Add(host);
        var facade = host.AddComponent<GridBuildingSystem3D>();
        facade.Initialize(8, 6, .5f, Vector3.zero);
        var type = Type(2, 1);
        var target = Place(type, Vector2Int.zero);
        facade.Placement.Register(target);
        facade.selectedObject = target;
        facade.SetPlacedObjectTypeSO(type);
        facade.DeselectObjectType();
        Assert.AreSame(target, facade.Placement.GetAt(Vector2Int.zero));
        Assert.AreEqual(Vector3.zero, target.transform.position);
        Assert.IsNull(facade.selectedObject);
    }
    [Test] public void SupportingFloorCannotBeRemovedOrMoved()
    {
        var floor = Place(Type(1, 1, true), Vector2Int.zero);
        var type = Type(); Profile(type).requiresFloor = true;
        Place(type, Vector2Int.zero);
        Assert.IsFalse(service.CanRemove(floor));
        Assert.IsFalse(service.TryMove(floor, Vector2Int.one, floor.Dir));
        Assert.AreSame(floor, service.GetAt(Vector2Int.zero, true));
    }
    [Test] public void BlockedUndoIsRetainedAndCanRetryAfterObstacleIsRemoved()
    {
        var host = new GameObject("Undo test grid"); owned.Add(host);
        var facade = host.AddComponent<GridBuildingSystem3D>(); facade.Initialize(8, 6, 1, Vector3.zero);
        var context = new LastOrders.Build.BuildContext(null, null, facade) { IgnoreClockCost = true };
        var target = Place(Type(), Vector2Int.zero); facade.Placement.Register(target);
        var stack = new LastOrders.Build.BuildCommandStack(context, 5);
        Assert.IsTrue(stack.Execute(new LastOrders.Build.MoveBuildableCommand(target, Vector2Int.one)));
        var blocker = Place(Type(), Vector2Int.zero); facade.Placement.Register(blocker);
        Assert.IsFalse(stack.UndoLast());
        Assert.AreEqual(1, stack.History.Count);
        facade.Placement.Unregister(blocker);
        Assert.IsTrue(stack.UndoLast());
        Assert.AreEqual(Vector2Int.zero, target.Origin);
        Assert.AreEqual(0, stack.History.Count);
    }
    [Test] public void UndoCannotMoveAReusedPooledInstance()
    {
        var host = new GameObject("Identity test grid"); owned.Add(host);
        var facade = host.AddComponent<GridBuildingSystem3D>(); facade.Initialize(8, 6, 1, Vector3.zero);
        var context = new LastOrders.Build.BuildContext(null, null, facade) { IgnoreClockCost = true };
        var target = Place(Type(), Vector2Int.zero); facade.Placement.Register(target);
        target.RestoreIdentity(123);
        var command = new LastOrders.Build.MoveBuildableCommand(target, Vector2Int.one);
        Assert.IsTrue(command.Execute(context));
        target.RestoreIdentity(456);
        Assert.IsFalse(command.CanUndo(context));
        command.Undo(context);
        Assert.AreEqual(Vector2Int.one, target.Origin);
    }
    [Test] public void FixtureBlocksSolidsOverItButNotFloorsOrNeighbours()
    {
        var owner = new object();
        // Cells are 0.5 m from (-4, -3): this covers cells x 2-3, z 2 exactly.
        PlacementService.SetFixture(owner, Rect.MinMaxRect(-3f, -2f, -2f, -1.5f));
        try
        {
            Assert.AreEqual(PlacementFailure.Occupied, service.Validate(Type(), new Vector2Int(2, 2), PlacedObjectTypeSO.Dir.Down));
            Assert.AreEqual(PlacementFailure.Occupied, service.Validate(Type(2, 2), new Vector2Int(1, 1), PlacedObjectTypeSO.Dir.Down));
            Assert.AreEqual(PlacementFailure.None, service.Validate(Type(1, 1, true), new Vector2Int(2, 2), PlacedObjectTypeSO.Dir.Down));
            Assert.AreEqual(PlacementFailure.None, service.Validate(Type(), new Vector2Int(4, 2), PlacedObjectTypeSO.Dir.Down), "only touches the fixture's side");
            Assert.AreEqual(PlacementFailure.None, service.Validate(Type(), new Vector2Int(2, 3), PlacedObjectTypeSO.Dir.Down), "only touches the fixture's side");
        }
        finally
        {
            PlacementService.SetFixture(owner, null);
        }

        Assert.AreEqual(PlacementFailure.None, service.Validate(Type(), new Vector2Int(2, 2), PlacedObjectTypeSO.Dir.Down));
    }
}
#endif
