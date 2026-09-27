using System.Text;
using FoodStreet.Navigation;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Heat;
using UnityEngine;

namespace LastOrders.Debugging
{
    /// <summary>
    /// The development overlay. 24.2 is emphatic that this gets built on day one, not when
    /// something breaks: every grid bug is invisible in the inspector and obvious the instant
    /// the grid can be drawn.
    ///
    /// It shows the three things that are otherwise impossible to reason about at runtime -
    /// what the navigation layer thinks the world looks like, what the pools are holding,
    /// and where Heat actually came from - plus the near-miss margin, because 15.7 cannot be
    /// tuned by feel alone.
    /// </summary>
    public sealed class LastOrdersDebugOverlay : MonoBehaviour
    {
        public enum GridLayer
        {
            Off,
            Walkability,
            Cost,
            Clearance,
            FlowDirection,
        }

        [SerializeField] GameLifetimeScope scope;

        [Header("Display")]
        [SerializeField] bool showPanel = true;
        [SerializeField] GridLayer gridLayer = GridLayer.Off;
        [SerializeField] FlowFieldId flowFieldToDraw = FlowFieldId.Doors;

        [Tooltip("Height above ground to draw the grid gizmos.")]
        [SerializeField] float drawHeight = 0.05f;

        [Tooltip("Metres around the scene view's focus to draw grid gizmos for. The whole " +
                 "grid is drawn when this is zero or less.")]
        [SerializeField] float gizmoRange = 24f;

        [Header("Input")]
        [SerializeField] KeyCode togglePanelKey = KeyCode.F1;
        [SerializeField] KeyCode cycleLayerKey = KeyCode.F2;

        GameComposition Composition => scope != null ? scope.Composition : null;

        readonly StringBuilder builder = new StringBuilder(512);
        GUIStyle style;

        void Update()
        {
            if (Input.GetKeyDown(togglePanelKey)) showPanel = !showPanel;

            if (Input.GetKeyDown(cycleLayerKey))
            {
                gridLayer = (GridLayer)(((int)gridLayer + 1) % System.Enum.GetValues(typeof(GridLayer)).Length);
            }
        }

        void OnGUI()
        {
            if (!showPanel) return;

            GameComposition composition = Composition;
            if (composition == null) return;

            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                alignment = TextAnchor.UpperLeft,
            };

            builder.Clear();
            AppendCycle(composition);
            AppendHeat(composition);
            AppendPerimeter(composition);
            AppendPools(composition);
            AppendNight(composition);
            AppendDefense(composition);

            GUI.Box(new Rect(10, 10, 340, 420), GUIContent.none);
            GUI.Label(new Rect(20, 16, 320, 410), builder.ToString(), style);
        }

        void AppendCycle(GameComposition composition)
        {
            builder.Append("<b>CYCLE</b>\n");
            builder.Append($"Day {composition.Time.CurrentDay}  ");
            builder.Append($"{composition.Machine.CurrentPhase}\n");
            builder.Append($"Clock {composition.Time.ClockMinutes:0.0} / 60  ");
            builder.Append($"phase {composition.Time.PhaseElapsedSeconds:0.0}s\n\n");
        }

        void AppendHeat(GameComposition composition)
        {
            HeatService heat = composition.Heat;

            builder.Append("<b>HEAT</b>\n");
            builder.Append($"{heat.CurrentHeat:0.0}  tier {heat.ForecastTier}");
            builder.Append(heat.IsLocked ? "  <color=#ff8080>LOCKED</color>\n" : "\n");

            // The breakdown is the point. A single Heat number tells you nothing about why
            // the night went badly; this tells you which decision cost the player.
            foreach (var entry in heat.Breakdown)
            {
                if (Mathf.Approximately(entry.Value, 0f)) continue;
                builder.Append($"  {entry.Key}: {entry.Value:0.0}\n");
            }

            builder.Append('\n');
        }

        void AppendPerimeter(GameComposition composition)
        {
            var readout = composition.Perimeter.Current;

            builder.Append("<b>PERIMETER</b>\n");
            builder.Append($"Seats {readout.SeatingCapacity}  Doors {readout.DoorCount}\n");
            builder.Append($"Integrity {readout.Integrity:P0}  Gaps {readout.Gaps}\n\n");
        }

        void AppendPools(GameComposition composition)
        {
            builder.Append("<b>POOLS</b>\n");
            builder.Append($"Active {composition.Pools.TotalActive}\n");

            // A non-zero violation count means something is being released dirty. It will not
            // look like a pooling bug when it finally surfaces, so it is worth watching here.
            int violations = PoolIntegrity.ViolationCount;
            builder.Append(violations > 0
                ? $"<color=#ff4040>Integrity violations: {violations}</color>\n\n"
                : "Integrity clean\n\n");
        }

        void AppendNight(GameComposition composition)
        {
            DayReport report = composition.Context.Report;

            builder.Append("<b>LAST NIGHT</b>\n");
            builder.Append($"Killed {report.EnemiesKilled}  Served {report.CustomersServed}\n");
            builder.Append($"Lost  D{report.StructuresDamaged} W{report.StructuresWrecked} X{report.StructuresDestroyed}\n");

            // 15.7's near-miss margin. Tuning wants this clustering just above zero: the last
            // enemy dying against a barricade about to break is the night players talk about.
            string colour = report.ClosestCall < 0.15f ? "#80ff80" : "#ffffff";
            builder.Append($"Closest call: <color={colour}>{report.ClosestCall:P0}</color>\n");
        }

        void AppendDefense(GameComposition composition)
        {
            if (composition.Defense.All.Count == 0) return;

            builder.Append("\n<b>DEFENSE</b>\n");

            // One line per post. A tower that is built but silent is the thing to spot here -
            // nobody posted, or the guard fled - because in the scene it just looks quiet.
            foreach (DefenseUnit unit in composition.Defense.All)
            {
                builder.Append($"{unit.Id}: ");

                if (!unit.IsBuilt)
                {
                    builder.Append("empty site\n");
                    continue;
                }

                builder.Append($"L{unit.LevelNumber} ");

                if (unit.NeedsCrew)
                {
                    builder.Append(unit.Crew == null
                        ? "<color=#ff8080>no guard</color> "
                        : unit.CrewOnDuty ? $"{unit.Crew.Name} " : $"<color=#ffb060>{unit.Crew.Name} fled</color> ");
                }

                if (unit.Target != null) builder.Append($"-> {unit.Target.DisplayName} ");
                builder.Append($"shots {unit.ShotsFired}\n");
            }
        }

        /// <summary>
        /// Where to centre the gizmo window: the scene view's pivot in the editor, the
        /// component's own position otherwise.
        /// </summary>
        Vector3 GizmoFocus()
        {
#if UNITY_EDITOR
            UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
            if (view != null) return view.pivot;
#endif
            return transform.position;
        }

        void OnDrawGizmos()
        {
            if (gridLayer == GridLayer.Off) return;

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return;

            NavGrid grid = GetNavGrid(nav);
            if (grid == null) return;

            FlowField field = gridLayer == GridLayer.FlowDirection
                ? nav.GetFlowField(flowFieldToDraw)
                : null;

            // Every cell, every frame, is fine for a 3,500-cell grid in the editor and
            // nowhere near acceptable in a build - which is why this is gizmos only.
            //
            // Bounded to a window around the scene view's focus as well, because the blockout
            // scene's 0.5 m build grid works out to over 120,000 nav cells: drawing all of
            // them makes the editor unusable the moment this layer is switched on.
            Vector2Int focus = grid.WorldToCell(GizmoFocus());
            int span = gizmoRange > 0f
                ? Mathf.CeilToInt(gizmoRange / Mathf.Max(grid.CellSize, 1e-3f))
                : Mathf.Max(grid.Width, grid.Height);

            int minX = Mathf.Max(0, focus.x - span);
            int maxX = Mathf.Min(grid.Width - 1, focus.x + span);
            int minZ = Mathf.Max(0, focus.y - span);
            int maxZ = Mathf.Min(grid.Height - 1, focus.y + span);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int index = grid.Index(x, z);
                    Vector3 world = grid.CellToWorld(x, z) + Vector3.up * drawHeight;

                    switch (gridLayer)
                    {
                        case GridLayer.Walkability:
                            Gizmos.color = nav.Grid.IsWalkable(index)
                                ? new Color(0f, 1f, 0f, 0.15f)
                                : new Color(1f, 0f, 0f, 0.25f);
                            Gizmos.DrawCube(world, new Vector3(grid.CellSize, 0.01f, grid.CellSize));
                            break;

                        case GridLayer.Cost:
                        {
                            float t = nav.Grid.GetCost(index) / 64f;
                            Gizmos.color = new Color(t, 1f - t, 0f, 0.18f);
                            Gizmos.DrawCube(world, new Vector3(grid.CellSize, 0.01f, grid.CellSize));
                            break;
                        }

                        case GridLayer.Clearance:
                        {
                            float t = nav.Grid.GetClearance(index) / 8f;
                            Gizmos.color = new Color(0f, t, 1f - t, 0.18f);
                            Gizmos.DrawCube(world, new Vector3(grid.CellSize, 0.01f, grid.CellSize));
                            break;
                        }

                        case GridLayer.FlowDirection:
                        {
                            if (field == null || !field.IsValid) break;

                            Vector3 direction = field.GetDirection(new Vector2Int(x, z));
                            if (direction.sqrMagnitude < 0.0001f) break;

                            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                            Gizmos.DrawRay(world, direction * grid.CellSize * 0.8f);
                            break;
                        }
                    }
                }
            }
        }

        static NavGrid GetNavGrid(NavigationSystem nav) => nav.Grid?.Grid;
    }
}
