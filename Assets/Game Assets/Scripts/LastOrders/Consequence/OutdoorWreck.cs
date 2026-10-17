using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace LastOrders.Consequence
{
    /// <summary>
    /// Something out in the yard a zombie can smash: a planting bed, a livestock pen, a turret.
    ///
    /// Smashed, it becomes a wreck - and a wreck is still the player's thing. It keeps its place on
    /// the grid, its level and its upgrades, its crop and its stock; it only stops working, slumps
    /// and darkens, until a repair kit puts every wreck in the yard back together at once. Nobody
    /// re-buys or re-places anything after a bad night.
    ///
    /// Added to the yard's objects at runtime when an attack is locked in, rather than authored
    /// onto every prefab, so whatever the player built since the last attack is included without
    /// anyone having to remember to tag it. Whatever stays the player's own business - the
    /// kitchen, the stores, the walls - is never given one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutdoorWreck : MonoBehaviour, IRaidTarget
    {
        const float TiltDegrees = 8f;
        const float SinkMetres = 0.06f;
        const float SlumpSeconds = 0.35f;

        /// <summary>Multiplied into every material's colour while it is a wreck: scorched and dusty.</summary>
        static readonly Color WreckTint = new Color(0.42f, 0.38f, 0.35f, 1f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        float maxHealth = 45f;
        string displayName;

        Vector3 restPosition;
        Quaternion restRotation;
        Vector3 restScale;
        bool slumped;

        Transform anchor;

        /// <summary>Each renderer's own property blocks from before it was darkened, per material
        /// slot, so putting it back leaves whatever the object itself had set untouched.</summary>
        readonly List<(Renderer renderer, int slot, MaterialPropertyBlock original)> darkened =
            new List<(Renderer, int, MaterialPropertyBlock)>();

        public float Health { get; private set; } = 45f;

        public float MaxHealth => maxHealth;

        public bool IsWrecked { get; private set; }

        /// <summary>Hit tonight, and not yet a wreck. Mended by itself in the morning.</summary>
        public bool IsDamaged => !IsWrecked && Health < maxHealth - 0.01f;

        public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName : name;

        /// <summary>The body of a defense - a turret on the ground - rather than something in the yard.
        /// The horde decides who goes for these; the yard's raiders leave them alone.</summary>
        public bool IsDefense { get; private set; }

        /// <summary>A raider sent into the yard lets a bed go rather than smash a wall to get to it.</summary>
        public bool WorthBreakingThroughFor => false;

        /// <summary>Raised the moment it becomes a wreck.</summary>
        public event Action<OutdoorWreck> OnWrecked;

        /// <summary>Raised when a repair puts it back together.</summary>
        public event Action<OutdoorWreck> OnRestored;

        /// <summary>A blow landed, with where it came from.</summary>
        public event Action<OutdoorWreck, Vector3> OnHit;

        /// <summary>True when the component given has a wreck on it - the one line a bed, a pen or a
        /// turret asks before it does its job.</summary>
        public static bool IsWreckedOn(Component component) =>
            component != null && component.TryGetComponent(out OutdoorWreck wreck) && wreck.IsWrecked;

        public void Configure(float health, string name, bool isDefense = false)
        {
            maxHealth = Mathf.Max(1f, health);
            displayName = name;
            IsDefense = isDefense;

            // Configured as an attack is locked in, when anything merely dented has long since been
            // mended: whole, unless it is still a wreck from an earlier night.
            Health = IsWrecked ? 0f : maxHealth;
        }

        void Awake()
        {
            Health = maxHealth;
        }

        // --- Where it is -------------------------------------------------------------------------

        /// <summary>Its footprint on the ground, from its colliders - or its renderers, with none.</summary>
        public Bounds Footprint
        {
            get
            {
                bool any = false;
                var bounds = new Bounds(transform.position, Vector3.one);

                foreach (Collider collider in GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || collider.isTrigger) continue;
                    if (!any) bounds = collider.bounds;
                    else bounds.Encapsulate(collider.bounds);
                    any = true;
                }

                if (any) return bounds;

                foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                {
                    if (renderer is ParticleSystemRenderer || !renderer.enabled) continue;
                    if (!any) bounds = renderer.bounds;
                    else bounds.Encapsulate(renderer.bounds);
                    any = true;
                }

                return bounds;
            }
        }

        public Vector3 Centre
        {
            get
            {
                Bounds bounds = Footprint;
                return new Vector3(bounds.center.x, transform.position.y, bounds.center.z);
            }
        }

        /// <summary>The nearest point of it to <paramref name="from"/>, flat.</summary>
        public Vector3 ClosestPoint(Vector3 from)
        {
            Bounds bounds = Footprint;
            return new Vector3(Mathf.Clamp(from.x, bounds.min.x, bounds.max.x), from.y,
                               Mathf.Clamp(from.z, bounds.min.z, bounds.max.z));
        }

        public float FlatDistance(Vector3 from)
        {
            Vector3 closest = ClosestPoint(from);
            closest.y = from.y;
            return Vector3.Distance(closest, from);
        }

        /// <summary>Just over the top of it: where the wrench floats.</summary>
        public Transform Anchor
        {
            get
            {
                if (anchor != null) return anchor;

                Bounds bounds = Footprint;
                anchor = new GameObject("WreckAnchor").transform;
                anchor.SetParent(transform, false);
                anchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                return anchor;
            }
        }

        // --- Taking blows ------------------------------------------------------------------------

        /// <summary>A zombie's blow. True when this was the blow that made it a wreck, or it already was one.</summary>
        public bool TakeHit(float damage, Vector3 from)
        {
            if (IsWrecked) return true;
            if (damage <= 0f) return false;

            Health = Mathf.Max(0f, Health - damage);
            OnHit?.Invoke(this, from);

            if (Health > 0f)
            {
                Flinch();
                return false;
            }

            Wreck();
            return true;
        }

        /// <summary>Morning, for something hit but not wrecked: the dents are knocked out overnight.</summary>
        public void Heal()
        {
            if (!IsWrecked) Health = maxHealth;
        }

        public void Wreck()
        {
            if (IsWrecked) return;

            IsWrecked = true;
            Health = 0f;

            Slump();
            Darken();

            OnWrecked?.Invoke(this);
        }

        /// <summary>Back together: whole, standing straight, its own colours, working again.</summary>
        public void Restore()
        {
            if (!IsWrecked)
            {
                Health = maxHealth;
                return;
            }

            IsWrecked = false;
            Health = maxHealth;

            Lighten();
            Unslump();

            OnRestored?.Invoke(this);
        }

        // --- How it looks -------------------------------------------------------------------------

        void Flinch()
        {
            if (slumped || !Application.isPlaying) return;

            DOTween.Complete(this);
            transform.DOPunchScale(Vector3.one * 0.06f, 0.18f, 6, 0.6f).SetId(this).SetTarget(transform);
        }

        /// <summary>Knocked askew and sunk a little into the ground: unmistakably broken from above.</summary>
        void Slump()
        {
            DOTween.Complete(this);

            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            slumped = true;

            // Leans by a stable amount that depends on where it stands, so two wrecks side by side do
            // not lean identically.
            Vector3 at = transform.position;
            float pitch = Mathf.Sin(at.x * 1.7f + at.z * 0.9f);
            float roll = Mathf.Cos(at.x * 0.8f - at.z * 1.3f);
            Quaternion lean = Quaternion.Euler(TiltDegrees * Mathf.Sign(pitch) * (0.6f + 0.4f * Mathf.Abs(pitch)), 0f,
                                               TiltDegrees * 0.7f * Mathf.Sign(roll) * (0.5f + 0.5f * Mathf.Abs(roll)));

            Transform t = transform;
            Vector3 sunk = restPosition + (t.parent != null ? t.parent.InverseTransformVector(Vector3.down * SinkMetres)
                                                            : Vector3.down * SinkMetres);

            if (!Application.isPlaying)
            {
                t.localPosition = sunk;
                t.localRotation = restRotation * lean;
                return;
            }

            t.DOLocalMove(sunk, SlumpSeconds).SetEase(Ease.OutBounce).SetId(this).SetTarget(t);
            t.DOLocalRotateQuaternion(restRotation * lean, SlumpSeconds).SetEase(Ease.OutBack).SetId(this).SetTarget(t);
        }

        void Unslump()
        {
            if (!slumped) return;

            slumped = false;
            Transform t = transform;
            DOTween.Complete(this);

            t.localScale = restScale;

            if (!Application.isPlaying)
            {
                t.localPosition = restPosition;
                t.localRotation = restRotation;
                return;
            }

            t.DOLocalMove(restPosition, SlumpSeconds).SetEase(Ease.OutBack).SetId(this).SetTarget(t);
            t.DOLocalRotateQuaternion(restRotation, SlumpSeconds).SetEase(Ease.OutBack).SetId(this).SetTarget(t);
        }

        void Darken()
        {
            Lighten();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                // Sprites, particles and lines carry their own colours and are not the body of it.
                if (renderer is SpriteRenderer || renderer is ParticleSystemRenderer ||
                    renderer is LineRenderer || renderer is TrailRenderer) continue;

                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    if (material == null) continue;

                    bool hasBase = material.HasProperty(BaseColorId);
                    bool hasColor = material.HasProperty(ColorId);
                    if (!hasBase && !hasColor) continue;

                    var original = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, slot);
                    darkened.Add((renderer, slot, original));

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, slot);

                    if (hasBase) block.SetColor(BaseColorId, Multiply(Current(original, material, BaseColorId), WreckTint));
                    if (hasColor) block.SetColor(ColorId, Multiply(Current(original, material, ColorId), WreckTint));

                    renderer.SetPropertyBlock(block, slot);
                }
            }
        }

        void Lighten()
        {
            for (int i = 0; i < darkened.Count; i++)
            {
                (Renderer renderer, int slot, MaterialPropertyBlock original) = darkened[i];
                if (renderer == null) continue;

                // An empty block overrides nothing: the same as never having had one.
                renderer.SetPropertyBlock(original, slot);
            }

            darkened.Clear();
        }

        static Color Current(MaterialPropertyBlock block, Material material, int id)
        {
            if (!block.isEmpty && block.HasColor(id)) return block.GetColor(id);
            return material.GetColor(id);
        }

        static Color Multiply(Color colour, Color tint) =>
            new Color(colour.r * tint.r, colour.g * tint.g, colour.b * tint.b, colour.a);

        void OnDestroy()
        {
            DOTween.Kill(this);
        }
    }
}
