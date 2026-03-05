using System.Diagnostics.CodeAnalysis;
using System.Text;
using Merchant.Misc;
using Merchant.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.SpecialOrders;

namespace Merchant.Management;

public record ForSaleTarget(
    Item Thing,
    Furniture Table,
    List<(Point, int)> BrowseAround,
    ShopkeepThemeBoostData? Boost,
    int Idx = -1
)
{
    public CustomerActor? HeldBy { get; set; } = null;
    public float ThemeBonus { get; set; } = 0f;
    public SoldRecord? Sold
    {
        get => field;
        set
        {
            if (field != null)
                return;
            field = value;
            HeldBy = null;
            ModEntry.tableShim.TryRemoveItemFromTable(Table, Thing, Idx);
        }
    }

    public static bool CanOfferForSale([NotNullWhen(true)] Item? item, Farmer player)
    {
        return item != null && item.canBeShipped() && item.sellToStorePrice(player.UniqueMultiplayerID) > 0;
    }
}

public sealed record ShopkeepBrowsing(
    GameLocation Location,
    Farmer Player,
    Point EntryPoint,
    Queue<CustomerActor> WaitingActors,
    List<ForSaleTarget> ForSaleTargets,
    ShopBonusStats ShopBonus
)
{
    #region make
    public static bool TryMake(
        GameLocation location,
        Farmer player,
        [NotNullWhen(true)] out ShopkeepBrowsing? browsing,
        [NotNullWhen(false)] out string? failReason
    )
    {
        browsing = null;
        failReason = null;
        // map
        if (location.Map == null)
        {
            failReason = I18n.FailReason_InvalidMap();
            return false;
        }
        // location
        List<ShopkeepThemeBoostData>? themeBoostDatas = null;
        if (location.ParentBuilding != null)
        {
            string themeBoostIds = location.ParentBuilding.GetMetadata(AssetManager.Metadata_ShopkeepThemeBoosts);
            if (themeBoostIds != null)
            {
                themeBoostDatas = [];
                foreach (string themeBoost in themeBoostIds.Split(','))
                {
                    if (AssetManager.ShopkeepContexts.Get(themeBoost) is ShopkeepThemeBoostData themeBoostData)
                    {
                        themeBoostDatas.Add(themeBoostData);
                    }
                }
            }

            if (
                location.ParentBuilding.GetMetadata(AssetManager.Metadata_ShopkeepCondition) is string shopkeepCondition
                && !GameStateQuery.CheckConditions(shopkeepCondition, new(location, player, null, null, Random.Shared))
            )
            {
                failReason =
                    location.ParentBuilding.GetMetadata(AssetManager.Metadata_ShopkeepNotAllowedMessage)
                    ?? I18n.FailReason_CantBeShop();
                return false;
            }
        }
        else
        {
            failReason = I18n.FailReason_NotFarmBuilding();
            return false;
        }

        // tile accessibility
        if (!location.TryGetMapPropertyAs(AssetManager.MapProp_EntryPoint, out Point entryPoint))
        {
            if (location.warps.Count < 1)
            {
                failReason = I18n.FailReason_NoWarpsIn();
                return false;
            }
            Warp firstWarp = location.warps[0];
            entryPoint = new(firstWarp.X, firstWarp.Y - 1);
        }
        if (!Topology.IsTileStandable(location, entryPoint))
        {
            failReason = I18n.FailReason_WarpBlocked();
            return false;
        }
        List<Point> reachableTiles = Topology.TileStandableBFS(location, entryPoint);
        if (!reachableTiles.Any())
        {
            failReason = I18n.FailReason_NoReachable();
            return false;
        }

        // shop layout and for sale items
        int floorDecorCount = 0;
        int standingDecorCount = location.objects.Values.Count(SObjectIsDecor);
        int unreachableTableCount = 0;
        List<ForSaleTarget> forSaleTargets = [];
        foreach (Furniture furniture in location.furniture)
        {
            if (
                ModEntry.tableShim.TryGetForSaleTargets(
                    furniture,
                    player,
                    reachableTiles,
                    themeBoostDatas,
                    out List<ForSaleTarget?>? ForSaleTargets
                )
            )
            {
                foreach (ForSaleTarget? forSale in ForSaleTargets)
                {
                    if (forSale == null)
                    {
                        unreachableTableCount++;
                        standingDecorCount++;
                    }
                    else
                    {
                        forSaleTargets.Add(forSale);
                    }
                }
            }
            else if (furniture.furniture_type.Value == Furniture.rug)
            {
                floorDecorCount += furniture.getTilesHigh() * furniture.getTilesWide();
            }
            else
            {
                int furnitureSize = furniture.getTilesHigh() * furniture.getTilesWide();
                standingDecorCount += Math.Clamp(furniture.getTilesHigh() * furniture.getTilesWide() / 2, 1, 4);
            }
        }
        if (forSaleTargets.Count == 0)
        {
            failReason = I18n.FailReason_NoItemsForSale();
            return false;
        }

        floorDecorCount += location.terrainFeatures.Count();

        HashSet<string> excludingSet = location.characters.Select(chara => chara.Name).ToHashSet();
        List<CustomerActor> waitingActors = [];
        int forSaleTargetsCount = forSaleTargets.Count;
        // tourists
        ModEntry.TourismWaves.MakeTouristActors(
            forSaleTargetsCount,
            entryPoint,
            forSaleTargets,
            excludingSet,
            ref waitingActors
        );
        forSaleTargetsCount -= waitingActors.Count;
        // customers
        int customerCount = Math.Min(32, Math.Min(forSaleTargetsCount, 4 + ModEntry.ProgressData.Logs.Count));
        ModEntry.FriendEntries.MakeCustomerActors(
            customerCount,
            entryPoint,
            forSaleTargets,
            excludingSet,
            ref waitingActors
        );
        ModEntry.Log($"Picked {waitingActors.Count} actors");
        Random.Shared.ShuffleInPlace(waitingActors);

        ShopBonusStats bonusStats = new(
            standingDecorCount,
            forSaleTargets.Count,
            floorDecorCount,
            reachableTiles.Count,
            unreachableTableCount,
            themeBoostDatas
        );

        browsing = new(
            location,
            player,
            entryPoint,
            new Queue<CustomerActor>(waitingActors),
            forSaleTargets,
            bonusStats
        );
        return true;
    }

    private static bool SObjectIsDecor(SObject obj)
    {
        return obj.IsScarecrow() || obj is IndoorPot or Fence or MiniJukebox or Mannequin;
    }
    #endregion

    #region browsing loop
    internal enum BrowsingState
    {
        Waiting,
        NewCustomer,
        Finished,
    }

    private readonly StateManager<BrowsingState> state = new(BrowsingState.NewCustomer, nameof(BrowsingState));
    private const int newCustomerCooldown = 2000;

    private readonly List<CustomerActor> dispatchedActors = [];

    public bool Update(GameTime time, ref ShopkeepHaggle? haggling)
    {
        state.Update(time);
        if (state.Current == BrowsingState.Finished)
        {
            return true;
        }

        if (WaitingActors.Count == 0 && dispatchedActors.All(actor => actor.IsLeavingOrFinished && !actor.IsEmoting))
        {
            ModEntry.Log("Browsing finished reason: all actors are leaving");
            state.SetAndLock(BrowsingState.Finished);
            return true;
        }

        List<ForSaleTarget>? availableForSale = null;
        List<ForSaleTarget>? availableForSaleHeld = null;
        foreach (ForSaleTarget forSale in ForSaleTargets)
        {
            if (forSale.Sold == null)
            {
                if (forSale.HeldBy == null)
                {
                    availableForSale ??= [];
                    availableForSale.Add(forSale);
                }
                else
                {
                    availableForSaleHeld ??= [];
                    availableForSaleHeld.Add(forSale);
                }
            }
        }

        if (availableForSale == null && availableForSaleHeld == null)
        {
            ModEntry.Log("Browsing finished reason: all items have been sold");
            state.SetAndLock(BrowsingState.Finished);
            return true;
        }

        if (state.Current == BrowsingState.NewCustomer)
        {
            state.Current = BrowsingState.Waiting;
            AddNewCustomer();
            if (WaitingActors.Any())
            {
                state.SetNext(BrowsingState.NewCustomer, newCustomerCooldown + Random.Shared.Next(2000));
            }
        }

        foreach (CustomerActor actor in dispatchedActors)
        {
            if (actor.IsInvisible)
                continue;
            actor.update(time, Location);
            actor.UpdateBuyTarget(availableForSale, availableForSaleHeld, out ForSaleTarget? hagglingForSaleTarget);
            if (haggling == null && hagglingForSaleTarget != null)
            {
                haggling = ShopkeepHaggle.Make(Player, actor, hagglingForSaleTarget, ShopBonus.TotalBonus);
            }
        }

        return false;
    }

    public void UpdateActorsOnly(GameTime time)
    {
        foreach (CustomerActor actor in dispatchedActors)
        {
            if (!actor.IsInvisible)
                actor.UpdateDuringReporting(time, Location);
        }
    }

    private void AddNewCustomer()
    {
        if (!WaitingActors.TryDequeue(out CustomerActor? nextActor))
        {
            return;
        }
        ModEntry.Log($"AddNewCustomer: '{nextActor.Name}' ({WaitingActors.Count} remaining)");
        dispatchedActors.Add(nextActor);
        nextActor.EnterShop(Location);
        Game1.playSound(AssetManager.DoorbellCue, 1100 + (int)(300 * Random.Shared.NextSingle()));
    }

    internal void Cleanup()
    {
        WaitingActors.Clear();
        dispatchedActors.Clear();
    }

    internal SessionReportMenu? FinalizeAndReport()
    {
        List<SoldRecord> sales = [];
        StringBuilder sb = new("===== SOLD =====");

        ulong totalEarnings = 0;
        foreach (ForSaleTarget forSale in ForSaleTargets)
        {
            if (forSale.Sold == null)
                continue;

            sales.Add(forSale.Sold);
            totalEarnings += forSale.Sold.Price;
            ApplyShippedBehaviors(forSale, (int)forSale.Sold.Price);

            sb.Append($"\n- {forSale.Thing.DisplayName} {forSale.Sold}");
        }

        if (sales.Count <= 0)
        {
            return null;
        }

        ModEntry.Log(sb.ToString(), LogLevel.Info);

        Player.Money = Player.Money + (int)totalEarnings;
        Game1.dayTimeMoneyBox.gotGoldCoin((int)totalEarnings);

        ShopkeepSessionLog newLog = new()
        {
            Shop = Location.NameOrUniqueName,
            IsAutoShopkeep = false,
            Date = Game1.Date.TotalDays,
            Sales = sales,
        };
        ModEntry.ProgressData.SaveShopkeepSession(newLog, totalEarnings);
        return SessionReportMenu.Make(newLog);
    }

    private void ApplyShippedBehaviors(ForSaleTarget forSale, int price)
    {
        Item thing = forSale.Thing;
        Game1.stats.ItemsShipped += (uint)thing.Stack;
        if (thing.Category == -75 || thing.Category == -79)
        {
            Game1.stats.CropsShipped += (uint)thing.Stack;
        }
        if (thing is SObject obj && obj.countsForShippedCollection())
        {
            Player.shippedBasic(obj.ItemId, obj.Stack);
        }
        if (Player.team.specialOrders != null)
        {
            foreach (SpecialOrder specialOrder2 in Player.team.specialOrders)
            {
                specialOrder2.onItemShipped?.Invoke(Player, forSale.Thing, price);
            }
        }
    }

    #endregion

    #region draw
    public void DrawShadows(SpriteBatch b)
    {
        foreach (CustomerActor actor in dispatchedActors)
        {
            actor.DrawShadow(b);
        }
    }

    public void DrawCharacters(SpriteBatch b)
    {
        foreach (CustomerActor actor in dispatchedActors)
        {
            actor.draw(b);
        }
    }

    public void DrawCharacterEmotes(SpriteBatch b)
    {
        foreach (CustomerActor actor in dispatchedActors)
        {
            actor.DrawEmote(b);
        }
    }
    #endregion
}
