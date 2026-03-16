using Merchant.Misc;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Internal;
using StardewValley.TokenizableStrings;

namespace Merchant.Models;

public sealed class UpgradeSalable(
    string shopDisplayName,
    string shopDesc,
    Rectangle iconSourceRect,
    Action purchasedAction,
    bool drawNotAllowed = false
) : SObject("897", 1)
{
    private static readonly Rectangle errorRect = new(320, 496, 16, 16);
    private static readonly Texture2D tx = AssetManager.PowersTexture;
    public override string DisplayName => shopDisplayName;

    public override string getDescription() => shopDesc;

    public override bool actionWhenPurchased(string shopId)
    {
        purchasedAction();
        Game1.exitActiveMenu();
        return true;
    }

    public override void drawInMenu(
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth,
        StackDrawType drawStackNumber,
        Color color,
        bool drawShadow
    )
    {
        spriteBatch.Draw(
            tx,
            location + new Vector2(32f, 32f),
            iconSourceRect,
            color * transparency,
            0f,
            new Vector2(iconSourceRect.Width / 2, iconSourceRect.Height / 2),
            4f * scaleSize,
            SpriteEffects.None,
            layerDepth
        );
        if (drawNotAllowed)
        {
            spriteBatch.Draw(
                Game1.mouseCursors,
                location + new Vector2(32f, 32f),
                errorRect,
                color * transparency,
                0f,
                new Vector2(iconSourceRect.Width / 2, iconSourceRect.Height / 2),
                4f * scaleSize,
                SpriteEffects.None,
                layerDepth
            );
        }
    }
}

public static class Upgrades
{
    internal const string IQ_ADVERTISE_LEVEL = $"{ModEntry.ModId}_ADVERTISE_LEVEL";
    internal const string IQ_ROBO_SHOPKEEP_LEVEL = $"{ModEntry.ModId}_ROBO_SHOPKEEP_LEVEL";
    internal const string IQ_AUTO_RESTOCK = $"{ModEntry.ModId}_AUTO_RESTOCK";

    internal const string GSQ_HAS_ADVERTISE_LEVEL = $"{ModEntry.ModId}_HAS_ADVERTISE_LEVEL";
    internal const string GSQ_HAS_AUTO_RESTOCK = $"{ModEntry.ModId}_HAS_AUTO_RESTOCK";
    internal const string GSQ_HAS_ROBO_SHOPKEEP_LEVEL = $"{ModEntry.ModId}_HAS_ROBO_SHOPKEEP_LEVEL";

    public static void Register()
    {
        ItemQueryResolver.Register(IQ_ADVERTISE_LEVEL, ADVERTISE_LEVEL);
        ItemQueryResolver.Register(IQ_AUTO_RESTOCK, AUTO_RESTOCK);
        ItemQueryResolver.Register(IQ_ROBO_SHOPKEEP_LEVEL, ROBO_SHOPKEEP_LEVEL);

        GameStateQuery.Register(GSQ_HAS_ADVERTISE_LEVEL, HAS_ADVERTISE_LEVEL);
        GameStateQuery.Register(GSQ_HAS_AUTO_RESTOCK, HAS_AUTO_RESTOCK);
        GameStateQuery.Register(GSQ_HAS_ROBO_SHOPKEEP_LEVEL, HAS_ROBO_SHOPKEEP_LEVEL);

        TokenParser.RegisterParser(IQ_ADVERTISE_LEVEL, TS_ADVERTISE_LEVEL);
        TokenParser.RegisterParser(IQ_ROBO_SHOPKEEP_LEVEL, TS_ROBO_SHOPKEEP_LEVEL);
    }

    private static bool TS_ROBO_SHOPKEEP_LEVEL(string[] query, out string replacement, Random random, Farmer player)
    {
        replacement = string.Empty;
        if (!Context.IsWorldReady)
            return false;
        replacement = $"{ModEntry.ProgressData.RoboShopkeepLevel / 100f:P2}";
        ModEntry.Log(replacement);
        return true;
    }

    private static bool TS_ADVERTISE_LEVEL(string[] query, out string replacement, Random random, Farmer player)
    {
        replacement = string.Empty;
        if (!Context.IsWorldReady)
            return false;
        replacement = ModEntry.ProgressData.AdvertiseLevel.ToString();
        return true;
    }

    private static bool HAS_ROBO_SHOPKEEP_LEVEL(string[] query, GameStateQueryContext context)
    {
        if (!Context.IsWorldReady)
            return false;
        return ModEntry.ProgressData.RoboShopkeepLevel > MerchantProgressData.BASE_ROBO_SHOPKEEP;
    }

    private static bool HAS_AUTO_RESTOCK(string[] query, GameStateQueryContext context)
    {
        if (!Context.IsWorldReady)
            return false;
        return ModEntry.ProgressData.AutoRestockUnlocked;
    }

    private static bool HAS_ADVERTISE_LEVEL(string[] query, GameStateQueryContext context)
    {
        if (!Context.IsWorldReady)
            return false;
        return ModEntry.ProgressData.AdvertiseLevel > MerchantProgressData.BASE_ADVERTISE;
    }

    private static IEnumerable<ItemQueryResult> ROBO_SHOPKEEP_LEVEL(
        string key,
        string arguments,
        ItemQueryContext context,
        bool avoidRepeat,
        HashSet<string>? avoidItemIds,
        Action<string, string> logError
    )
    {
        if (!Context.IsWorldReady)
            return [];
        int roboShopkeepLevel = ModEntry.ProgressData.RoboShopkeepLevel;
        if (roboShopkeepLevel >= 20)
            return [];
        int level = roboShopkeepLevel / 5;
        return
        [
            new ItemQueryResult(
                new UpgradeSalable(
                    I18n.Upgrade_Roboshopkeep_Name(level),
                    I18n.Upgrade_Roboshopkeep_Desc($"{roboShopkeepLevel / 100f:P2}"),
                    new(32, 0, 16, 16),
                    static () => ModEntry.ProgressData.RoboShopkeepLevel += 5
                )
                {
                    Price = 200000 + 50000 * level,
                }
            ),
        ];
    }

    private static IEnumerable<ItemQueryResult> ADVERTISE_LEVEL(
        string key,
        string arguments,
        ItemQueryContext context,
        bool avoidRepeat,
        HashSet<string>? avoidItemIds,
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
                    new(0, 0, 16, 16),
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
        HashSet<string>? avoidItemIds,
        Action<string, string> logError
    )
    {
        if (!Context.IsWorldReady)
            return [];
        Rectangle iconSourceRect = new(16, 0, 16, 16);
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
                            iconSourceRect,
                            static () => ModEntry.ProgressData.AutoRestockEnabled = false,
                            drawNotAllowed: true
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
                        iconSourceRect,
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
                    iconSourceRect,
                    static () =>
                    {
                        ModEntry.ProgressData.AutoRestockUnlocked = true;
                        ModEntry.ProgressData.AutoRestockEnabled = true;
                    }
                )
                {
                    Price = 25000,
                    Stack = 1,
                }
            ),
        ];
    }
}
