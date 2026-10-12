using System.Collections.Generic;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.Lighting
{
    /// <summary>
    /// A light that comes on at night: a lamp's pool on the ground, or a lit area - the ground
    /// inside the compound, the restaurant's room.
    ///
    /// Not a Unity light. Nothing here is drawn or lit per frame: <see cref="NightLights"/> paints
    /// every one of these into the sun's cookie once, and from then on the sun carries them to
    /// every surface it already lights - the ground, the walls, the furniture, the people - for the
    /// one cookie lookup those shaders make anyway. A real point light would cost a pass per lit
    /// object on mobile, and the level's ground and floor are single meshes that URP would light
    /// with one of them at most.
    ///
    /// Lamps add up where their pools overlap. Areas marked as fill do not: where two lit areas
    /// meet, the brighter one wins, so the joins between claimed land and the compound don't show
    /// as a bright seam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightLight : MonoBehaviour
    {
        public enum Shape
        {
            /// <summary>A soft round pool under the object, like a lamp's.</summary>
            Round,

            /// <summary>A lit rectangle along the object's X and Z, fading out past its edge.</summary>
            Area,
        }

        [SerializeField] Shape shape = Shape.Round;

        [Tooltip("The light's colour. Warm for lamps, nearly white for the yard.")]
        [SerializeField] Color colour = new Color(1f, 0.78f, 0.48f);

        [Tooltip("How bright, where it is brightest. 1 is about a clear day's sun on flat ground.")]
        [SerializeField, Min(0f)] float intensity = 0.8f;

        [Tooltip("Round: metres from the middle of the pool to where the light has gone.")]
        [SerializeField, Min(0.1f)] float radius = 6f;

        [Tooltip("Area: metres across along the object's own X and Z. Unused when the area is fitted.")]
        [SerializeField] Vector2 size = new Vector2(10f, 10f);

        [Tooltip("Area: metres the light takes to fade out past the edge - light spilling over a wall.")]
        [SerializeField, Min(0.05f)] float edge = 2f;

        [Tooltip("Area: lit as one with the other fill areas, the brightest winning where they overlap, " +
                 "rather than adding to them.")]
        [SerializeField] bool fill = true;

        [Header("Ceiling lights")]
        [Tooltip("Area: metres between the ceiling lights pooling on the floor. 0 for an even light.")]
        [SerializeField, Min(0f)] float poolSpacing;

        [Tooltip("Area: how much brighter under a ceiling light than between them.")]
        [SerializeField, Range(0f, 1f)] float poolStrength = 0.35f;

        [Header("Fitting")]
        [Tooltip("Area: laid over this renderer's footprint instead of the size above, and only lit " +
                 "while it is shown. Re-measured as it changes - the restaurant's floor grows.")]
        [SerializeField] Renderer fitTo;

        /// <summary>A compound zone on the same object: the area is the zone's, lit only while it
        /// counts as inside, so claimed land lights up the night it is claimed.</summary>
        CompoundZone zone;
        bool zoneLooked;

        static readonly List<NightLight> active = new List<NightLight>();

        /// <summary>Every light switched on in the scene, whether or not it is dark yet.</summary>
        public static IReadOnlyList<NightLight> Active => active;

        public Shape Kind => shape;

        CompoundZone Zone
        {
            get
            {
                if (!zoneLooked)
                {
                    zone = GetComponent<CompoundZone>();
                    zoneLooked = true;
                }

                return zone;
            }
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        void OnDisable() => active.Remove(this);

        /// <summary>
        /// Where the light falls now and how bright, or false when it gives no light at the moment:
        /// its zone isn't claimed, the renderer it fits is hidden, it is turned all the way down.
        /// </summary>
        public bool TryGetFootprint(out Footprint footprint)
        {
            footprint = default;
            if (intensity <= 0f) return false;

            Color light = colour * intensity;

            if (shape == Shape.Round)
            {
                Vector3 at = transform.position;
                footprint = Footprint.Round(new Vector2(at.x, at.z), radius, light);
                return true;
            }

            if (Zone != null)
            {
                if (!zone.IsPresent) return false;
                footprint = Footprint.Area(zone.Area.center, Vector2.right, zone.Area.size * 0.5f, edge, light, fill,
                                           poolSpacing, poolStrength);
                return true;
            }

            if (fitTo != null)
            {
                if (!fitTo.enabled || !fitTo.gameObject.activeInHierarchy) return false;

                Bounds bounds = fitTo.bounds;
                footprint = Footprint.Area(new Vector2(bounds.center.x, bounds.center.z), Vector2.right,
                                           new Vector2(bounds.extents.x, bounds.extents.z), edge, light, fill,
                                           poolSpacing, poolStrength);
                return true;
            }

            Vector3 centre = transform.position;
            Vector3 right = transform.right;
            var axis = new Vector2(right.x, right.z);
            axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector2.right;

            Vector3 scale = transform.lossyScale;
            var half = new Vector2(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.z)) * 0.5f;
            footprint = Footprint.Area(new Vector2(centre.x, centre.z), axis, half, edge, light, fill, poolSpacing, poolStrength);
            return true;
        }

        /// <summary>A lamp: a soft round pool of light.</summary>
        public void ConfigureLamp(Color lightColour, float brightness, float reach)
        {
            shape = Shape.Round;
            colour = lightColour;
            intensity = brightness;
            radius = reach;
            fitTo = null;
        }

        /// <summary>A lit area: its own size, a compound zone on the same object, or fitted over a renderer.</summary>
        public void ConfigureArea(Color lightColour, float brightness, float fadeMetres, bool isFill, Renderer fitOver,
                                  float ceilingSpacing = 0f, float ceilingStrength = 0f)
        {
            shape = Shape.Area;
            colour = lightColour;
            intensity = brightness;
            edge = fadeMetres;
            fill = isFill;
            fitTo = fitOver;
            poolSpacing = ceilingSpacing;
            poolStrength = ceilingStrength;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.8f);
            if (!TryGetFootprint(out Footprint f)) return;

            Rect bounds = f.Bounds;
            Gizmos.DrawWireCube(new Vector3(bounds.center.x, 0.1f, bounds.center.y), new Vector3(bounds.width, 0.2f, bounds.height));
        }
    }

    /// <summary>
    /// Where one night light falls on the ground and how bright: everything the cookie needs to
    /// paint it, in world metres. Compared from one check to the next, so the cookie is painted
    /// again only when a light has actually moved, grown or gone.
    /// </summary>
    public struct Footprint : System.IEquatable<Footprint>
    {
        /// <summary>World X and Z of the middle.</summary>
        public Vector2 Centre;

        /// <summary>The light's own X axis on the ground, as a unit vector in world X and Z.</summary>
        public Vector2 Axis;

        /// <summary>Half the size along its own X and Z. A round pool's radius on both.</summary>
        public Vector2 Half;

        /// <summary>Metres an area takes to fade out past its edge. Unused by a round pool.</summary>
        public float Edge;

        /// <summary>Colour times intensity.</summary>
        public Color Light;

        public bool IsRound;

        /// <summary>True for an area lit as one with the others: the brightest wins, they don't add.</summary>
        public bool IsFill;

        /// <summary>Metres between ceiling lights on an area's floor; 0 for an even light.</summary>
        public float PoolSpacing;

        public float PoolStrength;

        public static Footprint Round(Vector2 centre, float radius, Color light) => new Footprint
        {
            Centre = centre,
            Axis = Vector2.right,
            Half = new Vector2(radius, radius),
            Light = light,
            IsRound = true,
        };

        public static Footprint Area(Vector2 centre, Vector2 axis, Vector2 half, float edge, Color light, bool fill,
                                     float poolSpacing, float poolStrength) => new Footprint
        {
            Centre = centre,
            Axis = axis,
            Half = half,
            Edge = edge,
            Light = light,
            IsFill = fill,
            PoolSpacing = poolSpacing,
            PoolStrength = poolStrength,
        };

        /// <summary>How far the light reaches from the middle along its own X and Z, fade included.</summary>
        public Vector2 Reach => IsRound ? Half : Half + new Vector2(Edge, Edge);

        /// <summary>The world rectangle (X, Z) that holds all of the light.</summary>
        public Rect Bounds
        {
            get
            {
                Vector2 reach = Reach;
                var side = new Vector2(-Axis.y, Axis.x);
                float x = Mathf.Abs(Axis.x) * reach.x + Mathf.Abs(side.x) * reach.y;
                float z = Mathf.Abs(Axis.y) * reach.x + Mathf.Abs(side.y) * reach.y;
                return Rect.MinMaxRect(Centre.x - x, Centre.y - z, Centre.x + x, Centre.y + z);
            }
        }

        /// <summary>
        /// The spacing of the ceiling lights along each of the area's axes: as near the asked-for
        /// spacing as fits a whole number of them wall to wall, so none is cut in half by a wall.
        /// </summary>
        public Vector2 FittedPoolSpacing
        {
            get
            {
                if (PoolSpacing <= 0f) return Vector2.zero;
                float across = Mathf.Max(1f, Mathf.Round(Half.x * 2f / PoolSpacing));
                float along = Mathf.Max(1f, Mathf.Round(Half.y * 2f / PoolSpacing));
                return new Vector2(Half.x * 2f / across, Half.y * 2f / along);
            }
        }

        /// <summary>Equal within a centimetre and a hair of colour: a light nudged by float noise is not "moved".</summary>
        public bool Equals(Footprint other) =>
            IsRound == other.IsRound && IsFill == other.IsFill &&
            Close(Centre, other.Centre) && Close(Axis, other.Axis) && Close(Half, other.Half) &&
            Mathf.Abs(Edge - other.Edge) < 0.01f &&
            Mathf.Abs(PoolSpacing - other.PoolSpacing) < 0.01f && Mathf.Abs(PoolStrength - other.PoolStrength) < 0.001f &&
            Mathf.Abs(Light.r - other.Light.r) < 0.002f && Mathf.Abs(Light.g - other.Light.g) < 0.002f &&
            Mathf.Abs(Light.b - other.Light.b) < 0.002f;

        public override bool Equals(object obj) => obj is Footprint other && Equals(other);

        public override int GetHashCode() => Centre.GetHashCode() ^ Half.GetHashCode();

        static bool Close(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
    }
}
