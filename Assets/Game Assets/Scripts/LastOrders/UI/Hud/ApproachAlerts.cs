using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// Where the zombies are coming from: one alert for each side of the compound an attack comes up,
    /// pinned over the threat while it is on screen and held at the screen's edge, its beak pointing
    /// at it, while it is not.
    ///
    /// In the preparation countdown it marks, in amber, the sides the first wave will come from, at
    /// the breach they will come through - time to look there and build there before anything shows.
    /// Between waves it marks the next wave's sides the same way. Once a wave is out it turns red and
    /// follows the front of each side's zombies - the one nearest the diner - until every one of them
    /// is dead or over the wall. A tap on an alert at the edge glides the camera over to it.
    ///
    /// It reads state and decides nothing, like the rest of the HUD. Built by 'Last Orders / UI / Build
    /// Gameplay HUD', or onto a HUD already in the scene by 'Last Orders / UI / Add Approach Alerts'.
    /// </summary>
    [DefaultExecutionOrder(1210)]
    public sealed class ApproachAlerts : MonoBehaviour
    {
        sealed class Side
        {
            public int Lane;
            public ApproachAlertMarker Marker;

            /// <summary>Zombies of the wave under way that have not yet set off up this side.</summary>
            public int ToCome;

            // Counted afresh every frame.
            public int Coming;
            public int Out;
            public Vector3 Front;
            public float FrontDistance;

            public bool Shown;
            public bool Attack;
            public bool Pinned;
            public int Count;
            public Vector3 Target;
            public Vector2 Look;
            public Vector2 Goal;

            // As drawn: eased towards the above.
            public bool Placed;
            public float Angle;
            public float Scale = 1f;
            public float Flash;
            public bool PunchDue;
            public string Text;
        }

        [SerializeField] GameLifetimeScope scope;
        [SerializeField] HudSkin skin;

        [Tooltip("The alert each side's is copied from. Kept switched off.")]
        [SerializeField] ApproachAlertMarker template;

        [Tooltip("HUD cards the alerts keep out from under, so one held at the edge of the screen is never " +
                 "hidden behind them.")]
        public RectTransform[] keepClear = new RectTransform[0];

        [Tooltip("How far in from each edge of the screen the middle of an alert is held: left, bottom, right, " +
                 "top. Enough that the tip of the beak stays on screen.")]
        [SerializeField] Vector4 edgeInset = new Vector4(84f, 84f, 84f, 84f);

        [Tooltip("How far the middle of an alert keeps from the HUD cards.")]
        [SerializeField] float clearance = 66f;

        [Tooltip("Closest two alerts may sit, middle to middle.")]
        [SerializeField] float spacing = 136f;

        [Tooltip("Metres above the ground an alert points at: about a zombie's head.")]
        [SerializeField] float headHeight = 2f;

        [Tooltip("Pixels from the middle of an alert to the tip of its beak.")]
        [SerializeField] float beakReach = 74f;

        [Tooltip("Pixels between the middle of the badge and the near edge of its tag.")]
        [SerializeField] float tagGap = 50f;

        [Tooltip("Size of an alert pinned over zombies on screen, against one held at the edge.")]
        [Range(0.4f, 1f)] [SerializeField] float pinScale = 0.8f;

        GameComposition composition;
        EnemySpawner spawner;
        CameraPanZoomRig rig;
        RectTransform layer;

        readonly List<Side> sides = new List<Side>();
        readonly List<Side> shown = new List<Side>();
        readonly List<Rect> blocked = new List<Rect>();
        readonly Vector3[] corners = new Vector3[4];

        static readonly string CountColour = "#" + ColorUtility.ToHtmlStringRGB(HudPalette.Gold);

        // --- Lifetime ------------------------------------------------------------------------------

        void Start()
        {
            layer = (RectTransform)transform;
            if (template != null) template.gameObject.SetActive(false);

            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            // Not an error: without the composition root nothing attacks, so there is nothing to point at.
            if (composition == null) return;

            spawner = FindFirstObjectByType<EnemySpawner>();
            rig = FindFirstObjectByType<CameraPanZoomRig>();

            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Night.OnSpawnRequested += HandleSpawnRequested;
            composition.Night.OnGroupIncoming += HandleGroupIncoming;
            if (composition.Encounters != null) composition.Encounters.OnWaveStarted += HandleWaveStarted;
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Night.OnSpawnRequested -= HandleSpawnRequested;
            composition.Night.OnGroupIncoming -= HandleGroupIncoming;
            if (composition.Encounters != null) composition.Encounters.OnWaveStarted -= HandleWaveStarted;
        }

        // --- What is coming, and up which side --------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            if (current == DayPhase.Night) return;

            for (int i = 0; i < sides.Count; i++) sides[i].ToCome = 0;
        }

        void HandleWaveStarted(int index, int total, EncounterWave wave)
        {
            for (int i = 0; i < sides.Count; i++) sides[i].ToCome = 0;
            for (int i = 0; i < wave.Spawns.Count; i++) Get(wave.Spawns[i].Lane).ToCome++;
        }

        void HandleSpawnRequested(EnemyDefinition definition, int lane)
        {
            Side side = Find(lane);
            if (side != null && side.ToCome > 0) side.ToCome--;
        }

        void HandleGroupIncoming(WaveGroup group)
        {
            Side side = Get(group.Lane);

            // The old timed night has no waves to count from, so its groups are counted as they are announced.
            if (composition.Night.WaveCount == 0) side.ToCome += group.Count;

            side.PunchDue = true;
        }

        // --- Every frame ---------------------------------------------------------------------------

        void Update()
        {
            if (composition == null) return;

            float dt = Time.unscaledDeltaTime;
            Camera view = Camera.main;

            if (spawner == null || view == null)
            {
                for (int i = 0; i < sides.Count; i++) Hide(sides[i], dt);
                return;
            }

            Count();
            Place(view, dt);
        }

        /// <summary>Which sides are shown, how many each, and what each points at.</summary>
        void Count()
        {
            for (int i = 0; i < sides.Count; i++)
            {
                Side s = sides[i];
                s.Coming = 0;
                s.Out = 0;
                s.FrontDistance = float.MaxValue;
            }

            DayPhase phase = composition.Machine.CurrentPhase;
            bool night = phase == DayPhase.Night;

            if (phase == DayPhase.Dusk)
            {
                Preview(FirstGroups());
            }
            else if (night)
            {
                Preview(NextWaveGroups());
                CountOut();
            }

            for (int i = 0; i < sides.Count; i++)
            {
                Side s = sides[i];
                s.Attack = night && (s.Out > 0 || s.ToCome > 0);
                s.Shown = s.Attack || s.Coming > 0;
                if (!s.Shown) continue;

                s.Count = s.Attack ? s.Out + s.ToCome : s.Coming;
                s.Target = s.Out > 0 ? s.Front : spawner.LanePoint(s.Lane);
            }
        }

        /// <summary>In the countdown: the first wave's groups - or the whole night's, on the old timed night.</summary>
        List<WaveGroup> FirstGroups()
        {
            WavePlan plan = composition.Context.TonightsWave;
            if (plan == null) return null;

            return plan.IsEncounter ? plan.Waves[0].Groups : plan.Groups;
        }

        /// <summary>In the breather between two waves: the next one's groups.</summary>
        List<WaveGroup> NextWaveGroups()
        {
            NightState night = composition.Night;
            if (night.WaveCount == 0 || night.BreakSecondsRemaining <= 0f) return null;

            WavePlan plan = composition.Context.TonightsWave;
            int next = night.WaveIndex + 1;
            return plan != null && next < plan.Waves.Count ? plan.Waves[next].Groups : null;
        }

        void Preview(List<WaveGroup> groups)
        {
            if (groups == null) return;

            for (int i = 0; i < groups.Count; i++) Get(groups[i].Lane).Coming += groups[i].Count;
        }

        /// <summary>The zombies still on their way in, by the side they came up, and the front of each side's.</summary>
        void CountOut()
        {
            IReadOnlyList<EnemyAgent> live = composition.Night.LiveEnemies;
            Vector3 home = DinerCentre();

            for (int i = 0; i < live.Count; i++)
            {
                EnemyAgent agent = live[i];
                if (agent == null || !agent.IsAlive || agent.IsRetreating) continue;
                if (!spawner.TryGetLane(agent, out int lane)) continue;

                // Over the wall it is no longer coming - it is here, and the health bar says so.
                Vector3 at = agent.Position;
                if (composition.Compound != null && composition.Compound.IsInside(at)) continue;

                Side side = Get(lane);
                side.Out++;

                float dx = at.x - home.x, dz = at.z - home.z;
                float distance = dx * dx + dz * dz;
                if (distance < side.FrontDistance)
                {
                    side.FrontDistance = distance;
                    side.Front = at;
                }
            }
        }

        Vector3 DinerCentre()
        {
            var restaurant = composition.Restaurant;
            if (restaurant != null && restaurant.HasFootprint)
                return new Vector3(restaurant.Footprint.center.x, 0f, restaurant.Footprint.center.y);

            return rig != null ? rig.Pivot : Vector3.zero;
        }

        // --- Placing ---------------------------------------------------------------------------------

        void Place(Camera view, float dt)
        {
            Rect area = layer.rect;
            area.xMin += edgeInset.x;
            area.yMin += edgeInset.y;
            area.xMax -= edgeInset.z;
            area.yMax -= edgeInset.w;

            CollectBlocked();
            shown.Clear();

            for (int i = 0; i < sides.Count; i++)
            {
                Side s = sides[i];
                if (!s.Shown || area.width <= 0f || area.height <= 0f)
                {
                    Hide(s, dt);
                    continue;
                }

                Aim(s, view, area);
                shown.Add(s);
            }

            Separate(area);

            for (int i = 0; i < shown.Count; i++)
            {
                Side s = shown[i];
                s.Goal = KeepClear(s.Goal, area);
                Draw(s, dt);
            }
        }

        /// <summary>
        /// Where the threat is on screen, and so where its alert goes: pinned just over it when it is in
        /// view, otherwise held at the edge on the line from the middle of the screen out to it.
        /// </summary>
        void Aim(Side s, Camera view, Rect area)
        {
            Vector3 world = s.Target + Vector3.up * headHeight;
            Vector3 screen = view.WorldToScreenPoint(world);

            // Behind the camera a projection comes out mirrored. Walking the point back along the ground
            // towards the middle of the view until it is in front keeps the direction to it true.
            bool behind = screen.z < view.nearClipPlane;
            if (behind)
            {
                Vector3 middle = GroundUnderMiddle(view, world);
                for (int i = 0; i < 10 && screen.z < view.nearClipPlane + 1f; i++)
                {
                    world = Vector3.Lerp(middle, world, 0.5f);
                    screen = view.WorldToScreenPoint(world);
                }
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 point);

            Vector2 centre = area.center;
            Vector2 away = point - centre;
            if (behind) away = (away.sqrMagnitude > 1e-4f ? away.normalized : Vector2.down) * 100000f;

            s.Look = centre + away;
            s.Pinned = !behind && area.Contains(point);

            if (s.Pinned)
            {
                // Just over them, the beak's tip on their heads.
                s.Goal = Clamp(point + Vector2.up * (beakReach * pinScale), area);
                return;
            }

            float sx = Mathf.Abs(away.x) > 1e-3f ? area.width * 0.5f / Mathf.Abs(away.x) : float.MaxValue;
            float sy = Mathf.Abs(away.y) > 1e-3f ? area.height * 0.5f / Mathf.Abs(away.y) : float.MaxValue;
            s.Goal = centre + away * Mathf.Min(1f, Mathf.Min(sx, sy));
        }

        static Vector3 GroundUnderMiddle(Camera view, Vector3 fallback)
        {
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var ground = new Plane(Vector3.up, new Vector3(0f, fallback.y, 0f));
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : fallback;
        }

        /// <summary>Pushes alerts that crowd each other apart, so two sides' alerts never stack.</summary>
        void Separate(Rect area)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < shown.Count; i++)
                {
                    for (int j = i + 1; j < shown.Count; j++)
                    {
                        Side a = shown[i], b = shown[j];
                        float wanted = spacing * Mathf.Max(a.Pinned ? pinScale : 1f, b.Pinned ? pinScale : 1f);

                        Vector2 apart = b.Goal - a.Goal;
                        float distance = apart.magnitude;
                        if (distance >= wanted) continue;

                        Vector2 push = (distance > 0.01f ? apart / distance : Vector2.right) * ((wanted - distance) * 0.5f);
                        a.Goal = Clamp(a.Goal - push, area);
                        b.Goal = Clamp(b.Goal + push, area);
                    }
                }
            }
        }

        /// <summary>The HUD's cards in this layer's space, grown by the clearance an alert keeps from them.</summary>
        void CollectBlocked()
        {
            blocked.Clear();

            for (int i = 0; i < keepClear.Length; i++)
            {
                RectTransform card = keepClear[i];
                if (card == null || !card.gameObject.activeInHierarchy) continue;

                card.GetWorldCorners(corners);
                Vector2 a = layer.InverseTransformPoint(corners[0]);
                Vector2 b = layer.InverseTransformPoint(corners[2]);

                blocked.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - clearance, Mathf.Min(a.y, b.y) - clearance,
                                            Mathf.Max(a.x, b.x) + clearance, Mathf.Max(a.y, b.y) + clearance));
            }
        }

        /// <summary>Slides a point out from under any card, the shortest way that stays on screen.</summary>
        Vector2 KeepClear(Vector2 point, Rect area)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                int under = BlockedAt(point);
                if (under < 0) return point;

                Rect card = blocked[under];
                Vector2 best = point;
                float bestDistance = float.MaxValue;
                bool bestIsClear = false;

                void Try(Vector2 candidate)
                {
                    candidate = Clamp(candidate, area);
                    if (card.Contains(candidate)) return;

                    bool clear = BlockedAt(candidate) < 0;
                    float distance = (candidate - point).sqrMagnitude;

                    // A spot clear of every card beats a nearer one that lands under another.
                    if ((clear && !bestIsClear) || (clear == bestIsClear && distance < bestDistance))
                    {
                        best = candidate;
                        bestDistance = distance;
                        bestIsClear = clear;
                    }
                }

                Try(new Vector2(card.xMin - 1f, point.y));
                Try(new Vector2(card.xMax + 1f, point.y));
                Try(new Vector2(point.x, card.yMin - 1f));
                Try(new Vector2(point.x, card.yMax + 1f));

                if (bestDistance == float.MaxValue) return point;
                point = best;
            }

            return point;
        }

        int BlockedAt(Vector2 point)
        {
            for (int i = 0; i < blocked.Count; i++)
                if (blocked[i].Contains(point)) return i;

            return -1;
        }

        static Vector2 Clamp(Vector2 point, Rect area) =>
            new Vector2(Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));

        // --- Drawing ---------------------------------------------------------------------------------

        void Draw(Side s, float dt)
        {
            ApproachAlertMarker m = s.Marker != null ? s.Marker : s.Marker = Spawn(s);
            if (m == null) return;

            var rt = (RectTransform)m.transform;
            float goalScale = s.Pinned ? pinScale : 1f;

            if (!s.Placed || !m.gameObject.activeSelf)
            {
                m.gameObject.SetActive(true);
                rt.anchoredPosition = s.Goal;
                s.Scale = goalScale;
                s.Angle = AngleTo(s.Look - s.Goal);
                s.Placed = true;
                s.PunchDue = true;
                m.group.alpha = 0f;
            }

            float ease = 1f - Mathf.Exp(-12f * dt);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, s.Goal, ease);
            s.Scale = Mathf.Lerp(s.Scale, goalScale, ease);
            s.Angle = Mathf.LerpAngle(s.Angle, AngleTo(s.Look - rt.anchoredPosition), 1f - Mathf.Exp(-16f * dt));

            m.group.alpha = Mathf.MoveTowards(m.group.alpha, 1f, dt * 5f);
            m.body.localScale = new Vector3(s.Scale, s.Scale, 1f);
            m.body.anchoredPosition = new Vector2(0f, s.Pinned ? Mathf.Sin(Time.unscaledTime * 4f) * 4f : 0f);
            m.pointer.localEulerAngles = new Vector3(0f, 0f, s.Angle);

            if (s.PunchDue)
            {
                s.PunchDue = false;
                s.Flash = 1f;
                if (m.motion != null) m.motion.Punch(0.35f);
            }

            s.Flash = Mathf.MoveTowards(s.Flash, 0f, dt * 1.6f);

            Color tone = s.Attack ? HudPalette.Red : HudPalette.Gold;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (s.Attack ? 6f : 3.5f));
            m.glow.color = tone.WithAlpha(0.3f + 0.25f * pulse + 0.4f * s.Flash);
            m.glow.rectTransform.localScale = Vector3.one * (1f + 0.08f * pulse + 0.25f * s.Flash);
            m.pointerImage.color = tone;
            m.glyph.color = s.Attack ? Color.white : HudPalette.Ink;
            m.badge.raycastTarget = !s.Pinned;

            if (skin != null)
            {
                Sprite face = skin[s.Attack ? HudSprites.RoundRed : HudSprites.RoundGold];
                if (m.badge.sprite != face) m.badge.sprite = face;
            }

            string text = Label(s);
            if (text != s.Text)
            {
                s.Text = text;
                m.label.text = text;
                m.tag.sizeDelta = new Vector2(m.label.GetPreferredValues(text).x + 30f, m.tag.sizeDelta.y);
            }

            // The tag on the far side of the badge from the beak, so the beak is never hidden under it.
            float beak = (s.Angle + 90f) * Mathf.Deg2Rad;
            Vector2 half = m.tag.sizeDelta * 0.5f;
            m.tag.anchoredPosition = new Vector2(-Mathf.Cos(beak) * (tagGap + half.x), -Mathf.Sin(beak) * (tagGap + half.y));
        }

        /// <summary>The beak is drawn pointing up; this turns it to point along <paramref name="toward"/>.</summary>
        static float AngleTo(Vector2 toward) =>
            toward.sqrMagnitude < 4f ? 180f : Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg - 90f;

        string Label(Side s)
        {
            string count = $"<color={CountColour}>{s.Count}</color>";
            string[] names = composition.Encounters != null ? composition.Encounters.LaneNames : null;
            bool named = names != null && s.Lane >= 0 && s.Lane < names.Length && !string.IsNullOrEmpty(names[s.Lane]);

            return named ? $"{count}  {names[s.Lane].ToUpperInvariant()}" : count;
        }

        static void Hide(Side s, float dt)
        {
            ApproachAlertMarker m = s.Marker;
            if (m == null || !m.gameObject.activeSelf) return;

            m.badge.raycastTarget = false;
            m.group.alpha = Mathf.MoveTowards(m.group.alpha, 0f, dt * 5f);
            if (m.group.alpha > 0f) return;

            m.gameObject.SetActive(false);
            s.Placed = false;
        }

        ApproachAlertMarker Spawn(Side s)
        {
            if (template == null) return null;

            ApproachAlertMarker m = Instantiate(template, template.transform.parent);
            m.name = $"Alert_Lane{s.Lane}";
            if (m.button != null) m.button.onClick.AddListener(() => Focus(s));
            return m;
        }

        /// <summary>Glides the camera over to the threat a tapped alert points at.</summary>
        void Focus(Side s)
        {
            if (rig != null) rig.PanTo(s.Target);
        }

        Side Find(int lane)
        {
            for (int i = 0; i < sides.Count; i++)
                if (sides[i].Lane == lane) return sides[i];

            return null;
        }

        Side Get(int lane)
        {
            Side side = Find(lane);
            if (side != null) return side;

            side = new Side { Lane = lane };
            sides.Add(side);
            return side;
        }

        public void Configure(GameLifetimeScope lifetimeScope, HudSkin hudSkin, ApproachAlertMarker marker)
        {
            scope = lifetimeScope;
            skin = hudSkin;
            template = marker;
        }
    }
}
