using Merchant.Management;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Merchant.Models;

public static class GameDelegates
{
    private const string TileAction_CashRegister = $"{ModEntry.ModId}_CashRegister";
    internal const string InteractMethod =
        $"Merchant.Models.{nameof(GameDelegates)}, Merchant: {nameof(InteractCashRegister)}";

    public static void Register()
    {
        GameLocation.RegisterTileAction(TileAction_CashRegister, TileActionCashRegister);
    }

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
        ShopkeepSessionLog? log = null;
        if (
            ModEntry.ProgressData?.TryGetMostRecentLogForLocation(location.NameOrUniqueName, out log, out int logIdx)
            ?? false
        )
        {
            responses.Add(new("merchant_sessionlog", I18n.Menu_SessionLog(logIdx + 1)));
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
                        ShopkeepGame.StartMinigame(location, player, cashRegisterPoint, browsing);
                        break;
                    case "merchant_checkbonus":
                        Game1.drawDialogueNoTyping(browsing.ShopBonus.FormatSummary());
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

    private static bool TileActionCashRegister(GameLocation location, string[] args, Farmer player, Point point) =>
        ShowMerchantMenu(location, player, point);

    public static bool InteractCashRegister(SObject machine, GameLocation location, Farmer player) =>
        ShowMerchantMenu(location, player, machine.TileLocation.ToPoint());
}
