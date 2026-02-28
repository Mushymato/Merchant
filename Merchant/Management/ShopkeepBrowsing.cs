using System.Diagnostics.CodeAnalysis;
using System.Text;
using Merchant.Misc;
using Merchant.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace Merchant.Management;

public record ForSaleTarget(
    Item Thing,
    Furniture Table,
    List<(Point, int)> BrowseAround,
    bool FromHeldChest = false,
    int Idx = -1
)
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
            if (FromHeldChest)
            {
                if (Table.heldObject.Value is Chest chest)
                {
                    for (int i = 0; i < chest.Items.Count; i++)
                    {
                        Item item = chest.Items[i];
                        if (item != null)
                        {
                            item.onDetachedFromParent();
                            if (item is SObject obj)
                                obj.performRemoveAction();
                            chest.Items[i] = null;
                        }
                    }
                }
            }
            else
            {
                if (Table.heldObject.Value is SObject obj)
                {
                    obj.onDetachedFromParent();
                    obj.performRemoveAction();
                    Table.heldObject.Value = null;
                }
            }
        }
    }

    public static bool CanOfferForSale(Item? item, Farmer player)
    {
        return item != null && item is not Furniture && item.sellToStorePrice(player.UniqueMultiplayerID) > 0;
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
            if (furniture.IsTable() && furniture.heldObject.Value != null)
            {
                AddForSaleTable(
                    player,
                    reachableTiles,
                    forSaleTables,
                    furniture,
                    ref standingDecorCount,
                    ref unreachableTableCount
                );
            }
            else if (furniture.furniture_type.Value == 12)
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
        int customerCount = Math.Min(
            32,
            Math.Min(forSaleTables.Count, 4 + (ModEntry.ProgressData?.Logs.Count ?? 0) / 2)
        );
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

    public static void AddForSaleTable(
        Farmer player,
        List<Point> reachableTiles,
        List<ForSaleTarget> forSaleTables,
        Furniture furniture,
        ref int standingDecorCount,
        ref int unreachableTableCount
    )
    {
        List<(Point, int)> browseAround = FormBrowseAround(furniture, reachableTiles).ToList();
        bool unreachable = !browseAround.Any();
        bool gotForSale = false;

        if (furniture.heldObject.Value is Chest chest)
        {
            // FF branch
            for (int i = 0; i < chest.Items.Count; i++)
            {
                Item item = chest.Items[i];
                if (ForSaleTarget.CanOfferForSale(item, player))
                {
                    if (unreachable)
                    {
                        unreachableTableCount++;
                        return;
                    }
                    gotForSale = true;
                    forSaleTables.Add(new(item, furniture, browseAround, true, i));
                }
            }
        }
        else if (ForSaleTarget.CanOfferForSale(furniture.heldObject.Value, player))
        {
            if (unreachable)
            {
                unreachableTableCount++;
                standingDecorCount++;
                return;
            }
            gotForSale = true;
            forSaleTables.Add(new(furniture.heldObject.Value, furniture, browseAround));
        }
        if (!gotForSale)
        {
            standingDecorCount++;
        }
    }

    private static IEnumerable<(Point, int)> FormBrowseAround(Furniture furniture, List<Point> reachable)
    {
        Rectangle boundingBox = new(
            (int)furniture.TileLocation.X,
            (int)furniture.TileLocation.Y,
            furniture.getTilesWide(),
            furniture.getTilesHigh()
        );
        Point pnt;

        for (int i = 0; i < boundingBox.Width; i++)
        {
            int x = boundingBox.Left + i;
            // X
            // .
            pnt = new(x, boundingBox.Bottom);
            if (reachable.Contains(pnt))
                yield return new(pnt, 0);
            // .
            // X
            pnt = new(x, boundingBox.Top - 1);
            if (reachable.Contains(pnt))
                yield return new(pnt, 2);
        }
        for (int i = 0; i < boundingBox.Height; i++)
        {
            int y = boundingBox.Top + i;
            // .X
            pnt = new(boundingBox.Left - 1, y);
            if (reachable.Contains(pnt))
                yield return new(pnt, 1);
            // X.
            pnt = new(boundingBox.Right, y);
            if (reachable.Contains(pnt))
                yield return new(pnt, 3);
        }
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
    private const int newCustomerCDMin = 2000;
    private const int newCustomerCDMax = 4000;

    internal bool AboutToFinish => state.Next == BrowsingState.Finished;

    private readonly Queue<CustomerActor> waitingActors = ShuffleWaitingActors(CustomerActors);
    private readonly List<CustomerActor> dispatchedActors = [];

    public static Queue<CustomerActor> ShuffleWaitingActors(List<CustomerActor> customerActors)
    {
        customerActors = customerActors.ToList();
        Random.Shared.ShuffleInPlace(customerActors);
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
            state.Current = BrowsingState.Finished;
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
            state.Current = BrowsingState.Finished;
            return true;
        }

        if (state.Current == BrowsingState.NewCustomer)
        {
            state.Current = BrowsingState.Waiting;
            AddNewCustomer();
            if (waitingActors.Any())
            {
                state.SetNext(BrowsingState.NewCustomer, Random.Shared.Next(newCustomerCDMin, newCustomerCDMax));
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
                sb.Append($"\n- {forSale.Thing.DisplayName} ({forSale.Sold})");
            }
        }

        if (sales.Count <= 0)
            return null;

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
