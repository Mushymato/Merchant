using System.Diagnostics.CodeAnalysis;
using System.Text;
using Merchant.Misc;
using Merchant.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace Merchant.Management;

public record ForSaleTarget(Item Thing, Furniture Table, List<(Point, int)> BrowseAround, int Idx = -1)
{
    public CustomerActor? HeldBy { get; set; } = null;
    public SoldRecord? Sold
    {
        get => field;
        set
        {
            if (field != null)
                return;
            field = value;
            HeldBy = null;
            ModEntry.tableShim.TryRemoveItemFromTable(Table, Thing);
        }
    }

    public static bool CanOfferForSale(Item? item, Farmer player)
    {
        return item != null
            && item is SObject obj
            && !obj.bigCraftable.Value
            && item is not Furniture
            && item.sellToStorePrice(player.UniqueMultiplayerID) > 0;
    }
}

public sealed record ShopkeepBrowsing(
    GameLocation Location,
    Farmer Player,
    Point EntryPoint,
    List<Point> ReachableTiles,
    List<CustomerActor> CustomerActors,
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
        // location
        if (location is FarmHouse)
        {
            failReason = I18n.FailReason_IsFarmHouse();
            return false;
        }
        if (location.ParentBuilding == null)
        {
            failReason = I18n.FailReason_NotFarmBuilding();
            return false;
        }
        if (location.Map == null)
        {
            failReason = I18n.FailReason_InvalidMap();
            return false;
        }
        int mapTileCount = location.Map.DisplayWidth / 64 * (location.Map.DisplayHeight / 64);
        if (mapTileCount == 0)
        {
            failReason = I18n.FailReason_InvalidMap();
            return false;
        }
        // tile accessibility
        if (location.warps.Count < 1)
        {
            failReason = I18n.FailReason_NoWarpsIn();
            return false;
        }
        Warp firstWarp = location.warps[0];
        Point entryPoint = new(firstWarp.X, firstWarp.Y - 1);
        List<Point> reachableTiles = Topology.TileStandableBFS(location, entryPoint);
        if (!reachableTiles.Any())
        {
            failReason = I18n.FailReason_NoReachable();
            return false;
        }

        // shop layout and for sale items
        int floorDecorCount = 0;
        int standingDecorCount = 0;
        int unreachableTableCount = 0;
        List<ForSaleTarget> forSaleTables = [];
        foreach (Furniture furniture in location.furniture)
        {
            if (
                ModEntry.tableShim.TryGetForSaleTargets(
                    furniture,
                    player,
                    reachableTiles,
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
                        forSaleTables.Add(forSale);
                    }
                }
            }
            else if (furniture.furniture_type.Value == Furniture.rug)
            {
                floorDecorCount += furniture.getTilesHigh() * furniture.getTilesWide();
            }
            else
            {
                standingDecorCount++;
            }
        }
        if (forSaleTables.Count == 0)
        {
            failReason = I18n.FailReason_NoItemsForSale();
            return false;
        }

        floorDecorCount += location.terrainFeatures.Count();

        // customers
        List<CustomerActor> customerActors = [];
        int customerCount = Math.Min(32, Math.Min(forSaleTables.Count, 4 + (ModEntry.ProgressData?.Logs.Count ?? 0)));
        foreach (FriendEntry sourceFriend in ModEntry.FriendEntries.PickCustomerNPCs(customerCount))
        {
            customerActors.Add(new CustomerActor(sourceFriend, entryPoint));
        }

        ShopBonusStats bonusStats = new(
            standingDecorCount,
            forSaleTables.Count,
            floorDecorCount,
            mapTileCount,
            unreachableTableCount
        );

        browsing = new(location, player, entryPoint, reachableTiles, customerActors, forSaleTables, bonusStats);
        return true;
    }
    #endregion

    #region browsing loop
    internal enum BrowsingState
    {
        Waiting,
        NewCustomer,
        Finished,
    }

    private readonly StateManager<BrowsingState> state = new(BrowsingState.NewCustomer);
    private readonly int newCustomerCooldown = GetNewCustomerCooldown(Location);

    private static int GetNewCustomerCooldown(GameLocation location)
    {
        // TODO: formalize these into data assets
        if (location.IsRainingHere())
            return 4000;
        if (location.IsWinterHere() && (Game1.dayOfMonth >= 22 || Game1.dayOfMonth < 25))
            return 1000;
        return 2000;
    }

    internal bool HaggleEnabled = true;

    private readonly Queue<CustomerActor> waitingActors = ShuffleWaitingActors(CustomerActors);
    private readonly List<CustomerActor> dispatchedActors = [];

    public static Queue<CustomerActor> ShuffleWaitingActors(List<CustomerActor> customerActors)
    {
        customerActors = customerActors.ToList();
        Random.Shared.ShuffleInPlace(customerActors);
        ModEntry.Log($"Queued {customerActors.Count} actors");
        return new(customerActors);
    }

    public bool Update(GameTime time, ref ShopkeepHaggle? haggling)
    {
        state.Update(time);
        if (state.Current == BrowsingState.Finished)
        {
            return true;
        }

        if (waitingActors.Count == 0 && dispatchedActors.All(actor => actor.IsLeavingOrFinished))
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
            if (waitingActors.Any())
            {
                state.SetNext(BrowsingState.NewCustomer, newCustomerCooldown + Random.Shared.Next(1000));
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
                actor.update(time, Location);
        }
    }

    private void AddNewCustomer()
    {
        if (!waitingActors.TryDequeue(out CustomerActor? nextActor))
        {
            return;
        }
        ModEntry.Log($"AddNewCustomer: {nextActor.Name}");
        nextActor.HaggleEnabled = HaggleEnabled;
        dispatchedActors.Add(nextActor);
        nextActor.currentLocation = Location;
        nextActor.setTileLocation(EntryPoint.ToVector2());
        Game1.playSound(AssetManager.DoorbellCue, 1100 + (int)(300 * Random.Shared.NextSingle()));
    }

    internal void Cleanup()
    {
        waitingActors.Clear();
        dispatchedActors.Clear();
    }

    internal SessionReportMenu? Finalize()
    {
        List<SoldRecord> sales = [];
        StringBuilder sb = new("===== SOLD =====");

        ulong totalEarnings = 0;
        foreach (ForSaleTarget forSale in ForSaleTargets)
        {
            if (forSale.Sold != null)
            {
                sales.Add(forSale.Sold);
                totalEarnings += forSale.Sold.Price;
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
                sb.Append($"\n- {forSale.Thing.DisplayName} {forSale.Sold}");
            }
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
        ModEntry.ProgressData?.SaveShopkeepSession(newLog, totalEarnings);
        return SessionReportMenu.Make(newLog);
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
