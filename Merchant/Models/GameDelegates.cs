using Merchant.Management;
using Merchant.Menus;
using Merchant.Misc;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Menus;
using StardewValley.Triggers;

namespace Merchant.Models;

public static class GameDelegates
{
    internal const string InteractMethod_CashRegister =
        $"Merchant.Models.{nameof(GameDelegates)}, Merchant: {nameof(InteractCashRegister)}";
    internal const string InteractMethod_RoboShopkeep =
        $"Merchant.Models.{nameof(GameDelegates)}, Merchant: {nameof(InteractRoboShopkeep)}";
    private const string TileAction_CashRegister = $"{ModEntry.ModId}_CashRegister";
    private const string GSQ_BOOK_SELLER_IN_TOWN = $"{ModEntry.ModId}_BOOK_SELLER_IN_TOWN";
    private const string GSQ_SOLD_BUYER = $"{ModEntry.ModId}_SOLD_BUYER";
    private const string GSQ_SOLD_PRICE = $"{ModEntry.ModId}_SOLD_PRICE";
    private const string GSQ_ITEM_MATCHES_THEME = $"{ModEntry.ModId}_ITEM_MATCHES_THEME";
    internal const string Trigger_Merchant_Sold = $"{ModEntry.ModId}_Sold";
    internal const string ModData_SoldPrice = $"{ModEntry.ModId}/Sold/Price";
    internal const string ModData_SoldBuyer = $"{ModEntry.ModId}/Sold/Buyer";

    public static void Register()
    {
        GameLocation.RegisterTileAction(TileAction_CashRegister, TileActionCashRegister);
        TriggerActionManager.RegisterTrigger(Trigger_Merchant_Sold);
        GameStateQuery.Register(GSQ_BOOK_SELLER_IN_TOWN, BOOK_SELLER_IN_TOWN);
        GameStateQuery.Register(GSQ_SOLD_BUYER, SOLD_BUYER);
        GameStateQuery.Register(GSQ_SOLD_PRICE, SOLD_PRICE);
        GameStateQuery.Register(GSQ_ITEM_MATCHES_THEME, ITEM_MATCHES_THEME);
    }

    private static bool ITEM_MATCHES_THEME(string[] query, GameStateQueryContext context)
    {
        if (query.Length < 3)
        {
            ModEntry.Log("Expected at least 2 arguments", LogLevel.Error);
            return false;
        }
        if (
            !GameStateQuery.Helpers.TryGetItemArg(
                query,
                1,
                context.TargetItem,
                context.InputItem,
                out Item item,
                out string error
            )
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        foreach (string themeId in query.Skip(2))
        {
            if (!AssetManager.ThemeBoosts.Data.TryGetValue(themeId, out ShopkeepThemeBoostData? themeBoostData))
            {
                ModEntry.Log($"'{themeId}' is not a theme boost", LogLevel.Error);
                return false;
            }
            if (themeBoostData.SplitContextTags.CheckContextTags(item))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SOLD_PRICE(string[] query, GameStateQueryContext context)
    {
        if (
            !context.TargetItem.modData.TryGetValue(ModData_SoldPrice, out string priceStr)
            || !uint.TryParse(priceStr, out uint price)
        )
        {
            return false;
        }
        if (
            !ArgUtility.TryGetInt(query, 1, out int minValue, out string error, name: "int minValue")
            || !ArgUtility.TryGetOptionalInt(query, 2, out int maxValue, out error, name: "int maxValue")
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        maxValue = Math.Max(minValue, maxValue);
        return price >= minValue && price <= maxValue;
    }

    private static bool SOLD_BUYER(string[] query, GameStateQueryContext context)
    {
        if (!context.TargetItem.modData.TryGetValue(ModData_SoldBuyer, out string buyer))
        {
            return false;
        }
        if (!ArgUtility.TryGet(query, 1, out string buyerExpect, out string error, name: "string buyerExpect"))
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        return buyer == buyerExpect;
    }

    private static bool BOOK_SELLER_IN_TOWN(string[] query, GameStateQueryContext context)
    {
        return Utility.getDaysOfBooksellerThisSeason().Contains(Game1.dayOfMonth);
    }

    private static bool TileActionCashRegister(GameLocation location, string[] args, Farmer player, Point point)
    {
        return ShowMerchantMenu(location, player, point, args.Skip(1).ToArray(), relaxedTileStandLogic: true);
    }

    public static bool InteractCashRegister(SObject machine, GameLocation location, Farmer player)
    {
        string[] boostIds = [];
        if (
            machine
                .GetMachineData()
                ?.CustomFields?.TryGetValue(AssetManager.Metadata_ShopkeepThemeBoosts, out string? themeBoostIds)
            ?? false
        )
            boostIds = themeBoostIds.Split(',');
        return ShowMerchantMenu(location, player, machine.TileLocation.ToPoint(), boostIds);
    }

    public static bool ShowMerchantMenu(
        GameLocation location,
        Farmer player,
        Point cashRegisterPoint,
        string[] boostIds,
        bool relaxedTileStandLogic = false
    )
    {
        if (
            !ShopkeepBrowsing.TryMake(
                location,
                player,
                out ShopkeepBrowsing? browsing,
                out string? failReason,
                boostIds: boostIds
            )
        )
        {
            Game1.drawObjectDialogue(failReason);
            return false;
        }
        List<Response> responses =
        [
            new("merchant_startgame", I18n.Menu_StartMinigame()),
            new("merchant_manageupgrades", I18n.Menu_ManageUpgrades()),
            new("merchant_viewbonus", I18n.Menu_ViewBonus()),
        ];
        if (browsing.ShopBonus.ThemeBoostDatas?.Any() ?? false)
        {
            responses.Add(new("merchant_viewthemes", I18n.Menu_ViewThemes()));
        }
        if (ModEntry.TourismWaves.HasActiveWaves())
        {
            responses.Add(new("merchant_tourism", I18n.Menu_TourismWave()));
        }
        responses.Add(new("merchant_sessionlog", I18n.Menu_SessionLog()));
        responses.Add(
            new("merchant_cancel", Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel"))
        );

        location.createQuestionDialogue(
            I18n.Minigame_Id(),
            responses.ToArray(),
            (who, response) =>
            {
                switch (response)
                {
                    case "merchant_startgame":
                        if (
                            !ShopkeepGame.TryStartMinigame(
                                location,
                                player,
                                cashRegisterPoint,
                                browsing,
                                out string? failReason,
                                relaxedTileStandLogic: relaxedTileStandLogic
                            )
                        )
                            Game1.drawObjectDialogue(failReason);
                        break;
                    case "merchant_manageupgrades":
                        Utility.TryOpenShopMenu(AssetManager.UpgradeShopId, "AnyOrNone");
                        break;
                    case "merchant_viewbonus":
                        Game1.drawDialogueNoTyping(browsing.ShopBonus.FormatStats());
                        break;
                    case "merchant_viewthemes":
                        Game1.drawDialogueNoTyping(browsing.ShopBonus.FormatThemes());
                        break;
                    case "merchant_tourism":
                        Game1.drawDialogueNoTyping(ModEntry.TourismWaves.FormatSummary());
                        break;
                    case "merchant_sessionlog":
                        SessionListingMenu.TryShow(location.NameOrUniqueName);
                        break;
                }
            },
            speaker: null
        );
        return true;
    }

    public static bool InteractRoboShopkeep(SObject machine, GameLocation location, Farmer player)
    {
        if (machine.owner.Value != player.UniqueMultiplayerID)
        {
            Game1.drawObjectDialogue(I18n.FailReason_NotYourRobo());
            return false;
        }
        if (!ShopkeepBrowsing.TryMake(location, player, out ShopkeepBrowsing? browsing, out string? failReason))
        {
            Game1.drawObjectDialogue(failReason);
            return false;
        }
        List<Response> responses =
        [
            new("merchant_manageupgrades", I18n.Menu_ManageUpgrades()),
            new("merchant_viewbonus", I18n.Menu_ViewBonus()),
        ];
        if (browsing.ShopBonus.ThemeBoostDatas?.Any() ?? false)
        {
            responses.Add(new("merchant_viewthemes", I18n.Menu_ViewThemes()));
        }
        responses.Add(
            new("merchant_cancel", Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel"))
        );

        location.createQuestionDialogue(
            I18n.Roboshopkeep_Id(),
            responses.ToArray(),
            (who, response) =>
            {
                switch (response)
                {
                    case "merchant_viewbonus":
                        Game1.drawDialogueNoTyping(browsing.ShopBonus.FormatStats());
                        break;
                    case "merchant_manageupgrades":
                        Utility.TryOpenShopMenu(AssetManager.UpgradeShopId, "AnyOrNone");
                        break;
                    case "merchant_viewthemes":
                        Game1.drawDialogueNoTyping(browsing.ShopBonus.FormatThemes());
                        break;
                }
            },
            speaker: null
        );

        return true;
    }
}
