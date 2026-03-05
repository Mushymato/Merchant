using Merchant.Management;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace Merchant.Models;

public static class GameDelegates
{
    internal const string InteractMethod =
        $"Merchant.Models.{nameof(GameDelegates)}, Merchant: {nameof(InteractCashRegister)}";
    private const string TileAction_CashRegister = $"{ModEntry.ModId}_CashRegister";
    private const string GSQ_BOOK_SELLER_IN_TOWN = $"{ModEntry.ModId}_BOOK_SELLER_IN_TOWN";
    private const string GSQ_IN_HAGGLING = $"{ModEntry.ModId}_IN_HAGGLING";
    internal const string Trigger_Merchant_Sold = $"{ModEntry.ModId}_Sold";
    internal const string ModData_SoldPrice = $"{ModEntry.ModId}/Sold/Price";
    internal const string ModData_SoldBuyer = $"{ModEntry.ModId}/Sold/Buyer";

    public static void Register()
    {
        GameLocation.RegisterTileAction(TileAction_CashRegister, TileActionCashRegister);
        TriggerActionManager.RegisterTrigger(Trigger_Merchant_Sold);
        GameStateQuery.Register(GSQ_BOOK_SELLER_IN_TOWN, BOOK_SELLER_IN_TOWN);
    }

    private static bool BOOK_SELLER_IN_TOWN(string[] query, GameStateQueryContext context)
    {
        return Utility.getDaysOfBooksellerThisSeason().Contains(Game1.dayOfMonth);
    }

    private static bool TileActionCashRegister(GameLocation location, string[] args, Farmer player, Point point) =>
        ShowMerchantMenu(location, player, point);

    public static bool InteractCashRegister(SObject machine, GameLocation location, Farmer player) =>
        ShowMerchantMenu(location, player, machine.TileLocation.ToPoint());

    public static bool ShowMerchantMenu(GameLocation location, Farmer player, Point cashRegisterPoint)
    {
        if (!ShopkeepBrowsing.TryMake(location, player, out ShopkeepBrowsing? browsing, out string? failReason))
        {
            Game1.drawObjectDialogue(failReason);
            return false;
        }
        List<Response> responses =
        [
            new("merchant_startgame", I18n.Menu_StartMinigame()),
            new("merchant_checkbonus", I18n.Menu_CheckBonus()),
        ];
        if (ModEntry.TourismWaves.ActiveWaves.Any())
        {
            responses.Add(new("merchant_tourism", I18n.Menu_TourismWave()));
        }
        if (
            ModEntry.ProgressData.TryGetMostRecentLogForLocation(
                location.NameOrUniqueName,
                out ShopkeepSessionLog? log,
                out int logIdx
            )
        )
        {
            responses.Add(new("merchant_sessionlog", I18n.Menu_SessionLog()));
        }
        responses.Add(
            new("merchant_cancel", Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel"))
        );

        location.createQuestionDialogue(
            I18n.Menu_Prompt(),
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
                                out string? failReason
                            )
                        )
                            Game1.drawObjectDialogue(failReason);
                        break;
                    case "merchant_checkbonus":
                        Game1.drawDialogueNoTyping(browsing.ShopBonus.FormatSummary());
                        break;
                    case "merchant_tourism":
                        Game1.drawDialogueNoTyping(ModEntry.TourismWaves.FormatSummary());
                        break;
                    case "merchant_sessionlog":
                        Game1.activeClickableMenu = SessionReportMenu.Make(log!);
                        break;
                }
            },
            speaker: null
        );
        return true;
    }
}
