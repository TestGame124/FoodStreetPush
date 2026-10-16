using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using FoodStreet.Navigation;
using FoodStreet.Placement;
using FoodStreet.Visitors;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Effects;
using LastOrders.Field;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.Kitchen;
using LastOrders.Night;
using LastOrders.Perimeter;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;
using UnityEngine.Serialization;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// The restaurant's own walls in the scene: where they stand, the button that offers to push
    /// one out, the ground each side could grow into, and moving the walls when it does.
    ///
    /// Growing it is three steps. The EXPAND button over the front wall - or a tap on any of the
    /// walls - asks "Expand the restaurant?" with what the next expansion costs. Yes lights up
    /// the strip of ground behind, left of and right of the building that each side would take
    /// in: green where it is clear, red where something is standing on it. Tapping a strip
    /// checks again that nothing is in the way and that the player can pay, and only then pushes
    /// that wall out. Anything standing on the ground is in the way - a plant pot, the stores, a
    /// shelter, a tower, a stretch of the compound wall, a parking bay - and so is the compound
    /// wall itself: the restaurant never grows past it.
    ///
    /// The walls are plain cubes with their length along local Z, as the layout tools lay them.
    /// A side that grows moves its own wall out and lengthens the two walls either side of it.
    /// The front wall, in two pieces round the door, never moves: the door stays where the
    /// visitors and the defenses know it. Growing out the back carries the kitchen back with the
    /// back wall - the stations and anything else built on the grid there, the serving pass and
    /// the waiters' places at it - so it is the dining room that gets bigger. The floor
    /// (<see cref="RestaurantFloor"/>) is laid again between the walls on every frame they move.
    ///
    /// It also stands the walls up for the night: the service is registered as the barrier that
    /// stops every zombie at them, and the walls' outlines go to the nav grid so the night's
    /// routes lead round the building to its door rather than through its side - the door the
    /// defenses out front are watching.
    /// </summary>
    [DefaultExecutionOrder(1130)]
    public sealed class RestaurantShell : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] GameLifetimeScope scope;

        [Header("Walls")]
        [Tooltip("The kitchen wall, opposite the door. It moves out, the kitchen with it, when the " +
                 "restaurant grows at the back; it lengthens when a side grows.")]
        [SerializeField] Transform backWall;

        [Tooltip("The side wall on the left, seen from outside the front.")]
        [SerializeField] Transform leftWall;

        [Tooltip("The side wall on the right, seen from outside the front.")]
        [SerializeField] Transform rightWall;

        [Tooltip("The front wall between the left wall and the door.")]
        [SerializeField] Transform frontLeftWall;

        [Tooltip("The front wall between the door and the right wall.")]
        [SerializeField] Transform frontRightWall;

        [Tooltip("What stands at the front wall: the door, the visitor route's door nodes, the night " +
                 "shelter's door nodes. Never counted as in the way of the walls.")]
        [SerializeField] Transform[] frontFixtures;

        [Tooltip("Routes round the building are kept outside its footprint. Found when left empty.")]
        [SerializeField] NightShelter shelter;

        [Tooltip("The floor inside, laid wall to wall again whenever a wall moves.")]
        [SerializeField] RestaurantFloor floor;

        [Header("Choosing where to grow")]
        [Tooltip("A transparent unlit material for the strips. Colour is set per strip.")]
        [SerializeField] Material highlightMaterial;

        [SerializeField] Color clearColour = new Color(0.3f, 0.9f, 0.35f, 0.42f);
        [SerializeField] Color blockedColour = new Color(0.95f, 0.3f, 0.25f, 0.42f);

        [Tooltip("Seconds between checks of what is standing on the strips while they are shown.")]
        [SerializeField] float recheckInterval = 0.4f;

        [Tooltip("Metres a worker's waiting spot is kept clear of the walls when the building grows over it.")]
        [SerializeField] float homeClearance = 1f;

        [Header("Effects")]
        [Tooltip("Played where a zombie's blow lands on the walls.")]
        [FormerlySerializedAs("killEffect")]
        [SerializeField] PooledEffect hitEffect;

        [Tooltip("Played along a wall as it moves out.")]
        [SerializeField] PooledEffect buildEffect;

        /// <summary>How many times a strip's area a collider round it has to be to count as
        /// ground rather than as something standing on it.</summary>
        const float RegionScale = 25f;

        /// <summary>Metres across the frame round each strip.</summary>
        const float EdgeWidth = 0.18f;

        /// <summary>How deep the kitchen is when there is no floor to say.</summary>
        const float DefaultKitchenDepth = 3f;

        /// <summary>Metres from the serving pass a waiter's place can be and still be at it.</summary>
        const float WaiterReach = 1.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Regex CopySuffix = new Regex(@"\s*\(\d+\)$");
        static readonly Regex WordBreak = new Regex(@"(?<=[a-z])(?=[A-Z])");

        static Mesh quad;

        readonly Dictionary<RestaurantSide, Strip> strips = new Dictionary<RestaurantSide, Strip>();
        readonly Collider[] overlap = new Collider[64];

        GameComposition composition;
        RestaurantService restaurant;
        MaterialPropertyBlock block;

        Transform expandAnchor;
        ActionPromptView expandButton;
        float nextRefresh;

        bool choosing;
        bool building;
        int handledTapFrame = -1;
        float nextRecheck;

        /// <summary>The nav grid has the walls as they stand now. False until it exists to be told.</summary>
        bool wallsOnGrid;

        readonly List<Rect> wallAreas = new List<Rect>(5);

        // What stands in the kitchen, gathered when the back wall is about to go out.
        readonly List<PlacedObject_Done> kitchenPlaced = new List<PlacedObject_Done>();
        readonly List<ServingPass> kitchenPasses = new List<ServingPass>();
        readonly List<Waiter> kitchenWaiters = new List<Waiter>();

        /// <summary>One side's strip of ground while the player is choosing.</summary>
        sealed class Strip
        {
            public RestaurantSide Side;
            public GameObject Root;
            public MeshRenderer Fill;

            /// <summary>South, north, west, east: a solid frame round the fill, so the strip reads
            /// as an area on grass the same green as it.</summary>
            public MeshRenderer[] Edges;

            public BoxCollider Tap;
            public ActionPromptView Button;
            public Rect Area;
            public bool Clear;
            public string Obstacle;
        }

        public string DisplayName => "Restaurant";

        public bool IsChoosing => choosing;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError("[RestaurantShell] No GameLifetimeScope, so the restaurant cannot grow and " +
                               "nothing stops zombies at its walls.", this);
                enabled = false;
                return;
            }

            if (!HasWalls())
            {
                Debug.LogError("[RestaurantShell] Its walls are not assigned. Run 'Last Orders / Restaurant / " +
                               "Set Up Repairs & Expansion (Current Scene)'.", this);
                enabled = false;
                return;
            }

            restaurant = composition.Restaurant;
            if (shelter == null) shelter = FindFirstObjectByType<NightShelter>();

            restaurant.SetFootprint(MeasureWalls(), FrontDirection());
            Refit();
            FitFloor();

            MovementBarriers.Register(restaurant);
            wallsOnGrid = PutWallsOnGrid();

            restaurant.OnHit += HandleHit;
            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
        }

        void OnDestroy()
        {
            Mouse3D.WorldTapResolved -= HandleWorldTap;

            if (restaurant != null)
            {
                restaurant.OnHit -= HandleHit;
                MovementBarriers.Unregister(restaurant);
            }

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav != null && nav.Grid != null) nav.Grid.SetStaticWalls(this, null);

            if (composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        void OnDisable()
        {
            // Not while the scene is coming down: the pool would re-parent the buttons under a
            // root that is being destroyed in the same pass.
            if (composition == null || !gameObject.scene.isLoaded) return;

            EndChoosing(null);
            ReleaseExpandButton();
        }

        void Update()
        {
            if (restaurant == null) return;

            // The nav grid can come up after this does.
            if (!wallsOnGrid) wallsOnGrid = PutWallsOnGrid();

            if (choosing)
            {
                Pulse();

                nextRecheck -= Time.unscaledDeltaTime;
                if (nextRecheck <= 0f) Recheck();
            }

            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh > 0f) return;

            nextRefresh = 0.25f;
            DrawExpandButton();
        }

        // --- Asking ------------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (restaurant == null) return "The game systems are not running.";
            if (choosing) return "Tap one of the highlighted areas to expand there, or anywhere else to cancel.";
            if (building) return "The walls are going up.";

            return OpenPopup();
        }

        /// <summary>"Expand the restaurant?" The reply to the tap, or null when the popup says it all.</summary>
        string OpenPopup()
        {
            RestaurantExpansionPopupView prefab = composition.WorldUi.Catalog != null
                ? composition.WorldUi.Catalog.restaurantExpansionPopup
                : null;

            if (prefab == null)
            {
                return "There is no restaurant expansion popup in the UI catalog. Run 'Last Orders / Restaurant / " +
                       "Set Up Repairs & Expansion (Current Scene)'.";
            }

            RestaurantExpansionPopupView popup = composition.Popups.Show(prefab);
            if (popup == null) return null;

            popup.Bind(restaurant.GetOffer, BeginChoosing, this);
            ReleaseExpandButton();
            return null;
        }

        bool PopupIsOpen =>
            composition.Popups.Current is RestaurantExpansionPopupView view && ReferenceEquals(view.Owner, this);

        /// <summary>The standing EXPAND button over the front wall, while the restaurant can grow.</summary>
        void DrawExpandButton()
        {
            bool show = !choosing && !building && !PopupIsOpen && restaurant.AnyStepsLeft &&
                        restaurant.IsExpansionTime(out _);

            if (!show)
            {
                ReleaseExpandButton();
                return;
            }

            if (expandButton != null && expandButton.IsLive && expandButton.Anchor == ExpandAnchor) return;

            expandButton = composition.WorldUi.ShowExpandPrompt(ExpandAnchor, () => TapFeedback.Report(OpenPopup()));
        }

        void ReleaseExpandButton()
        {
            if (expandButton == null) return;

            if (expandButton.IsLive && expandButton.Anchor == expandAnchor) expandButton.Release();
            expandButton = null;
        }

        /// <summary>Over the middle of the front wall on the right of the door, clear of the door itself.</summary>
        Transform ExpandAnchor
        {
            get
            {
                if (expandAnchor == null)
                {
                    expandAnchor = new GameObject("ExpandAnchor").transform;
                    expandAnchor.SetParent(transform, false);
                }

                return expandAnchor;
            }
        }

        void PlaceExpandAnchor()
        {
            Transform wall = frontRightWall != null ? frontRightWall : frontLeftWall;
            Bounds bounds = BoundsOf(wall);
            ExpandAnchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        // --- Choosing a side ------------------------------------------------------------------

        void BeginChoosing()
        {
            if (restaurant == null || choosing || building) return;

            if (!restaurant.IsExpansionTime(out string reason))
            {
                TapFeedback.Report(reason);
                return;
            }

            choosing = true;
            handledTapFrame = Time.frameCount;
            ReleaseExpandButton();

            foreach (RestaurantSide side in new[] { RestaurantSide.Back, RestaurantSide.Left, RestaurantSide.Right })
                if (restaurant.StepsLeft(side) > 0) ShowStrip(side);

            Recheck();

            Mouse3D.WorldTapResolved -= HandleWorldTap;
            Mouse3D.WorldTapResolved += HandleWorldTap;

            TapFeedback.Report("Tap a highlighted area to expand the restaurant there. Green is clear, red has " +
                               "something in the way. Tap anywhere else to cancel.");
        }

        void EndChoosing(string message)
        {
            if (!choosing) return;

            choosing = false;
            Mouse3D.WorldTapResolved -= HandleWorldTap;

            foreach (Strip strip in strips.Values) HideStrip(strip);

            if (!string.IsNullOrEmpty(message)) TapFeedback.Report(message);
        }

        /// <summary>A tap on the world that was not on one of the strips puts them away.</summary>
        void HandleWorldTap(bool handled)
        {
            if (Time.frameCount == handledTapFrame) return;
            EndChoosing("Expansion cancelled.");
        }

        /// <summary>The player picked a side, by its strip or its button. The reply to show.</summary>
        public string ChooseSide(RestaurantSide side)
        {
            handledTapFrame = Time.frameCount;

            if (restaurant == null || !choosing) return null;
            if (building) return "The walls are going up.";

            if (restaurant.StepsLeft(side) <= 0)
                return $"The restaurant cannot grow any further {RestaurantService.Describe(side)}.";

            if (!restaurant.IsExpansionTime(out string reason))
            {
                EndChoosing(null);
                return reason;
            }

            // What is standing there first, then the price: moving a plant pot is free, and the
            // player should not be told to go and earn coins for ground they could not use anyway.
            Rect area = restaurant.StripFor(side);
            if (FindObstacle(area, out string obstacle))
            {
                Warn(area, "In the way!");
                return $"{obstacle} is in the way. Clear the ground {RestaurantService.Describe(side)} first.";
            }

            if (side == RestaurantSide.Back && !KitchenCanGoBack(out string blocked))
            {
                Warn(area, "In the way!");
                return blocked;
            }

            if (!restaurant.TryExpand(side, out string message))
            {
                Warn(area, "Can't afford");
                return message;
            }

            Rect grown = restaurant.Footprint;
            EndChoosing(null);
            Build(side, area, grown);
            return message;
        }

        void ShowStrip(RestaurantSide side)
        {
            if (!strips.TryGetValue(side, out Strip strip))
            {
                strip = CreateStrip(side);
                strips[side] = strip;
            }

            Rect area = restaurant.StripFor(side);
            strip.Area = area;

            float y = transform.position.y;
            strip.Root.transform.SetPositionAndRotation(new Vector3(area.center.x, y, area.center.y), Quaternion.identity);

            // The quad lies flat in X and Z, so Z is its depth.
            strip.Fill.transform.localScale = new Vector3(area.width, 1f, area.height);

            float halfW = area.width * 0.5f;
            float halfH = area.height * 0.5f;
            float edge = Mathf.Min(EdgeWidth, Mathf.Min(area.width, area.height) * 0.25f);

            PlaceEdge(strip.Edges[0], new Vector3(0f, 0f, -halfH + edge * 0.5f), new Vector3(area.width, 1f, edge));
            PlaceEdge(strip.Edges[1], new Vector3(0f, 0f, halfH - edge * 0.5f), new Vector3(area.width, 1f, edge));
            PlaceEdge(strip.Edges[2], new Vector3(-halfW + edge * 0.5f, 0f, 0f), new Vector3(edge, 1f, area.height));
            PlaceEdge(strip.Edges[3], new Vector3(halfW - edge * 0.5f, 0f, 0f), new Vector3(edge, 1f, area.height));

            // Tall, so a tap anywhere over the strip lands on it before what is standing there.
            strip.Tap.size = new Vector3(area.width, 2.4f, area.height);
            strip.Tap.center = new Vector3(0f, 1.2f, 0f);

            strip.Root.SetActive(true);

            strip.Button = composition.WorldUi.ShowExpandPrompt(strip.Root.transform,
                                                                () => TapFeedback.Report(ChooseSide(side)));
        }

        void HideStrip(Strip strip)
        {
            if (strip.Button != null && strip.Button.IsLive && strip.Button.Anchor == strip.Root.transform)
                strip.Button.Release();

            strip.Button = null;
            if (strip.Root != null) strip.Root.SetActive(false);
        }

        Strip CreateStrip(RestaurantSide side)
        {
            var root = new GameObject($"ExpandArea_{side}");
            root.transform.SetParent(transform, true);
            root.SetActive(false);

            MeshRenderer fill = Flat("Fill", root.transform, 0.04f);

            var edges = new MeshRenderer[4];
            for (int i = 0; i < edges.Length; i++) edges[i] = Flat($"Edge_{i}", root.transform, 0.05f);

            var tap = root.AddComponent<BoxCollider>();
            tap.isTrigger = true;

            root.AddComponent<RestaurantExpansionHighlight>().Bind(this, side);

            return new Strip { Side = side, Root = root, Fill = fill, Edges = edges, Tap = tap };
        }

        /// <summary>A flat quad just off the ground, drawn in the highlight material, casting nothing.</summary>
        MeshRenderer Flat(string label, Transform parent, float lift)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * lift;
            go.GetComponent<MeshFilter>().sharedMesh = Quad();

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = HighlightMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static void PlaceEdge(MeshRenderer edge, Vector3 centre, Vector3 scale)
        {
            Transform t = edge.transform;
            t.localPosition = new Vector3(centre.x, t.localPosition.y, centre.z);
            t.localScale = scale;
        }

        /// <summary>Asks again what is standing on each strip and colours it to match.</summary>
        void Recheck()
        {
            nextRecheck = recheckInterval;

            foreach (Strip strip in strips.Values)
            {
                if (strip.Root == null || !strip.Root.activeSelf) continue;

                strip.Clear = !FindObstacle(strip.Area, out strip.Obstacle);
            }

            Pulse();
        }

        /// <summary>The strips breathe, so they read as something to tap rather than as paint.</summary>
        void Pulse()
        {
            if (block == null) block = new MaterialPropertyBlock();

            float breath = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);

            foreach (Strip strip in strips.Values)
            {
                if (strip.Fill == null || !strip.Root.activeSelf) continue;

                Color colour = strip.Clear ? clearColour : blockedColour;

                // The frame holds steady and nearly solid; the fill inside it breathes.
                Color frame = colour;
                frame.a = 0.9f;
                Tint(frame);
                foreach (MeshRenderer edge in strip.Edges) edge.SetPropertyBlock(block);

                colour.a *= breath;
                Tint(colour);
                strip.Fill.SetPropertyBlock(block);
            }
        }

        void Tint(Color colour)
        {
            block.SetColor(BaseColorId, colour);
            block.SetColor(ColorId, colour);
        }

        void Warn(Rect area, string text)
        {
            composition.WorldUi.ShowWarning(new Vector3(area.center.x, transform.position.y + 1.2f, area.center.y), text);
        }

        // --- What is in the way ------------------------------------------------------------------

        /// <summary>
        /// True when something stands on the ground a side would take in, with what it is.
        /// Past the compound wall counts, and so does the wall itself, a parking bay, and anything
        /// solid - but not the people walking about, who move out of the way.
        /// </summary>
        public bool FindObstacle(Rect area, out string what)
        {
            Rect probe = Rect.MinMaxRect(area.xMin + 0.05f, area.yMin + 0.05f, area.xMax - 0.05f, area.yMax - 0.05f);

            CompoundPerimeterService compound = composition.Compound;

            if (compound.HasInterior && !InsideCompound(compound, probe))
            {
                what = "The compound wall";
                return true;
            }

            IReadOnlyList<ICompoundStructure> structures = compound.Structures;
            for (int i = 0; i < structures.Count; i++)
            {
                if (!(structures[i] is CompoundStructure piece) || piece == null || !piece.IsPresent) continue;

                var half = new Vector2(piece.Length * 0.5f, piece.Thickness * 0.5f);
                if (!RestaurantService.Overlaps(probe, piece.transform.position, piece.transform.eulerAngles.y, half)) continue;

                what = $"The {piece.DisplayName.ToLowerInvariant()}";
                return true;
            }

            // A bay is the car parked in it, whether or not one is there yet.
            foreach (ParkingSlot bay in FindObjectsByType<ParkingSlot>(FindObjectsSortMode.None))
            {
                if (!RestaurantService.Overlaps(probe, bay.transform.position, bay.transform.eulerAngles.y, new Vector2(1.2f, 2.3f)))
                    continue;

                what = "A parking bay";
                return true;
            }

            var centre = new Vector3(probe.center.x, transform.position.y + 1.55f, probe.center.y);
            var extents = new Vector3(probe.width * 0.5f, 1.45f, probe.height * 0.5f);

            int count = Physics.OverlapBoxNonAlloc(centre, extents, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlap[i];
                if (hit == null || Ignored(hit, probe)) continue;

                what = NameOf(hit);
                return true;
            }

            what = null;
            return false;
        }

        static bool InsideCompound(CompoundPerimeterService compound, Rect area)
        {
            const float inset = 0.1f;

            return compound.IsInside(new Vector3(area.xMin + inset, 0f, area.yMin + inset)) &&
                   compound.IsInside(new Vector3(area.xMax - inset, 0f, area.yMin + inset)) &&
                   compound.IsInside(new Vector3(area.xMin + inset, 0f, area.yMax - inset)) &&
                   compound.IsInside(new Vector3(area.xMax - inset, 0f, area.yMax - inset));
        }

        bool Ignored(Collider hit, Rect probe)
        {
            Transform t = hit.transform;

            // The building itself and its own strips.
            if (t.IsChildOf(transform)) return true;

            if (frontFixtures != null)
            {
                for (int i = 0; i < frontFixtures.Length; i++)
                    if (frontFixtures[i] != null && t.IsChildOf(frontFixtures[i])) return true;
            }

            // Something round the whole strip and many times its size is a region - the ground,
            // the camera's bounds - not something standing on it. A tower bigger than a small
            // strip is still a tower.
            Bounds b = hit.bounds;
            bool surrounds = b.min.x <= probe.xMin && b.max.x >= probe.xMax && b.min.z <= probe.yMin && b.max.z >= probe.yMax;
            if (surrounds && b.size.x * b.size.z >= RegionScale * Mathf.Max(1f, probe.width * probe.height))
                return true;

            // People and cars move, and move out of the way.
            if (hit.GetComponentInParent<WorkerAgent>() != null) return true;
            if (hit.GetComponentInParent<Visitor>() != null) return true;
            if (hit.GetComponentInParent<VisitorVehicle>() != null) return true;
            if (hit.GetComponentInParent<EnemyAgent>() != null) return true;
            if (hit.GetComponentInParent<AreaZombie>() != null) return true;
            if (hit.GetComponentInParent<StaffBehaviour>() != null) return true;

            // World-space buttons and bars.
            if (hit.GetComponentInParent<WorldUiElement>() != null) return true;

            return false;
        }

        /// <summary>What the player would call it: its own name for itself where it has one.</summary>
        static string NameOf(Collider hit)
        {
            ITappable tappable = hit.GetComponentInParent<ITappable>();
            if (tappable != null && !string.IsNullOrEmpty(tappable.DisplayName)) return tappable.DisplayName;

            PlacedObject_Done placed = hit.GetComponentInParent<PlacedObject_Done>();
            if (placed != null && placed.PlacedObjectTypeSO != null && !string.IsNullOrEmpty(placed.PlacedObjectTypeSO.nameString))
                return placed.PlacedObjectTypeSO.nameString;

            // The nearest thing above the collider with behaviour of its own - a prefab's root -
            // rather than the "Cube" the collider happens to sit on.
            Transform named = hit.transform;
            for (Transform t = hit.transform; t != null; t = t.parent)
            {
                if (t.GetComponent<MonoBehaviour>() == null) continue;

                named = t;
                break;
            }

            return Clean(named.name);
        }

        static string NameOf(PlacedObject_Done placed)
        {
            PlacedObjectTypeSO type = placed.PlacedObjectTypeSO;
            return type != null && !string.IsNullOrEmpty(type.nameString) ? type.nameString : Clean(placed.name);
        }

        static string Clean(string raw)
        {
            string name = CopySuffix.Replace(raw ?? string.Empty, string.Empty).Replace('_', ' ').Trim();
            if (name.StartsWith("pf") && name.Length > 2 && char.IsUpper(name[2])) name = name.Substring(2);

            name = WordBreak.Replace(name, " ");
            return string.IsNullOrEmpty(name) ? "Something" : name;
        }

        // --- Building it ---------------------------------------------------------------------------

        void Build(RestaurantSide side, Rect strip, Rect grown)
        {
            Vector2 flat = restaurant.DirectionOf(side);
            var direction = new Vector3(flat.x, 0f, flat.y);
            float depth = restaurant.Tuning.For(side).depth;
            float seconds = restaurant.Tuning.buildSeconds;

            building = true;

            Sequence walls = DOTween.Sequence().SetTarget(this);

            bool kitchenMoves = false;

            switch (side)
            {
                case RestaurantSide.Back:
                    Move(walls, backWall, direction, depth, seconds);
                    Extend(walls, leftWall, direction, depth, seconds);
                    Extend(walls, rightWall, direction, depth, seconds);
                    kitchenMoves = MoveKitchen(walls, direction, depth, seconds);
                    break;

                case RestaurantSide.Left:
                    Move(walls, leftWall, direction, depth, seconds);
                    Extend(walls, backWall, direction, depth, seconds);
                    Extend(walls, frontLeftWall, direction, depth, seconds);
                    break;

                case RestaurantSide.Right:
                    Move(walls, rightWall, direction, depth, seconds);
                    Extend(walls, backWall, direction, depth, seconds);
                    Extend(walls, frontRightWall, direction, depth, seconds);
                    break;
            }

            // Dust along the line the wall is moving to.
            if (buildEffect != null)
            {
                Vector2 a = flat.x != 0f ? new Vector2(flat.x > 0f ? strip.xMax : strip.xMin, strip.yMin) : new Vector2(strip.xMin, flat.y > 0f ? strip.yMax : strip.yMin);
                Vector2 b = flat.x != 0f ? new Vector2(a.x, strip.yMax) : new Vector2(strip.xMax, a.y);

                for (int i = 0; i <= 4; i++)
                {
                    Vector2 at = Vector2.Lerp(a, b, i / 4f);
                    composition.Effects.Play(buildEffect, new Vector3(at.x, transform.position.y + 0.3f, at.y));
                }
            }

            // The floor follows the walls out as they move, not after, and the plates ride the pass.
            walls.OnUpdate(() =>
            {
                FitFloor();
                if (kitchenMoves && ServingPassBoard.Instance != null) ServingPassBoard.Instance.KeepPlatesOnSpots();
            });

            walls.OnComplete(() =>
            {
                building = false;
                FitFloor();
                Refit();
                wallsOnGrid = PutWallsOnGrid();

                if (kitchenMoves)
                {
                    foreach (ServingPass pass in kitchenPasses)
                        if (pass != null) pass.Refit();

                    if (ServingPassBoard.Instance != null) ServingPassBoard.Instance.KeepPlatesOnSpots();
                }

                foreach (WorkerAgent worker in FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
                    worker.KeepHomeOutOf(grown, homeClearance);

                composition.WorldUi.ShowReward(new Vector3(strip.center.x, transform.position.y + 1f, strip.center.y),
                                               $"+{strip.width * strip.height:0} m²");
            });
        }

        /// <summary>Slides a wall, or anything else, the whole depth along the direction.</summary>
        static void Move(Sequence into, Transform piece, Vector3 direction, float depth, float seconds)
        {
            if (piece == null) return;

            piece.DOKill(true);
            into.Join(piece.DOMove(piece.position + direction * depth, seconds).SetEase(Ease.OutCubic));
        }

        /// <summary>
        /// Lengthens a wall that runs along the direction by the depth, at the end the direction
        /// points to: its middle moves half as far, and the far end stays put.
        /// </summary>
        static void Extend(Sequence into, Transform wall, Vector3 direction, float depth, float seconds)
        {
            if (wall == null) return;

            wall.DOKill(true);

            // Its length is along whichever of its own X and Z it is longer in.
            Vector3 scale = wall.localScale;
            bool alongZ = Mathf.Abs(scale.z) >= Mathf.Abs(scale.x);
            Vector3 axis = alongZ ? wall.forward : wall.right;

            // A wall across the direction is moved, not lengthened.
            if (Mathf.Abs(Vector3.Dot(axis, direction)) < 0.7f)
            {
                Move(into, wall, direction, depth, seconds);
                return;
            }

            float perUnit = alongZ
                ? (Mathf.Abs(scale.z) > 1e-4f ? wall.lossyScale.z / scale.z : 1f)
                : (Mathf.Abs(scale.x) > 1e-4f ? wall.lossyScale.x / scale.x : 1f);
            float grow = depth / Mathf.Max(1e-4f, Mathf.Abs(perUnit)) * Mathf.Sign(alongZ ? scale.z : scale.x);

            Vector3 target = alongZ ? new Vector3(scale.x, scale.y, scale.z + grow) : new Vector3(scale.x + grow, scale.y, scale.z);

            into.Join(wall.DOMove(wall.position + direction * depth * 0.5f, seconds).SetEase(Ease.OutCubic));
            into.Join(wall.DOScale(target, seconds).SetEase(Ease.OutCubic));
        }

        // --- The kitchen going back -------------------------------------------------------------------

        static PlacementService Grid => GridBuildingSystem3D.Instance != null ? GridBuildingSystem3D.Instance.Placement : null;

        /// <summary>The kitchen: the strip along the back wall's inner face, as deep as the floor's kitchen tiles.</summary>
        Rect KitchenArea()
        {
            Rect inner = MeasureInside();
            float depth = Mathf.Min(floor != null ? floor.KitchenDepth : DefaultKitchenDepth, inner.height, inner.width);
            Vector2 back = restaurant.DirectionOf(RestaurantSide.Back);

            if (back.y > 0.5f) return Rect.MinMaxRect(inner.xMin, inner.yMax - depth, inner.xMax, inner.yMax);
            if (back.y < -0.5f) return Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMax, inner.yMin + depth);
            if (back.x > 0.5f) return Rect.MinMaxRect(inner.xMax - depth, inner.yMin, inner.xMax, inner.yMax);
            return Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMin + depth, inner.yMax);
        }

        /// <summary>
        /// What stands in the kitchen now and goes back with its wall: anything built on the grid
        /// with the middle of its cells in it, the serving pass counters there, and the waiters
        /// whose places are at those counters.
        /// </summary>
        void GatherKitchen()
        {
            kitchenPlaced.Clear();
            kitchenPasses.Clear();
            kitchenWaiters.Clear();

            Rect area = KitchenArea();
            PlacementService grid = Grid;

            if (grid != null)
            {
                foreach (PlacedObject_Done placed in FindObjectsByType<PlacedObject_Done>(FindObjectsSortMode.None))
                {
                    if (placed.isRemoved || !placed.IsPlaced || placed.PlacedObjectTypeSO == null) continue;
                    if (area.Contains(grid.Footprint(placed).center)) kitchenPlaced.Add(placed);
                }
            }

            foreach (ServingPass pass in FindObjectsByType<ServingPass>(FindObjectsSortMode.None))
                if (area.Contains(pass.WorldFootprint.center)) kitchenPasses.Add(pass);

            foreach (Waiter waiter in FindObjectsByType<Waiter>(FindObjectsSortMode.None))
            {
                foreach (ServingPass pass in kitchenPasses)
                {
                    if (RestaurantService.DistanceTo(pass.WorldFootprint, waiter.Home) > WaiterReach) continue;

                    kitchenWaiters.Add(waiter);
                    break;
                }
            }
        }

        /// <summary>The whole cells the stations go back for the back wall's next step.</summary>
        Vector2Int KitchenCells(PlacementService grid)
        {
            Vector2 back = restaurant.DirectionOf(RestaurantSide.Back) * restaurant.Tuning.For(RestaurantSide.Back).depth;
            float cell = grid.Layout.CellSize;
            return new Vector2Int(Mathf.RoundToInt(back.x / cell), Mathf.RoundToInt(back.y / cell));
        }

        /// <summary>True when everything built in the kitchen has free cells to go back onto; the reply when not.</summary>
        bool KitchenCanGoBack(out string reason)
        {
            reason = null;
            PlacementService grid = Grid;
            if (grid == null) return true;

            GatherKitchen();
            if (grid.CanShift(kitchenPlaced, KitchenCells(grid), out PlacedObject_Done blocker)) return true;

            reason = blocker != null
                ? $"{NameOf(blocker)} is in the way of the kitchen going back. Move it first."
                : "The kitchen cannot go back any further: the building ground ends there.";
            return false;
        }

        /// <summary>
        /// Carries the kitchen back with the back wall: the stations change cells at once - nothing
        /// can be built into the ground they are going to - and slide there with the wall; the
        /// counters slide with them and the waiters walk to their new places. False when there is
        /// nothing in the kitchen to carry.
        /// </summary>
        bool MoveKitchen(Sequence into, Vector3 direction, float depth, float seconds)
        {
            GatherKitchen();

            PlacementService grid = Grid;
            if (grid != null && kitchenPlaced.Count > 0)
            {
                var from = new Vector3[kitchenPlaced.Count];
                for (int i = 0; i < kitchenPlaced.Count; i++)
                {
                    PlacementPop.SettleOn(kitchenPlaced[i].transform);
                    kitchenPlaced[i].transform.DOKill(true);
                    from[i] = kitchenPlaced[i].transform.position;
                }

                Vector2Int cells = KitchenCells(grid);
                float along = new Vector2(cells.x, cells.y).magnitude * grid.Layout.CellSize;
                if (Mathf.Abs(along - depth) > 0.01f)
                {
                    Debug.LogWarning($"[RestaurantShell] The back wall moves {depth:0.##} m but the kitchen can only go back " +
                                     $"whole cells ({along:0.##} m). Set the back side's depth to whole metres.", this);
                }

                if (!grid.TryShift(kitchenPlaced, cells))
                    Debug.LogWarning("[RestaurantShell] The kitchen could not go back on the build grid; it stays where it is.", this);

                for (int i = 0; i < kitchenPlaced.Count; i++)
                {
                    Transform station = kitchenPlaced[i].transform;
                    Vector3 to = station.position;
                    if ((to - from[i]).sqrMagnitude < 1e-6f) continue;

                    station.position = from[i];
                    into.Join(station.DOMove(to, seconds).SetEase(Ease.OutCubic));
                }
            }

            foreach (ServingPass pass in kitchenPasses) Move(into, pass.transform, direction, depth, seconds);

            foreach (Waiter waiter in kitchenWaiters)
                waiter.SetHome(waiter.Home + direction * depth, waiter.HomeRotation);

            return kitchenPlaced.Count > 0 || kitchenPasses.Count > 0;
        }

        /// <summary>Tells the routes and the night where the building now stands and where its door is.</summary>
        void Refit()
        {
            PlaceExpandAnchor();

            if (shelter == null) return;

            Rect area = restaurant.Footprint;
            Bounds old = shelter.Footprint;

            float height = old.size.y > 0.01f ? old.size.y : 3f;
            var building = new Bounds(new Vector3(area.center.x, old.center.y, area.center.y),
                                      new Vector3(area.width, height, area.height));
            shelter.Refit(building);
        }

        // --- The line no zombie crosses ------------------------------------------------------

        /// <summary>Dust off the wall where a blow lands, at the height the arms come down.</summary>
        void HandleHit(Vector3 at)
        {
            composition.Effects.Play(hitEffect != null ? hitEffect : buildEffect,
                                     new Vector3(at.x, transform.position.y + 1f, at.z));
        }

        /// <summary>
        /// Gives the nav grid each wall's outline as it stands, so the night's routes go round the
        /// building to the door instead of straight through a side wall. The door is the gap
        /// between the two front pieces, left open. False while there is no grid to tell yet.
        /// </summary>
        bool PutWallsOnGrid()
        {
            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return false;

            wallAreas.Clear();
            foreach (Transform wall in new[] { backWall, leftWall, rightWall, frontLeftWall, frontRightWall })
            {
                if (wall == null) continue;

                Bounds b = BoundsOf(wall);
                wallAreas.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
            }

            nav.Grid.SetStaticWalls(this, wallAreas);
            return true;
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            if (choosing && !current.AllowsBuilding()) EndChoosing("It is getting dark. The expansion will have to wait.");
        }

        // --- Measuring ----------------------------------------------------------------------------

        bool HasWalls() => backWall != null && leftWall != null && rightWall != null &&
                           (frontLeftWall != null || frontRightWall != null);

        /// <summary>The outer faces of the walls, flat.</summary>
        Rect MeasureWalls()
        {
            Bounds all = BoundsOf(backWall);
            foreach (Transform wall in new[] { leftWall, rightWall, frontLeftWall, frontRightWall })
                if (wall != null) all.Encapsulate(BoundsOf(wall));

            return Rect.MinMaxRect(all.min.x, all.min.z, all.max.x, all.max.z);
        }

        /// <summary>
        /// The room between the walls' inner faces, flat. Each wall trims the side it stands on:
        /// runs along X the south or north side, runs along Z the west or east.
        /// </summary>
        Rect MeasureInside()
        {
            Rect outer = MeasureWalls();
            float xMin = outer.xMin, xMax = outer.xMax, zMin = outer.yMin, zMax = outer.yMax;

            foreach (Transform wall in new[] { backWall, leftWall, rightWall, frontLeftWall, frontRightWall })
            {
                if (wall == null) continue;
                Bounds b = BoundsOf(wall);

                if (b.size.x >= b.size.z)
                {
                    if (b.center.z < outer.center.y) zMin = Mathf.Max(zMin, b.max.z);
                    else zMax = Mathf.Min(zMax, b.min.z);
                }
                else
                {
                    if (b.center.x < outer.center.x) xMin = Mathf.Max(xMin, b.max.x);
                    else xMax = Mathf.Min(xMax, b.min.x);
                }
            }

            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        /// <summary>
        /// Lays the floor between the walls as they stand now: the kitchen tiles along the back
        /// wall, the pattern measured from the doorway's middle on the back wall's line - neither of
        /// which moves sideways as the building grows. Also run by the setup tool, in the editor.
        /// </summary>
        public void FitFloor()
        {
            if (floor == null || !HasWalls()) return;

            Rect inner = MeasureInside();
            Vector2 front = FrontDirection();
            Vector2 back = Mathf.Abs(front.x) > Mathf.Abs(front.y)
                ? new Vector2(-Mathf.Sign(front.x), 0f)
                : new Vector2(0f, -Mathf.Sign(front.y));

            // The doorway: the gap between the two front pieces' facing ends.
            Vector2 door = inner.center;
            if (frontLeftWall != null && frontRightWall != null)
            {
                Bounds l = BoundsOf(frontLeftWall), r = BoundsOf(frontRightWall);
                door = new Vector2((Mathf.Min(l.max.x, r.max.x) + Mathf.Max(l.min.x, r.min.x)) * 0.5f,
                                   (Mathf.Min(l.max.z, r.max.z) + Mathf.Max(l.min.z, r.min.z)) * 0.5f);
            }

            Vector2 anchor = back.x != 0f
                ? new Vector2(back.x > 0f ? inner.xMax : inner.xMin, door.y)
                : new Vector2(door.x, back.y > 0f ? inner.yMax : inner.yMin);

            floor.Fit(inner, back, anchor);
        }

        /// <summary>Out through the front wall: from the middle of the building toward it.</summary>
        Vector2 FrontDirection()
        {
            Rect area = MeasureWalls();

            Vector3 front = Vector3.zero;
            int count = 0;
            foreach (Transform wall in new[] { frontLeftWall, frontRightWall })
            {
                if (wall == null) continue;

                front += BoundsOf(wall).center;
                count++;
            }

            front /= Mathf.Max(1, count);
            return new Vector2(front.x - area.center.x, front.z - area.center.y);
        }

        static Bounds BoundsOf(Transform piece)
        {
            if (piece == null) return default;

            Renderer r = piece.GetComponentInChildren<Renderer>();
            if (r != null) return r.bounds;

            Collider c = piece.GetComponentInChildren<Collider>();
            return c != null ? c.bounds : new Bounds(piece.position, Vector3.zero);
        }

        Material HighlightMaterial()
        {
            if (highlightMaterial != null) return highlightMaterial;

            // A scene nobody ran the setup on: the sprite shader is transparent, tinted by _Color,
            // and always in the build - an unlit material made here would be opaque.
            highlightMaterial = new Material(Shader.Find("Sprites/Default")) { name = "ExpandArea (runtime)" };
            return highlightMaterial;
        }

        static Mesh Quad()
        {
            if (quad != null) return quad;

            // Flat on the ground, facing up, a metre square about its middle.
            quad = new Mesh { name = "ExpandAreaQuad" };
            quad.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
            };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            quad.RecalculateBounds();
            return quad;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, Transform back, Transform left, Transform right,
                              Transform frontLeft, Transform frontRight, Transform[] fixtures,
                              NightShelter nightShelter, Material highlight, PooledEffect hit, PooledEffect dust)
        {
            scope = lifetimeScope;
            backWall = back;
            leftWall = left;
            rightWall = right;
            frontLeftWall = frontLeft;
            frontRightWall = frontRight;
            frontFixtures = fixtures;
            shelter = nightShelter;
            highlightMaterial = highlight;
            hitEffect = hit;
            buildEffect = dust;
        }

        /// <summary>Editor wiring, for the setup tool: the floor to keep laid between the walls.</summary>
        public void ConfigureFloor(RestaurantFloor restaurantFloor)
        {
            floor = restaurantFloor;
        }

        void OnDrawGizmosSelected()
        {
            if (restaurant == null || !restaurant.HasFootprint) return;

            Rect area = restaurant.Footprint;
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(area.center.x, 0.5f, area.center.y), new Vector3(area.width, 1f, area.height));
        }
    }
}
