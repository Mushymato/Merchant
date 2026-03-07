using Merchant.Misc;
using Merchant.Models;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Triggers;

namespace Merchant.Management;

public static class RoboSales
{
    internal const string RoboBuyer = $"{ModEntry.ModId}_RoboBuyer";

    internal static bool PerformRoboSaleOnBuilding(Building building)
    {
        if (building.GetIndoors() is not GameLocation location)
            return true;

        Farmer? sellingPlayer = null;
        Dictionary<long, Farmer> stakeholderFarmers = [];
        foreach (SObject obj in location.objects.Values)
        {
            if (
                obj.HasContextTag(AssetManager.ContextTag_RoboShopkeep)
                && Game1.GetPlayer(obj.owner.Value) is Farmer player
            )
            {
                sellingPlayer ??= player;
                stakeholderFarmers[obj.owner.Value] = player;
            }
        }
        if (stakeholderFarmers.Count == 0 || sellingPlayer == null)
            return true;

        if (!ShopkeepBrowsing.TryMake(location, sellingPlayer, out ShopkeepBrowsing? browsing, out _, getActors: false))
            return true;

        ShopkeepHaggle.GetMinAndMaxMult(browsing.ShopBonus.TotalBonus, out float minMult, out float maxMult);

        foreach (ForSaleTarget forSale in browsing.ForSaleTargets)
        {
            uint basePrice = forSale.GetBasePrice(sellingPlayer);
            uint pickedPrice = ShopkeepHaggle.CalcPntToPrice(
                basePrice,
                ModEntry.ProgressData.RoboShopkeepLevel / 100f,
                minMult,
                maxMult,
                forSale.Boost?.Value ?? 0f
            );
            forSale.Sold = SoldRecord.Make(RoboBuyer, false, pickedPrice, forSale.Thing);
            ShopkeepBrowsing.ApplyShippedBehaviors(sellingPlayer, forSale, pickedPrice);
            if (ModEntry.HasBETAS)
                TriggerActionManager.Raise(
                    "Spiderbuttons.BETAS_ItemShipped",
                    location: location,
                    player: sellingPlayer,
                    targetItem: forSale.Thing
                );
        }

        if (
            ShopkeepBrowsing.TryMakeSessionLog(
                location,
                browsing.ForSaleTargets,
                $"ROBO({location.NameOrUniqueName})",
                true,
                out ulong sessionEarnings,
                out _
            )
        )
        {
            ulong sharedEarning = (ulong)Math.Ceiling(sessionEarnings / (double)stakeholderFarmers.Count);
            foreach (Farmer player in stakeholderFarmers.Values)
            {
                player.Money += (int)sharedEarning;
            }
        }

        return true;
    }
}
