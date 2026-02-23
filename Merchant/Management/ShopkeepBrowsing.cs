using Merchant.Misc;
using Merchant.Models;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace Merchant.Management;

public record ForSaleTarget(Item Thing, Furniture Table, List<(Point, int)> BrowseAround, bool FromHeldChest)
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
}

public sealed record ShopkeepBrowsing(
    GameLocation Location,
    Farmer Player,
    Point EntryPoint,
    List<Point> ReachableTiles,
    List<CustomerActor> CustomerActors,
    List<ForSaleTarget> ForSaleTargets,
    float ShopDecorBonus
)
{
    #region make
    public static ShopkeepBrowsing? Make(GameLocation location, Farmer player)
    {
        // tile accessibility
        if (location.warps.Count < 1)
        {
            ModEntry.Log($"Location {location.NameOrUniqueName} has no warps in", LogLevel.Error);
            return null;
        }
        Warp firstWarp = location.warps[0];
        Point entryPoint = new(firstWarp.X, firstWarp.Y - 1);
        List<Point> reachableTiles = Topology.TileStandableBFS(location, entryPoint);
        if (!reachableTiles.Any())
        {
            ModEntry.Log($"Location {location.NameOrUniqueName} has no reachable tiles", LogLevel.Error);
            return null;
        }

        // shop layout and for sale items
        int floorDecorCount = 0;
        int standingDecorCount = 0;
        List<ForSaleTarget> forSaleTables = [];
        foreach (Furniture furniture in location.furniture)
        {
            if (furniture.heldObject.Value != null)
            {
                List<(Point, int)> browseAround = FormBrowseAround(furniture, reachableTiles).ToList();
                if (!browseAround.Any())
                    continue;

                if (furniture.heldObject.Value is Chest chest)
                {
                    foreach (Item item in chest.Items)
                    {
                        if (item != null && item.sellToStorePrice(player.UniqueMultiplayerID) > 0)
                        {
                            forSaleTables.Add(new(item, furniture, browseAround, true));
                        }
                    }
                }
                else if (furniture.heldObject.Value.sellToStorePrice(player.UniqueMultiplayerID) > 0)
                {
                    forSaleTables.Add(new(furniture.heldObject.Value, furniture, browseAround, false));
                }
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
        standingDecorCount += Math.Max(location.objects.Count() - 1, 0);
        floorDecorCount += location.terrainFeatures.Count();

        // customers
        List<CustomerActor> customerActors = [];
        foreach (NPC sourceNPC in NPCLookup.PickCustomerNPCs(player, forSaleTables.Count))
        {
            customerActors.Add(new CustomerActor(sourceNPC, location, player, entryPoint));
        }

        float shopDecorBonus = 0;
        int tableCount = forSaleTables.Count;
        if (tableCount > 0)
        {
            float decorBonus = 0.5f * standingDecorCount / tableCount;
            ModEntry.LogDebug($"DecorBonus: {standingDecorCount} / {tableCount} = {decorBonus}");
            shopDecorBonus += MathF.Min(decorBonus, 0.6f);
        }
        if (location.Map != null)
        {
            int mapTileCount = location.Map.DisplayWidth / 64 * (location.Map.DisplayHeight / 64);
            float rugBonus = 0.5f * floorDecorCount / mapTileCount;
            ModEntry.LogDebug($"RugBonus: {floorDecorCount} / {mapTileCount} {rugBonus}");
            shopDecorBonus += Math.Min(rugBonus, 0.4f);
        }

        return new(location, player, entryPoint, reachableTiles, customerActors, forSaleTables, shopDecorBonus);
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

        if (waitingActors.Count == 0 && dispatchedActors.All(actor => actor.IsLeaving))
        {
            state.Current = BrowsingState.Finished;
            return true;
        }

        if (state.Current == BrowsingState.NewCustomer)
        {
            state.Current = BrowsingState.Waiting;
            if (AddNewCustomer())
            {
                state.SetNext(BrowsingState.NewCustomer, Random.Shared.Next(newCustomerCDMin, newCustomerCDMax));
            }
            else if (state.Next != BrowsingState.Finished)
            {
                state.SetNext(BrowsingState.Finished, 30000);
                return false;
            }
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
            state.Current = BrowsingState.Finished;
            return true;
        }

        foreach (CustomerActor actor in dispatchedActors)
        {
            actor.UpdateBuyTarget(availableForSale, availableForSaleHeld, out ForSaleTarget? hagglingForSaleTarget);
            if (haggling == null && hagglingForSaleTarget != null)
            {
                haggling = ShopkeepHaggle.Make(Player, actor, hagglingForSaleTarget, ShopDecorBonus);
            }
        }

        return false;
    }

    private bool AddNewCustomer()
    {
        if (!waitingActors.TryDequeue(out CustomerActor? nextActor))
        {
            return false;
        }
        nextActor.currentLocation = Location;
        nextActor.setTileLocation(EntryPoint.ToVector2());
        ModEntry.LogDebug($"AddNewCustomer {nextActor.Name}");
        Location.characters.Add(nextActor);
        dispatchedActors.Add(nextActor);
        return true;
    }

    internal void FinalizeAndCleanup()
    {
        List<SoldRecord> sales = [];
        ModEntry.Log("===== SOLD =====", LogLevel.Info);

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
                ModEntry.Log($"- {forSale.Thing.DisplayName} ({forSale.Sold})", LogLevel.Info);
            }
        }
        Player.Money = Player.Money + (int)totalEarnings;
        Game1.dayTimeMoneyBox.gotGoldCoin((int)totalEarnings);

        ModEntry.ProgressData!.SaveShopkeepSession(sales, false, totalEarnings);

        waitingActors.Clear();
        dispatchedActors.Clear();
        Location.characters.RemoveWhere(actor => actor is CustomerActor);
        state.Current = BrowsingState.Finished;
    }
    #endregion
}
