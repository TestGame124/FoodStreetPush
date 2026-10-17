using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlantPot : MonoBehaviour, IInteractableObject, ISeedPlot, IHarvestPlot
{
    public enum PotState
    {
        Empty,
        IsBusy,
        Done,
    }

    public PotState currentState;

    public Transform centerPoint;

    private Item activeItem;

    public GameObject plantPreview;

    public float timer;

    public SeedsInventoryUI seedinventoryUI;

    [Tooltip("The growing crop: a plant on each of the seed's spots, each showing the stage reached.")]
    [SerializeField] CropPatch patch;

    [Tooltip("Used only when there is no patch: one sprite showing each of the seed's grow stages in turn.")]
    [SerializeField] CropSprite crop;

    /// <summary>A seed has just gone in: for the planting effects and anything else that wants to know.</summary>
    public event Action<PlantPot> Planted;

    /// <summary>The ripe crop has just been picked.</summary>
    public event Action<PlantPot> Harvested;

    /// <summary>The growing crop was finished on the spot with gems.</summary>
    public event Action<PlantPot> FinishedWithGems;

    [Header("Timer and finishing with gems")]
    [Tooltip("Seconds of growing one gem skips. The price to finish now is rounded up, and never under one gem.")]
    [SerializeField, Min(1f)] float secondsPerGem = 30f;

    [Tooltip("Seconds the card a tap on a growing crop opens stays up: its progress bar, the time left and the gem button.")]
    [SerializeField, Min(0.5f)] float cardSeconds = 6f;

    LastOrders.UI.World.CropCardView cardView;
    LastOrders.UI.World.WorldUiService worldUi;
    float cardUntil = -1f;
    bool cardShown;
    float nextUiLookup;

    /// <summary>Growing now: planted and not yet ripe.</summary>
    public bool IsGrowing => currentState == PotState.IsBusy && activeItem != null;

    /// <summary>Seconds of growing left, or zero when nothing is growing.</summary>
    public float SecondsLeft => IsGrowing ? Mathf.Max(0f, timer) : 0f;

    /// <summary>How far the crop has come, 0 just planted to 1 ripe.</summary>
    public float GrowProgress
    {
        get
        {
            if (currentState == PotState.Done) return 1f;
            if (!IsGrowing) return 0f;

            float total = Mathf.Max(1f, activeItem.itemDataSO.totalTimeInSeconds);
            return Mathf.Clamp01(1f - timer / total);
        }
    }

    /// <summary>Gems that finish the crop now: one for every <see cref="secondsPerGem"/> left, rounded up.</summary>
    public int FinishCost => IsGrowing ? Mathf.Max(1, Mathf.CeilToInt(SecondsLeft / secondsPerGem)) : 0;

    static int Gems => CurrenciesController.GetCurrencyAmount(CurrencyType.Gems);

    public bool CanHarvest => currentState == PotState.Done && !IsWrecked;

    /// <summary>Smashed by zombies in an attack. The crop stays where it was and stops growing, and
    /// nothing is planted or picked until a repair kit puts the bed back together.</summary>
    public bool IsWrecked => LastOrders.Consequence.OutdoorWreck.IsWreckedOn(this);

    /// <summary>Where a tool offered over this pot waits: above the middle of the crop.</summary>
    public Vector3 CropTop => (centerPoint != null ? centerPoint.position : transform.position) + Vector3.up * 0.35f;

    [Header("VFX")]
    [SerializeField] GameObject finishedEffect;

    
    private void OnEnable()
    {
        if(LevelController.Instance)
            LevelController.Instance.zone.RegisterPot(this);
    }

    private void OnDisable()
    {
        if (LevelController.Instance)
            LevelController.Instance.zone.UnRegisterPot(this);

        ReleaseUi();
    }

    private void OnDestroy() => ReleaseUi();
    private void Start()
    {
            LevelController.Instance.zone.RegisterPot(this);

        if (seedinventoryUI == null)
            seedinventoryUI = UIGame.GetUI().seedInventory;

        if(currentState == PotState.Empty)
            seedinventoryUI.AddEmptyPots(this);
    }

    public bool CanPlant(ItemDataSO seed) => currentState == PotState.Empty && !IsWrecked && seed != null && seed.canBePlanted;

    public bool TryPlant(ItemDataSO seed)
    {
        InventorySystem inventory = InventorySystem.instance;
        if (!CanPlant(seed) || inventory == null || inventory.ItemCount(seed.Type) < 1)
            return false;

        // A fresh item rather than the inventory's own: the pot keeps it until the harvest, and the
        // inventory's entry is changed in place or dropped as seeds are spent.
        Plant(new Item(seed, 1));
        return currentState != PotState.Empty;
    }

    public void Plant(Item item, bool growInstantly = false)
    {
        if (currentState != PotState.Empty || item == null || item.itemsAmount < 1)
            return;

        if (!InventorySystem.instance.RemoveItem(item))
            return;

        activeItem = item;

        if(!growInstantly)
            timer = activeItem.itemDataSO.totalTimeInSeconds;

        StartCoroutine(Growing());
        Planted?.Invoke(this);
    }

    IEnumerator Growing()
    {

        currentState = PotState.IsBusy;

        plantPreview.SetActive(true);

        ItemDataSO seed = activeItem.itemDataSO;
        int stageCount = seed.GrowStageCount;
        float growTime = Mathf.Max(1, seed.totalTimeInSeconds);

        // Counted down every frame, so the card's bar moves smoothly; gems can cut it to nothing.
        while (timer > 0f)
        {
            ShowStage(seed, GrowingStage(1f - timer / growTime, stageCount));
            yield return null;

            // A wrecked bed holds its crop where it was until it is repaired.
            if (IsWrecked)
                continue;

            timer -= Time.deltaTime;
        }

        timer = 0f;

        // The ripe crop appears only once it can be picked.
        ShowStage(seed, stageCount - 1);

        finishedEffect.SetActive(true);

        currentState = PotState.Done;

    }

    /// <summary>
    /// The stage to show at <paramref name="progress"/> (0 just planted, 1 grown): the growing time is
    /// shared evenly between every stage but the last, the ripe crop, which waits for the harvest.
    /// </summary>
    static int GrowingStage(float progress, int stageCount)
    {
        if (stageCount <= 1)
            return 0;

        return Mathf.Clamp(Mathf.FloorToInt(progress * (stageCount - 1)), 0, stageCount - 2);
    }

    void ShowStage(ItemDataSO seed, int index)
    {
        Sprite[] stages = seed.growStages ?? Array.Empty<Sprite>();
        Sprite single = stages.Length > 0 ? stages[Mathf.Clamp(index, 0, stages.Length - 1)] : null;

        if (patch != null)
        {
            if (seed.cropLook != null)
                patch.Show(seed.cropLook, index);
            else
                patch.ShowSingle(single);
        }
        else if (crop != null && single != null)
        {
            crop.Show(single);
        }
    }

    

    /// <summary>
    /// Called by Mouse3D once a tap has been confirmed. The router has already ruled out
    /// the UI, camera drags and pinches, so there is nothing left for the pot to re-check.
    /// </summary>
    public bool OnTouch()
    {
        if (IsWrecked)
            return LastOrders.Interaction.TapFeedback.Report("This planting bed was wrecked by zombies. A repair kit puts it back together.");

        switch (currentState)
        {
            case PotState.Empty:
                seedinventoryUI.gameObject.SetActive(true);
                seedinventoryUI.SetPlantingArea(this);
                CameraController.Focus(CameraController.CameraType.Focus ,transform, null,0.7f);
                return true;

            // Ripe: the sickle comes up over the pot, to be swept across every ripe pot. With no
            // sickle in the scene, the tap picks the crop straight away as it always did.
            case PotState.Done:
                HarvestTool tool = HarvestTool.Instance;
                if (tool == null || !tool.Offer(this, CropTop))
                    Harvest();
                return true;

            // Still growing: the card with the progress bar, the time left and the gem button. With
            // no card built, there is nothing to open and the tap is not this pot's.
            default:
                return OpenCard();
        }
    }

    // --- The card, and finishing with gems -------------------------------------------------------

    /// <summary>
    /// Finishes the growing crop now for <see cref="FinishCost"/> gems. False, with why, when the bed
    /// is wrecked, nothing is growing, or there are not enough gems.
    /// </summary>
    public bool TryFinishWithGems(out string message)
    {
        message = null;
        if (!IsGrowing)
            return false;

        string crop = activeItem.itemDataSO.Name;
        if (IsWrecked)
        {
            message = $"This bed was wrecked by zombies - repair it before finishing the {crop}.";
            return false;
        }

        int cost = FinishCost;
        if (Gems < cost || !new LastOrders.Economy.GemWallet().TrySpend(cost))
        {
            message = $"Not enough gems: finishing the {crop} now takes {cost} gem{(cost == 1 ? "" : "s")}, and you have {Gems}.";
            return false;
        }

        // The growing loop sees the clock at nothing on its next frame and ripens the crop as usual.
        timer = 0f;
        cardUntil = -1f;

        LastOrders.Interaction.TapFeedback.Report($"{crop} finished for {cost} gem{(cost == 1 ? "" : "s")}!");
        if (Ui != null) Ui.ShowFloatingText(CropTop + Vector3.up * 0.4f, $"-{cost} Gem{(cost == 1 ? "" : "s")}", new Color(0.55f, 0.85f, 1f));

        FinishedWithGems?.Invoke(this);
        return true;
    }

    bool OpenCard()
    {
        if (Ui == null || Ui.Catalog == null || Ui.Catalog.cropCard == null)
            return false;

        // A card already up stays up for longer; one that has gone makes way for a new one.
        if (cardView == null || !cardView.IsLive || cardView.IsClosing)
        {
            cardView = null;
            cardShown = false;
        }

        cardUntil = Time.time + cardSeconds;
        RefreshUi();
        return true;
    }

    bool FinishFromCard()
    {
        bool done = TryFinishWithGems(out string message);
        if (!done)
            LastOrders.Interaction.TapFeedback.Report(message);
        else
            cardView = null;    // the card plays its own way out

        return done;
    }

    LastOrders.UI.World.WorldUiService Ui
    {
        get
        {
            if (worldUi != null || Time.unscaledTime < nextUiLookup)
                return worldUi;

            // Looked for at most once a second until the game's composition is up.
            nextUiLookup = Time.unscaledTime + 1f;
            var scope = FindFirstObjectByType<LastOrders.Core.GameLifetimeScope>();
            worldUi = scope != null && scope.Composition != null ? scope.Composition.WorldUi : null;
            return worldUi;
        }
    }

    Transform UiAnchor => centerPoint != null ? centerPoint : transform;

    private void Update()
    {
        if (cardView != null || cardUntil > 0f)
            RefreshUi();
    }

    /// <summary>The card over the crop, from a tap until it times out, the player taps elsewhere,
    /// or the crop is ripe. Nothing shows over a growing crop otherwise.</summary>
    void RefreshUi()
    {
        if (cardView != null && (!cardView.IsLive || cardView.IsClosing)) cardView = null;

        LastOrders.UI.World.WorldUiService ui = IsGrowing ? Ui : worldUi;
        if (!IsGrowing || ui == null || Time.time >= cardUntil)
        {
            ReleaseUi();
            return;
        }

        if (cardView == null)
        {
            // Closed by a tap elsewhere, or by the card's own way out: done until the next tap.
            if (cardUntil > 0f && cardShown)
            {
                ReleaseUi();
                return;
            }

            cardView = ui.ShowCropCard(UiAnchor);
            if (cardView == null)
            {
                ReleaseUi();
                return;
            }

            cardView.Bind(FinishFromCard);
            cardShown = true;
        }

        ItemDataSO seed = activeItem.itemDataSO;
        int cost = FinishCost;
        cardView.Show(seed.itemImage, seed.Name, GrowProgress, SecondsLeft, cost, Gems >= cost, IsWrecked);
    }

    void ReleaseUi()
    {
        if (cardView != null && cardView.IsLive && !cardView.IsClosing) cardView.Release();
        cardView = null;
        cardUntil = -1f;
        cardShown = false;
    }

    public bool TryHarvest()
    {
        if (!CanHarvest)
            return false;

        Harvest();
        return true;
    }

    private void Harvest() { 
        InventorySystem inventory = InventorySystem.instance;
        if (activeItem == null || inventory == null || !inventory.CanAdd(activeItem.type))
        {
            LastOrders.Interaction.TapFeedback.Report("Make room in the storehouse before harvesting.");
            return;
        }

        finishedEffect.SetActive(false);
        plantPreview.SetActive(false);
        if (patch != null)
            patch.Clear();
        int generateItems = Mathf.Max(2, activeItem.itemDataSO.harvestYield);

        // Thrown out of the crop together, so they fan out rather than land on each other.
        activeItem.itemDataSO.GenerateItem(ItemController.instance.itemReceiver, activeItem, CropTop, inventory, generateItems);
        LastOrders.Economy.RewardSource.At(CropTop);
        LastOrders.Core.LastOrdersBridge.AddExperience(1);

        currentState= PotState.Empty;
        Harvested?.Invoke(this);
    }

}
