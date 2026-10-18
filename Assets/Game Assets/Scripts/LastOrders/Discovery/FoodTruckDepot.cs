using System.Collections.Generic;
using DG.Tweening;
using FoodStreet.Visitors;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Interaction;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// The stranded food truck on its pad by the road, put back on its wheels: loaded from the
    /// pass, it pulls out onto the road and away to wherever the player sent it, and comes home
    /// along the same road in time to hand over the takings.
    ///
    /// The truck is a visitor's vehicle in all but paint, so it drives, bounces and puffs smoke the
    /// way the customers' cars do. The trip itself - how long, what it pays - is the food truck
    /// service's; this only keeps the truck where the trip says it should be.
    /// </summary>
    [RequireComponent(typeof(DiscoverySite))]
    public sealed class FoodTruckDepot : MonoBehaviour, IDiscoveryFeature
    {
        [SerializeField] VisitorVehicle truck;

        [Tooltip("Where it stands between trips, facing the way it pulls out.")]
        [SerializeField] Transform pad;

        [Tooltip("From the pad onto the road and west into the fog.")]
        [SerializeField] Transform[] westRoute;

        [Tooltip("From the pad onto the road and east into the fog.")]
        [SerializeField] Transform[] eastRoute;

        [Tooltip("Back from the fog in the west to just short of the pad, coming round so it pulls on facing the way it parks.")]
        [SerializeField] Transform[] westHome;

        [Tooltip("Back from the fog in the east to just short of the pad, the same.")]
        [SerializeField] Transform[] eastHome;

        [Tooltip("The serving hatch's awning, hinged along its top: propped open on the pad, shut on the road.")]
        [SerializeField] Transform awning;

        [Tooltip("Degrees about the hinge's local X that prop it open; positive swings it up and out.")]
        [SerializeField] float awningOpenDegrees = 78f;

        [SerializeField] Sprite promptIcon;

        [SerializeField] Color takingsColour = new Color(1f, 0.86f, 0.3f);

        DiscoverySite site;
        FoodTruckService service;
        Quaternion awningShut;
        bool awningIsOpen;
        bool wired;
        bool comingHome;
        bool away;
        Transform[] route;
        Transform[] home;

        public bool WantsPrompt => service != null && service.State == FoodTruckState.Parked && !comingHome && !away;

        public Sprite PromptIcon => promptIcon;

        void Awake()
        {
            site = GetComponent<DiscoverySite>();
            if (awning != null) awningShut = awning.localRotation;
        }

        void Start()
        {
            // Derelict, it just stands on its pad: nothing drives it until it is found.
            if (truck != null && pad != null)
            {
                truck.transform.SetPositionAndRotation(pad.position, pad.rotation);
                truck.OnDeparted += HandleDeparted;
            }
        }

        public void OnFound(bool announce)
        {
            if (wired || site.Composition == null) return;

            service = site.Composition.FoodTruck;
            service.OnSent += HandleSent;
            service.OnReturned += HandleReturned;
            wired = true;

            SetAwning(true, animate: announce);
        }

        void OnDestroy()
        {
            DOTween.Kill(this);
            if (truck != null) truck.OnDeparted -= HandleDeparted;
            if (!wired) return;

            service.OnSent -= HandleSent;
            service.OnReturned -= HandleReturned;
        }

        void Update()
        {
            if (!wired || truck == null) return;

            // Home in time for the takings: it sets off back once the rest of the trip is about as
            // long as the drive - and never before it has actually gone.
            if (service.State == FoodTruckState.Out && away && !comingHome && service.SecondsLeft <= DriveSeconds(home))
                ComeHome();
        }

        public string OnSiteTapped()
        {
            if (service == null) return "The food truck is not connected to the game.";

            if (service.State == FoodTruckState.Out || comingHome)
            {
                string where = service.Heading != null ? service.Heading.displayName : "the road";
                int left = Mathf.CeilToInt(service.SecondsLeft);
                return comingHome && service.State == FoodTruckState.Parked
                    ? "The truck is on its way back."
                    : $"The truck is out at {where}: back in {left / 60}:{left % 60:00}.";
            }

            WorldUiCatalog catalog = site.Composition.WorldUi.Catalog;
            FoodTruckPopupView prefab = catalog != null ? catalog.foodTruckPopup : null;
            if (prefab == null) return $"{service.Loaded} of {service.Capacity} dishes aboard.";

            FoodTruckPopupView popup = site.Composition.Popups.Show(prefab);
            if (popup == null) return null;

            popup.Bind(this, service, Send);
            return null;
        }

        bool Send(FoodTruckDestination place)
        {
            bool sent = service.TrySend(place, out string message);
            TapFeedback.Report(message);
            return sent;
        }

        // --- Driving -------------------------------------------------------------------------------

        void HandleSent(FoodTruckDestination place)
        {
            if (truck == null) return;

            bool east = place != null && place.eastward;
            route = east ? eastRoute : westRoute;
            home = east ? eastHome : westHome;
            SetAwning(false, animate: true);

            var path = new List<Vector3> { truck.transform.position };
            foreach (Transform point in route)
                if (point != null) path.Add(point.position);

            away = false;
            comingHome = false;

            // The hatch is shut up before it rolls.
            DOVirtual.DelayedCall(0.4f, () =>
            {
                if (truck != null) truck.DriveAway(path, 0);
            }, false).SetTarget(this);
        }

        void HandleDeparted(VisitorVehicle vehicle)
        {
            // Into the fog: out of sight until it is time to come back - at once, if the trip ran out
            // while it was still on its way there.
            away = true;
            truck.gameObject.SetActive(false);

            if (service != null && service.State == FoodTruckState.Parked) ComeHome();
        }

        void ComeHome()
        {
            var path = new List<Vector3>();
            if (home != null)
                foreach (Transform point in home)
                    if (point != null) path.Add(point.position);

            // With no way home authored, the way out driven backwards.
            if (path.Count == 0 && route != null)
                for (int i = route.Length - 1; i >= 0; i--)
                    if (route[i] != null) path.Add(route[i].position);

            if (path.Count == 0) return;
            path.Add(pad.position);

            comingHome = true;
            away = false;

            truck.ResetForPool();
            truck.OnDeparted += HandleDeparted;
            truck.gameObject.SetActive(true);
            truck.OnParked += _ => Arrived();
            truck.DriveAlong(path, pad.forward, 2);
        }

        void Arrived()
        {
            comingHome = false;
            truck.transform.SetPositionAndRotation(pad.position, pad.rotation);
            SetAwning(true, animate: true);
        }

        void HandleReturned(FoodTruckDestination place, int coins, int sold)
        {
            Vector3 at = pad.position + Vector3.up * 3f;
            string where = place != null ? place.displayName : "the road";

            if (coins > 0)
            {
                RewardSource.At(at);
                site.Composition.WorldUi.ShowFloatingText(at, $"+{coins} coins", takingsColour);
            }

            TapFeedback.Report(sold > 0
                ? $"The food truck is back from {where}: {sold} dishes sold for {coins} coins."
                : $"The food truck is back from {where}.");

            // Paid at the trip's end however long the road was; a truck still out of sight sets off home now.
            if (away) ComeHome();
        }

        float DriveSeconds(Transform[] path)
        {
            if (path == null || truck == null || path.Length == 0) return 0f;

            float length = 0f;
            Vector3 previous = path[0] != null ? path[0].position : pad.position;
            foreach (Transform point in path)
            {
                if (point == null) continue;
                length += Vector3.Distance(previous, point.position);
                previous = point.position;
            }

            length += Vector3.Distance(previous, pad.position);

            float speed = truck.Definition != null ? Mathf.Max(1f, truck.Definition.driveSpeed) : 6f;
            return length / speed + 2.5f;
        }

        void SetAwning(bool open, bool animate)
        {
            if (awning == null || open == awningIsOpen) return;

            awningIsOpen = open;
            Quaternion goal = open ? awningShut * Quaternion.Euler(awningOpenDegrees, 0f, 0f) : awningShut;

            awning.DOKill();
            if (animate) awning.DOLocalRotateQuaternion(goal, 0.5f).SetEase(open ? Ease.OutBack : Ease.InOutSine);
            else awning.localRotation = goal;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(VisitorVehicle vehicle, Transform parked, Transform[] westOut, Transform[] eastOut, Transform[] westBack,
                              Transform[] eastBack, Transform hatchAwning, Sprite icon)
        {
            truck = vehicle;
            pad = parked;
            westRoute = westOut;
            eastRoute = eastOut;
            westHome = westBack;
            eastHome = eastBack;
            awning = hatchAwning;
            promptIcon = icon;
        }
    }
}
