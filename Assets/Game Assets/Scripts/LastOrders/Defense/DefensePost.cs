using System;
using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Interaction;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// A defense in the scene: a guard tower today, an automated turret later. Either a site
    /// with nothing built on it yet, or the defense itself.
    ///
    /// Everything about how it looks is authored here and nothing about the rules is. It
    /// registers a <see cref="DefenseUnit"/> with the defense service and draws whatever that
    /// unit says - the guard up on the platform while someone is posted, the aim turning to
    /// follow what is being tracked, a tracer on every shot. An empty site shows a pad and a
    /// build button; buying the defense raises it on the spot, with no worker involved.
    ///
    /// Adding a defense to a level is placing one of these. A new kind of defense is a new
    /// definition asset and a new model under the same component - the parts below are all
    /// optional, so a turret with no crew and a gun head for an aim pivot needs no code. How it
    /// moves can come from a <see cref="DefenseMotionProfile"/>, and anything else that reacts
    /// to a shot - barrels kicking back, spent cases - listens to <see cref="Fired"/>.
    /// </summary>
    public sealed class DefensePost : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] GameLifetimeScope scope;

        [SerializeField] DefenseDefinition definition;

        [Tooltip("Stable identity. Recorded in progression flags, so renaming it forgets the purchase.")]
        [SerializeField] string postId = "defense";

        [Tooltip("Stands built from the start - the tower the restaurant opens with, never charged " +
                 "for. Otherwise this is an empty site the player can buy one for.")]
        [SerializeField] bool startsBuilt;

        [Tooltip("Bought from the shop and set down on the build grid. Its id is made from where it " +
                 "stands, so every copy is its own defense, and it stands down while the shop has it " +
                 "put away - sold, or waiting to be bought again.")]
        [SerializeField] bool placedFromShop;

        [Header("Parts")]
        [Tooltip("The defense itself. Hidden while the site is empty.")]
        [SerializeField] GameObject builtVisuals;

        [Tooltip("What an empty site looks like: the pad the defense will stand on.")]
        [SerializeField] GameObject siteVisuals;

        [Tooltip("Turns to face whatever is being tracked. The guard stands on it; on a turret it " +
                 "is the gun head. Faces its authored forward - the road - when there is nothing.")]
        [SerializeField] Transform aimPivot;

        [Tooltip("Where shots come from.")]
        [SerializeField] Transform muzzle;

        [Tooltip("Whoever stands watch. Shown only while someone is at the post. Leave empty on " +
                 "anything automated.")]
        [SerializeField] GameObject crewFigure;

        [Header("Feedback")]
        [Tooltip("Drawn from the muzzle to the target for a moment on every shot.")]
        [SerializeField] LineRenderer tracer;

        [Tooltip("Shown at the muzzle for as long as the tracer is.")]
        [SerializeField] GameObject muzzleFlash;

        [SerializeField] float tracerSeconds = 0.07f;

        [Tooltip("Optional. Played at the muzzle on every shot.")]
        [SerializeField] PooledEffect muzzleEffect;

        [Tooltip("Optional. How the aim turns, sweeps while nothing is in sight, and kicks on a shot. " +
                 "When set, its turn speed, target height and flash time are used instead of the " +
                 "three below.")]
        [SerializeField] DefenseMotionProfile motion;

        [Tooltip("Degrees per second the aim turns.")]
        [SerializeField] float turnSpeed = 360f;

        [Tooltip("Where on an enemy a shot lands, above its feet.")]
        [SerializeField] float targetHeight = 0.9f;

        [Tooltip("The spotting call-out, over the post.")]
        [SerializeField] Color spottedColour = new Color(1f, 0.45f, 0.3f);

        [SerializeField] Vector3 calloutOffset = new Vector3(0f, 5.4f, 0f);

        [Header("Tapping")]
        [Tooltip("Resized to the built defense or to the empty pad, so what the player can tap " +
                 "is what they can see.")]
        [SerializeField] BoxCollider tapCollider;

        [SerializeField] Vector3 builtColliderSize = new Vector3(2.4f, 5f, 2.4f);
        [SerializeField] Vector3 siteColliderSize = new Vector3(2.6f, 0.6f, 2.6f);

        [Tooltip("Where the build button floats over an empty site.")]
        [SerializeField] Vector3 buildButtonOffset = new Vector3(0f, 1.3f, 0f);

        GameComposition composition;
        DefenseUnit unit;
        ActionPromptView buildButton;
        Quaternion restRotation;
        float tracerRemaining;

        /// <summary>Where this post is in its idle sweep, as a fraction of one, so posts side by
        /// side do not sweep in step.</summary>
        float scanOffset;

        /// <summary>The id it is registered under while the game runs - see <see cref="FreeId"/> -
        /// and the one set on it otherwise.</summary>
        public string PostId => unit != null ? unit.Id : postId;
        public DefenseDefinition Definition => definition;
        public DefenseUnit Unit => unit;
        public DefenseMotionProfile Motion => motion;

        /// <summary>Bought from the shop and set down on the build grid, rather than standing in the level.</summary>
        public bool PlacedFromShop => placedFromShop;

        static readonly List<DefensePost> standing = new List<DefensePost>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStanding() => standing.Clear();

        /// <summary>Every defense in the level right now.</summary>
        public static IReadOnlyList<DefensePost> Standing => standing;

        public string DisplayName => definition != null ? definition.displayName : name;

        /// <summary>
        /// A shot was drawn: from the muzzle, to where it is aimed, and the shot itself - which
        /// says what it was fired at and, for a thrown one, how long it takes to land. Raised
        /// after the tracer and the flash are up and the aim has snapped round, so a part
        /// reacting to it sees the gun pointing at what it hit.
        /// </summary>
        public event Action<Vector3, Vector3, DefenseShot> Fired;

        /// <summary>
        /// Plays a pooled one-shot effect at <paramref name="at"/> through the game's effects pool:
        /// for the parts on a post that have something to show - where a thrown shot lands. Nothing
        /// happens while the post is not connected to the game.
        /// </summary>
        public void PlayEffect(PooledEffect effect, Vector3 at) => composition?.Effects.Play(effect, at);

        float TurnSpeed => motion != null ? motion.turnSpeed : turnSpeed;
        float TargetHeight => motion != null ? motion.targetHeight : targetHeight;
        float FlashSeconds => motion != null ? motion.flashSeconds : tracerSeconds;

        void Awake()
        {
            if (aimPivot != null) restRotation = aimPivot.localRotation;
            if (tracer != null) tracer.enabled = false;
            if (muzzleFlash != null) muzzleFlash.SetActive(false);

            // From the post's place rather than at random, so the same post sweeps the same way
            // every time the level loads.
            Vector3 p = transform.position;
            scanOffset = Mathf.Repeat(p.x * 0.137f + p.z * 0.071f, 1f);
        }

        bool started;

        /// <summary>Taken out of the shop's pool again: back on duty on its first frame, once the shop has put it where it now stands.</summary>
        bool reconnect;

        void Start()
        {
            started = true;
            Connect();
        }

        /// <summary>
        /// A shop's copy taken out of its pool again - bought and set down - goes back on duty. Not at
        /// once: the pool hands it over before the shop moves it, and its id comes from where it stands.
        /// </summary>
        void OnEnable()
        {
            if (!standing.Contains(this)) standing.Add(this);
            if (started && placedFromShop && unit == null) reconnect = true;
        }

        /// <summary>A shop's copy put back in its pool - sold - stands down, so nothing invisible keeps shooting.</summary>
        void OnDisable()
        {
            standing.Remove(this);
            reconnect = false;
            if (placedFromShop) Disconnect();
        }

        void OnDestroy() => Disconnect();

        void Connect()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[DefensePost] {name} found no GameLifetimeScope.", this);
                return;
            }

            if (definition == null)
            {
                Debug.LogError($"[DefensePost] {name} has no defense definition.", this);
                return;
            }

            unit = composition.Defense.Register(definition, FreeId(), transform.position, startsBuilt);
            if (unit == null) return;

            unit.OnFired += HandleFired;
            unit.OnSpotted += HandleSpotted;
            unit.OnChanged += HandleChanged;

            // Whatever it came back as - built, empty, upgraded - show it as it is, with no fanfare.
            Show(animate: false);
        }

        void Disconnect()
        {
            if (unit == null) return;

            unit.OnFired -= HandleFired;
            unit.OnSpotted -= HandleSpotted;
            unit.OnChanged -= HandleChanged;

            HideBuildButton();
            composition?.Defense.Unregister(unit.Id);
            unit = null;
        }

        /// <summary>
        /// The id this post registers under: its own, unless another post already has it - a
        /// copy placed by duplicating one. Then one made from where it stands, so the copy still
        /// works, and keeps what was bought for it while it stays put. Its own id is still the
        /// right fix, and the warning says so. A copy bought from the shop always stands under
        /// where it was set down, with no warning: there are meant to be several.
        /// </summary>
        string FreeId()
        {
            if (!placedFromShop && composition.Defense.Find(postId) == null) return postId;

            Vector3 p = transform.position;
            string at = $"{postId}@{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.z)}";

            string id = at;
            for (int n = 2; composition.Defense.Find(id) != null; n++) id = $"{at}#{n}";

            if (!placedFromShop)
                Debug.LogWarning($"[DefensePost] {name} has the same post id as another defense ('{postId}'), so it " +
                                 $"stands as '{id}'. Give it an id of its own in the Inspector.", this);
            return id;
        }

        void Update()
        {
            if (reconnect)
            {
                reconnect = false;
                Connect();
            }

            if (unit == null) return;

            // Ranges are measured from where the post stands now, so one that has been moved
            // shoots from its new place, not from where the level started it.
            unit.Position = transform.position;

            SyncCrew();
            Aim();
            FadeTracer();
        }

        // --- Drawing the unit -------------------------------------------------------------------

        void Show(bool animate)
        {
            bool built = unit.IsBuilt;

            SetActive(builtVisuals, built);
            SetActive(siteVisuals, !built);

            if (tapCollider != null)
            {
                Vector3 size = built ? builtColliderSize : siteColliderSize;
                tapCollider.size = size;
                tapCollider.center = new Vector3(0f, size.y * 0.5f, 0f);
            }

            if (built) HideBuildButton();
            else ShowBuildButton();

            SyncCrew();

            if (!built || !animate || builtVisuals == null) return;

            // Rises into place rather than blinking in, so the player sees what they bought.
            Transform t = builtVisuals.transform;
            Vector3 scale = t.localScale;

            t.DOKill(true);
            t.localScale = new Vector3(scale.x, scale.y * 0.05f, scale.z);
            t.DOScale(scale, 0.5f).SetEase(Ease.OutBack);
        }

        void SyncCrew()
        {
            if (crewFigure == null) return;

            bool show = unit.IsBuilt && unit.NeedsCrew && unit.Crew != null && unit.CrewOnDuty;
            if (crewFigure.activeSelf != show) crewFigure.SetActive(show);
        }

        void Aim()
        {
            if (aimPivot == null) return;

            Quaternion rest = restRotation;

            // Watching the approach: a defense that can fire and has nothing in sight sweeps
            // either side of its resting direction. One that cannot fire stands at rest.
            if (motion != null && unit.CanFire) rest *= Quaternion.Euler(0f, motion.ScanYaw(Time.time, scanOffset), 0f);

            Quaternion goal = aimPivot.parent != null ? aimPivot.parent.rotation * rest : rest;

            IDefenseTarget target = unit.Target;
            if (target != null && target.IsAlive)
            {
                Vector3 direction = target.Position - aimPivot.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-4f) goal = Quaternion.LookRotation(direction);
            }

            aimPivot.rotation = Quaternion.RotateTowards(aimPivot.rotation, goal, TurnSpeed * Time.deltaTime);
        }

        void HandleFired(DefenseShot shot)
        {
            Vector3 to = shot.HitPoint + Vector3.up * TargetHeight;

            // Snapped round on the shot: the tracer has to leave the barrel pointing at what it
            // hits, even if the turn to face it had not quite finished.
            if (aimPivot != null)
            {
                Vector3 direction = to - aimPivot.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-4f) aimPivot.rotation = Quaternion.LookRotation(direction);
            }

            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 3f;

            // A tracer is a hitscan shot's: something thrown is drawn flying by whatever throws it.
            if (tracer != null && !shot.InFlight)
            {
                tracer.useWorldSpace = true;
                tracer.positionCount = 2;
                tracer.SetPosition(0, from);
                tracer.SetPosition(1, to);
                tracer.enabled = true;
            }

            if (muzzleFlash != null) muzzleFlash.SetActive(true);
            tracerRemaining = FlashSeconds;

            composition.Effects.Play(muzzleEffect, from);
            Fired?.Invoke(from, to, shot);
        }

        void FadeTracer()
        {
            if (tracerRemaining <= 0f) return;

            tracerRemaining -= Time.deltaTime;
            if (tracerRemaining > 0f) return;

            if (tracer != null) tracer.enabled = false;
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        /// <summary>The early warning: the guard has seen something coming before it is in reach.</summary>
        void HandleSpotted(IDefenseTarget target)
        {
            string what = string.IsNullOrEmpty(target.DisplayName) ? "Enemy" : target.DisplayName;
            composition.WorldUi.ShowFloatingText(transform.position + calloutOffset, $"{what} spotted!", spottedColour);
        }

        void HandleChanged() => Show(animate: true);

        // --- Buying and upgrading ------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (unit == null) return $"The {DisplayName} is not connected to the game.";

            // Built and at its last level there is nothing to buy, so a line on the HUD says
            // everything the popup would.
            if (unit.IsBuilt && !unit.HasNextLevel) return composition.Defense.Describe(unit.Id);

            return OpenPopup() ? null : composition.Defense.Describe(unit.Id);
        }

        void ShowBuildButton()
        {
            if (buildButton != null && buildButton.IsLive) return;

            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            ActionPromptView prefab = catalog != null ? catalog.actionPrompt : null;

            buildButton = composition.WorldUi.ShowButton(prefab, transform, buildButtonOffset, null, () => OpenPopup());
        }

        void HideBuildButton()
        {
            if (buildButton != null && buildButton.IsLive && buildButton.Anchor == transform) buildButton.Release();
            buildButton = null;
        }

        /// <summary>Opens the build or the upgrade offer, whichever applies. False when there is no popup to open.</summary>
        bool OpenPopup()
        {
            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            DefensePopupView prefab = catalog != null ? catalog.defensePopup : null;
            if (prefab == null) return false;

            DefensePopupView popup = composition.Popups.Show(prefab);
            if (popup == null) return false;

            popup.Bind(this, Offer, Buy);
            return true;
        }

        DefenseOffer Offer() =>
            unit.IsBuilt ? composition.Defense.GetUpgradeOffer(unit.Id) : composition.Defense.GetBuildOffer(unit.Id);

        void Buy(DefensePayment payment)
        {
            bool done = unit.IsBuilt
                ? composition.Defense.TryUpgrade(unit.Id, payment, out string message)
                : composition.Defense.TryBuild(unit.Id, payment, out message);

            TapFeedback.Report(message);

            // A refusal leaves the popup open with the reason on it.
            if (done && composition.Popups.Current is DefensePopupView open && open.Owner == (object)this)
                composition.Popups.Hide();
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>Editor wiring, for the setup tool. Keeps the fields private to everything else.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, DefenseDefinition defense, string id, bool built)
        {
            scope = lifetimeScope;
            definition = defense;
            postId = id;
            startsBuilt = built;
            placedFromShop = false;
        }

        /// <summary>Editor wiring for a copy the shop sells and the player sets down on the build grid: built as it is bought.</summary>
        public void ConfigureForShop(DefenseDefinition defense, string id)
        {
            scope = null;
            definition = defense;
            postId = id;
            startsBuilt = true;
            placedFromShop = true;
        }

        /// <summary>Editor wiring for the parts, for the prefab generator.</summary>
        public void ConfigureParts(GameObject built, GameObject site, Transform pivot, Transform barrel,
                                   GameObject crew, LineRenderer shot, GameObject flash, BoxCollider tap)
        {
            builtVisuals = built;
            siteVisuals = site;
            aimPivot = pivot;
            muzzle = barrel;
            crewFigure = crew;
            tracer = shot;
            muzzleFlash = flash;
            tapCollider = tap;
        }

        /// <summary>Editor wiring for what plays at the muzzle on every shot, for the prefab generator.</summary>
        public void ConfigureMuzzleEffect(PooledEffect effect) => muzzleEffect = effect;

        /// <summary>Editor wiring for how it moves and how big its tap target is, for the prefab generator.</summary>
        public void ConfigureMotion(DefenseMotionProfile profile, Vector3 builtSize, Vector3 siteSize, Vector3 calloutAt,
                                    Vector3 buildButtonAt)
        {
            motion = profile;
            builtColliderSize = builtSize;
            siteColliderSize = siteSize;
            calloutOffset = calloutAt;
            buildButtonOffset = buildButtonAt;
        }

        void OnDrawGizmosSelected()
        {
            DefenseLevel level = definition != null
                ? definition.Level(unit != null ? unit.LevelIndex : 0)
                : null;

            if (level == null || level.weapon == null) return;

            // Weapon reach in red, sight in amber: the gap between them is the early warning.
            DrawRing(level.weapon.rangeMetres, new Color(1f, 0.3f, 0.25f, 0.9f));
            DrawRing(level.SpotRange, new Color(1f, 0.8f, 0.2f, 0.7f));
        }

        void DrawRing(float radius, Color colour)
        {
            Gizmos.color = colour;

            const int segments = 48;
            Vector3 centre = transform.position + Vector3.up * 0.05f;
            Vector3 previous = centre + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
