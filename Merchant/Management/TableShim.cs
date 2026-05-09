using System.Diagnostics.CodeAnalysis;
using Merchant.Misc;
using Merchant.Models;
using Merchant.ModIntegration;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace Merchant.Management;

public interface ITableShim
{
    bool HasSpaceForItems(Furniture table);
    bool TryGetForSaleTargets(
        Furniture table,
        Farmer player,
        HashSet<Point> reachableTiles,
        List<ShopkeepThemeBoostData>? themeBoostDatas,
        [NotNullWhen(true)] out List<ForSaleTarget?>? forSaleTargets
    );
    bool TryRemoveItemFromTable(Furniture table, Item item, int idx);
    bool TryPlaceItemOnTable(Furniture table, ref Item? item);
}

public sealed class TableShimBase : ITableShim
{
    public bool HasSpaceForItems(Furniture table)
    {
        return table.IsTable() && table.heldObject.Value == null;
    }

    public bool TryGetForSaleTargets(
        Furniture table,
        Farmer player,
        HashSet<Point> reachableTiles,
        List<ShopkeepThemeBoostData>? themeBoostDatas,
        [NotNullWhen(true)] out List<ForSaleTarget?>? forSaleTargets
    )
    {
        forSaleTargets = null;
        if (!table.IsTable() || table.heldObject.Value is not SObject heldObj)
        {
            return false;
        }
        if (!ForSaleTarget.CanOfferForSale(heldObj, player))
        {
            return false;
        }

        Rectangle boundingBox = new(
            (int)table.TileLocation.X,
            (int)table.TileLocation.Y,
            table.getTilesWide(),
            table.getTilesHigh()
        );
        List<(Point, int)> browseAround = Topology.FormBrowseAround(boundingBox, reachableTiles);
        bool unreachable = !browseAround.Any();
        if (unreachable)
        {
            forSaleTargets = [null];
            return true;
        }

        forSaleTargets =
        [
            new(heldObj, table, browseAround, ShopkeepThemeBoostData.GetThemedBoostForItem(themeBoostDatas, heldObj)),
        ];
        return true;
    }

    public bool TryRemoveItemFromTable(Furniture table, Item item, int idx)
    {
        if (item is SObject obj && table.heldObject.Value == item)
        {
            obj.onDetachedFromParent();
            obj.performRemoveAction();
            table.heldObject.Value = null;
            return true;
        }
        return false;
    }

    public bool TryPlaceItemOnTable(Furniture table, ref Item? item)
    {
        if (item == null)
            return true;
        if (table.performObjectDropInAction(item, true, null) && table.performObjectDropInAction(item, false, null))
        {
            item = item?.ConsumeStack(1);
            return true;
        }
        return false;
    }
}

public sealed class TableShimFF(IFurnitureFrameworkAPI ffApi) : ITableShim
{
    private readonly TableShimBase baseShim = new();

    public bool HasSpaceForItems(Furniture table)
    {
        if (!ffApi.IsFF(table))
            return baseShim.HasSpaceForItems(table);

        return ffApi.GetSlotItems(table).Any(tbl => tbl.Item1 == null);
    }

    public bool TryGetForSaleTargets(
        Furniture table,
        Farmer player,
        HashSet<Point> reachableTiles,
        List<ShopkeepThemeBoostData>? themeBoostDatas,
        [NotNullWhen(true)] out List<ForSaleTarget?>? forSaleTargets
    )
    {
        if (!ffApi.IsFF(table))
            return baseShim.TryGetForSaleTargets(table, player, reachableTiles, themeBoostDatas, out forSaleTargets);

        forSaleTargets = null;
        List<Tuple<Item?, Vector2>> tableList = ffApi.GetSlotItems(table);
        if (tableList.Count == 0)
            return false;
        forSaleTargets = [];
        for (int i = 0; i < tableList.Count; i++)
        {
            (Item? item, Vector2 pos) = tableList[i];
            if (!ForSaleTarget.CanOfferForSale(item, player))
                continue;

            Rectangle boundingBox = new((int)(pos.X / Game1.tileSize), (int)(pos.Y / Game1.tileSize), 1, 1);

            List<(Point, int)> browseAround = Topology.FormBrowseAround(boundingBox, reachableTiles);
            bool unreachable = !browseAround.Any();
            if (unreachable)
            {
                // retry with the full bounding box
                boundingBox = new(
                    (int)table.TileLocation.X,
                    (int)table.TileLocation.Y,
                    table.getTilesWide(),
                    table.getTilesHigh()
                );
                browseAround = Topology.FormBrowseAround(boundingBox, reachableTiles);
                unreachable = !browseAround.Any();
                // still unreachable, continues
                if (unreachable)
                {
                    forSaleTargets.Add(null);
                    continue;
                }
            }
            forSaleTargets.Add(
                new(item, table, browseAround, ShopkeepThemeBoostData.GetThemedBoostForItem(themeBoostDatas, item), i)
            );
        }
        return forSaleTargets.Count > 0;
    }

    public bool TryPlaceItemOnTable(Furniture table, ref Item? item)
    {
        if (!ffApi.IsFF(table))
            return baseShim.TryPlaceItemOnTable(table, ref item);

        if (item == null)
            return true;
        List<Tuple<Item?, Vector2>> tableList = ffApi.GetSlotItems(table);
        for (int i = 0; i < tableList.Count; i++)
        {
            if (tableList[i].Item1 != null)
                continue;
            if (ffApi.CanSlotHold(table, i, item) && ffApi.PlaceInSlot(table, i, null, item, static () => { }))
            {
                item = item?.ConsumeStack(1);
                return true;
            }
        }
        return false;
    }

    public bool TryRemoveItemFromTable(Furniture table, Item item, int idx)
    {
        if (!ffApi.IsFF(table))
            return baseShim.TryRemoveItemFromTable(table, item, idx);

        if (item == null)
            return true;
        return ffApi.RemoveFromSlot(table, idx, static (itm) => true, static () => { }, out _);
    }
}
