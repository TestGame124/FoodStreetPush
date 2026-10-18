using DG.Tweening;
using FoodStreet.Interaction;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Interaction;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// The abandoned security hut, and the noise maker found in it: an old site siren on a tripod
    /// with a car battery to run it. The player sets it down somewhere out past the walls in
    /// daylight - in front of the guns is the idea - and sounds it in an attack, when every zombie
    /// within earshot turns off its trail and makes for it. It wails for a few seconds, or until
    /// the crowd round it has battered it quiet.
    ///
    /// The rules are the noise maker service's. This draws it: the device where it was set down,
    /// the ring of its earshot, the beacon spinning and the sound rolling out while it sounds,
    /// its health while it is being hit, and the button over it during an attack.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(DiscoverySite))]
    public sealed class SecurityHut : MonoBehaviour, IDiscoveryFeature
    {
        [Tooltip("The noise maker itself, moved to wherever it is set down.")]
        [SerializeField] Transform device;

        [Tooltip("Where it is set down when the hut is found, if nobody has put it anywhere yet.")]
        [SerializeField] Transform defaultSpot;

        [Tooltip("Where the buttons and the health bar float over the device.")]
        [SerializeField] Transform deviceMarker;

        [Tooltip("The ring on the ground showing how far it is heard, scaled to the radius.")]
        [SerializeField] Transform earshot;

        [Tooltip("The siren's beacon, spun about its local Y while it sounds.")]
        [SerializeField] Transform beacon;

        [Tooltip("Rings of sound rolling out from the horns while it sounds.")]
        [SerializeField] Transform[] soundRings;

        [Tooltip("The hut's own beacon light, lit while it is sounding.")]
        [SerializeField] GameObject alarmLight;

        [SerializeField] Sprite promptIcon;
        [SerializeField] Sprite alarmIcon;

        [SerializeField] Color brokenColour = new Color(1f, 0.45f, 0.35f);

        DiscoverySite site;
        NoiseMakerService service;
        ActionPromptView alarm;
        HealthBarView health;
        bool wired;
        bool placing;
        float earshotUntil;

        public bool WantsPrompt => service != null && !service.IsSounding && !placing;

        public Sprite PromptIcon => promptIcon;

        void Awake()
        {
            site = GetComponent<DiscoverySite>();
            if (device != null) device.gameObject.SetActive(false);
            if (alarmLight != null) alarmLight.SetActive(false);
            ShowRings(false);
        }

        public void OnFound(bool announce)
        {
            if (wired || site.Composition == null) return;

            service = site.Composition.NoiseMaker;
            service.OnPlaced += HandlePlaced;
            service.OnSounded += HandleSounded;
            service.OnSilenced += HandleSilenced;
            service.OnStruck += HandleStruck;
            wired = true;

            // Ready to use the moment it is found: standing outside the walls where the setup put it.
            if (!service.IsPlaced && defaultSpot != null) service.TryPlace(defaultSpot.position, out _);
            if (service.IsPlaced) HandlePlaced(service.Position);
        }

        void OnDestroy()
        {
            if (!wired) return;

            service.OnPlaced -= HandlePlaced;
            service.OnSounded -= HandleSounded;
            service.OnSilenced -= HandleSilenced;
            service.OnStruck -= HandleStruck;
            HideAlarm();
            HideHealth();
        }

        void Update()
        {
            if (!wired) return;

            if (placing) ReadPlacement();

            DrawAlarm();

            if (service.IsSounding)
            {
                if (beacon != null) beacon.Rotate(0f, 540f * Time.deltaTime, 0f, Space.Self);
                RollRings();
            }

            if (earshot != null)
            {
                bool show = placing || service.IsSounding || Time.time < earshotUntil;
                if (earshot.gameObject.activeSelf != show) earshot.gameObject.SetActive(show && service.IsPlaced);
            }
        }

        // --- The hut -------------------------------------------------------------------------------

        public string OnSiteTapped()
        {
            if (service == null) return "The noise maker is not connected to the game.";

            bool opened = BuildChoicePopup.Open(site.Composition, this, Describe, choice =>
            {
                BuildChoicePopup.Close(site.Composition, this);
                if (choice == 0) TapFeedback.Report(BeginPlacing());
                else if (choice == 1) TapFeedback.Report(Sound());
            });

            return opened ? null : BeginPlacing();
        }

        BuildChoiceOffer Describe()
        {
            NoiseMakerTuning tuning = site.Composition.Balance.discovery?.noiseMaker ?? new NoiseMakerTuning();
            var offer = new BuildChoiceOffer
            {
                Title = "Noise maker",
                Description = "An old site siren. Set it down out past the walls; sound it in an attack and every zombie near it comes for it.",
                Status = !service.IsPlaced ? "Not set down yet"
                    : service.IsSounding ? $"Sounding - {Mathf.CeilToInt(service.SecondsLeft)}s left"
                    : service.UsesLeft > 0 ? $"Set down and charged: {service.UsesLeft} use this attack"
                    : "Set down. Charges up when the next attack is called.",
            };

            offer.Details.Add($"Heard {tuning.radius:0} m away, for {tuning.soundSeconds:0} seconds");
            offer.Details.Add("Put it where your defenses can reach the crowd");
            offer.Details.Add("Zombies round it batter it quiet - a big crowd ends it early");

            offer.Options.Add(new BuildChoice(service.IsPlaced ? "MOVE IT" : "SET IT DOWN", service.CanMove(out _), null));
            offer.Options.Add(new BuildChoice("SOUND IT", service.CanSound(out _), null));
            return offer;
        }

        // --- Setting it down -----------------------------------------------------------------------

        string BeginPlacing()
        {
            if (!service.CanMove(out string reason)) return reason;

            placing = true;
            earshotUntil = Time.time + 4f;
            return "Tap open ground outside the walls to set the noise maker down.";
        }

        /// <summary>
        /// The next tap on open ground is where it goes. Read ahead of the world's own tap handling,
        /// and kept from it, so setting it down never also opens whatever stands under the finger.
        /// </summary>
        void ReadPlacement()
        {
            if (!PointerInput.Released || PointerInput.Consumed || PointerInput.GestureUsedMultiTouch) return;
            if (PointerInput.PressStartedOverUI || PointerInput.OverUI) return;
            if (PointerInput.DragPixels > Mathf.Min(Screen.width, Screen.height) * 0.02f) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Ray ray = camera.ScreenPointToRay(PointerInput.Position);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;

            PointerInput.Consume();
            Vector3 at = ray.GetPoint(distance);

            if (service.TryPlace(at, out string message))
            {
                placing = false;
                earshotUntil = Time.time + 3f;
            }

            TapFeedback.Report(message);
        }

        void HandlePlaced(Vector3 at)
        {
            if (device == null) return;

            device.gameObject.SetActive(true);
            device.position = at;
            if (earshot != null) earshot.localScale = new Vector3(service.Radius, 1f, service.Radius);

            device.DOKill(true);
            device.localScale = Vector3.one * 0.2f;
            device.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        // --- Sounding it ---------------------------------------------------------------------------

        string Sound()
        {
            service.TrySound(out string message);
            return message;
        }

        void HandleSounded()
        {
            if (alarmLight != null) alarmLight.SetActive(true);
            ShowRings(true);
            HideAlarm();

            if (device != null)
            {
                device.DOKill(true);
                device.DOShakeScale(0.4f, 0.12f, 14);
            }

            if (deviceMarker != null) health = site.Composition.WorldUi.ShowHealthBar(deviceMarker);
            if (health != null) health.Set(1f, 1f, 1f, false);
        }

        void HandleStruck(float fraction)
        {
            if (health != null && health.IsLive) health.Set(fraction, fraction, 1f, fraction < 0.35f);
            if (device != null) device.DOPunchScale(Vector3.one * 0.06f, 0.15f, 8);
        }

        void HandleSilenced(bool broken)
        {
            if (alarmLight != null) alarmLight.SetActive(false);
            ShowRings(false);
            HideHealth();

            if (broken && deviceMarker != null)
                site.Composition.WorldUi.ShowFloatingText(deviceMarker.position + Vector3.up * 0.5f, "Smashed quiet!", brokenColour);
        }

        void DrawAlarm()
        {
            bool wanted = service.CanSound(out _);
            if (!wanted)
            {
                HideAlarm();
                return;
            }

            if (alarm != null && alarm.IsLive) return;

            WorldUiCatalog catalog = site.Composition.WorldUi.Catalog;
            ActionPromptView prefab = catalog != null ? catalog.alarmPrompt : null;
            if (prefab == null || deviceMarker == null) return;

            alarm = site.Composition.WorldUi.ShowButton(prefab, deviceMarker, Vector3.zero, alarmIcon, () => TapFeedback.Report(Sound()));
        }

        void HideAlarm()
        {
            if (alarm != null && alarm.IsLive && alarm.Anchor == deviceMarker) alarm.Release();
            alarm = null;
        }

        void HideHealth()
        {
            if (health != null && health.IsLive && health.Anchor == deviceMarker) health.Release();
            health = null;
        }

        void ShowRings(bool show)
        {
            if (soundRings == null) return;
            foreach (Transform ring in soundRings)
                if (ring != null) ring.gameObject.SetActive(show);
        }

        /// <summary>Each ring grows out from the horns and fades, one after another, round and round.</summary>
        void RollRings()
        {
            if (soundRings == null || soundRings.Length == 0) return;

            for (int i = 0; i < soundRings.Length; i++)
            {
                Transform ring = soundRings[i];
                if (ring == null) continue;

                float t = Mathf.Repeat(Time.time * 0.9f + i / (float)soundRings.Length, 1f);
                ring.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.4f, t);
            }
        }

        /// <summary>A tap on the device itself: sound it if it can be, otherwise say what it is.</summary>
        public string DeviceTapped()
        {
            if (service == null) return null;
            if (service.CanSound(out _)) return Sound();

            earshotUntil = Time.time + 3f;
            service.CanSound(out string reason);
            return reason;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Transform siren, Transform start, Transform marker, Transform ring, Transform spinner, Transform[] waves,
                              GameObject light, Sprite icon, Sprite soundIcon)
        {
            device = siren;
            defaultSpot = start;
            deviceMarker = marker;
            earshot = ring;
            beacon = spinner;
            soundRings = waves;
            alarmLight = light;
            promptIcon = icon;
            alarmIcon = soundIcon;
        }
    }
}
