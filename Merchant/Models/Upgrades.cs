using StardewModdingAPI;
using StardewValley;
using StardewValley.Internal;

namespace Merchant.Models;

public sealed class UpgradeSalable(string shopDisplayName, string shopDesc, Action purchasedAction) : SObject("897", 1)
{
    public override string DisplayName => shopDisplayName;

    public override string getDescription() => shopDesc;

    public override bool actionWhenPurchased(string shopId)
    {
        Game1.playSound("purchaseClick", null);
        purchasedAction();
        Game1.exitActiveMenu();
        return true;
    }
}

public static class Upgrades
{
    internal const string IQ_ADVERTISE = $"{ModEntry.ModId}_ADVERTISE";
    internal const string IQ_AUTO_RESTOCK = $"{ModEntry.ModId}_AUTO_RESTOCK";
    internal const string IQ_ROBO_SHOPKEEP_LEVEL = $"{ModEntry.ModId}_ROBO_SHOPKEEP_LEVEL";

    public static void Register()
    {
        ItemQueryResolver.Register(IQ_ADVERTISE, ADVERTISE);
        ItemQueryResolver.Register(IQ_AUTO_RESTOCK, AUTO_RESTOCK);
        ItemQueryResolver.Register(IQ_ROBO_SHOPKEEP_LEVEL, ROBO_SHOPKEEP_LEVEL);
    }

    private static IEnumerable<ItemQueryResult> ROBO_SHOPKEEP_LEVEL(
        string key,
        string arguments,
        ItemQueryContext context,
        bool avoidRepeat,
        HashSet<string> avoidItemIds,
        Action<string, string> logError
    )
    {
        if (!Context.IsWorldReady)
            return [];
        int roboShopkeepLevel = ModEntry.ProgressData.RoboShopkeepLevel;
        if (roboShopkeepLevel >= 20)
            return [];
        return
        [
            new ItemQueryResult(
                new UpgradeSalable(
                    I18n.Upgrade_Roboshopkeep_Name(roboShopkeepLevel / 5),
                    I18n.Upgrade_Roboshopkeep_Desc($"{roboShopkeepLevel:P2}"),
                    static () => ModEntry.ProgressData.RoboShopkeepLevel += 5
                )
                {
                    Price = 500000,
                }
            ),
        ];
    }

    private static IEnumerable<ItemQueryResult> ADVERTISE(
        string key,
        string arguments,
        ItemQueryContext context,
        bool avoidRepeat,
        HashSet<string> avoidItemIds,
        Action<string, string> logError
    )
    {
        if (!Context.IsWorldReady)
            return [];
        int advertiseLevel = ModEntry.ProgressData.AdvertiseLevel;
        if (advertiseLevel >= 32)
            return [];
        int upgradeCost;
        if (advertiseLevel <= 4)
        {
            upgradeCost = 1000;
        }
        else if (advertiseLevel <= 12)
        {
            upgradeCost = (int)(MathF.Ceiling(advertiseLevel / 4f) - 1) * 5000;
        }
        else if (advertiseLevel <= 24)
        {
            upgradeCost = (int)(MathF.Ceiling(advertiseLevel / 4f) - 3) * 20000;
        }
        else
        {
            upgradeCost = (int)(MathF.Ceiling(advertiseLevel / 4f) - 5) * 100000;
        }
        return
        [
            new ItemQueryResult(
                new UpgradeSalable(
                    I18n.Upgrade_Advertisement_Name(advertiseLevel / 4),
                    I18n.Upgrade_Advertisement_Desc(advertiseLevel),
                    static () => ModEntry.ProgressData.AdvertiseLevel += 4
                )
                {
                    Price = upgradeCost,
                }
            ),
        ];
    }

    private static IEnumerable<ItemQueryResult> AUTO_RESTOCK(
        string key,
        string arguments,
        ItemQueryContext context,
        bool avoidRepeat,
        HashSet<string> avoidItemIds,
        Action<string, string> logError
    )
    {
        if (!Context.IsWorldReady)
            return [];
        if (ModEntry.ProgressData.AutoRestockUnlocked)
        {
            if (ModEntry.ProgressData.AutoRestockEnabled)
            {
                return
                [
                    new ItemQueryResult(
                        new UpgradeSalable(
                            I18n.Upgrade_AutoRestock_Disable(),
                            I18n.Upgrade_AutoRestock_Desc(),
                            static () => ModEntry.ProgressData.AutoRestockEnabled = false
                        )
                        {
                            Price = 0,
                        }
                    ),
                ];
            }
            return
            [
                new ItemQueryResult(
                    new UpgradeSalable(
                        I18n.Upgrade_AutoRestock_Enable(),
                        I18n.Upgrade_AutoRestock_Desc(),
                        static () => ModEntry.ProgressData.AutoRestockEnabled = true
                    )
                    {
                        Price = 0,
                    }
                ),
            ];
        }
        return
        [
            new ItemQueryResult(
                new UpgradeSalable(
                    I18n.Upgrade_AutoRestock_Unlock(),
                    I18n.Upgrade_AutoRestock_Desc(),
                    static () =>
                    {
                        ModEntry.ProgressData.AutoRestockUnlocked = true;
                        ModEntry.ProgressData.AutoRestockEnabled = true;
                    }
                )
                {
                    Price = 50000,
                    Stack = 1,
                }
            ),
        ];
    }
}
