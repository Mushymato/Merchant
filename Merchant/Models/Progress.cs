using System.Diagnostics.CodeAnalysis;
using Merchant.Management;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;

namespace Merchant.Models;

public sealed record SoldRecord(
    string Buyer,
    bool IsTourist,
    uint Price,
    string ItemId,
    string? PreserveId,
    byte[]? Color
)
{
    public static SoldRecord Make(CustomerActor buyer, uint price, Item item) =>
        Make(buyer.Name, buyer.sourceFriend.IsTourist, price, item);

    public static SoldRecord Make(string buyerName, bool isTourist, uint price, Item item)
    {
        string? preserveId = null;
        byte[]? colorBytes = null;
        if (item is SObject obj)
        {
            preserveId = obj.preservedParentSheetIndex.Value;
            if (obj is ColoredObject coloredObj)
            {
                Color color = coloredObj.color.Value;
                colorBytes = [color.R, color.G, color.B, color.A];
            }
        }
        Item thing = item;
        thing.modData[GameDelegates.ModData_SoldPrice] = price.ToString();
        thing.modData[GameDelegates.ModData_SoldBuyer] = buyerName;
        return new SoldRecord(buyerName, isTourist, price, item.QualifiedItemId, preserveId, colorBytes);
    }

    public Item CreateReprItem()
    {
        Item? reprItem;
        if (Color != null)
        {
            Color itemColor = new(Color[0], Color[1], Color[2], Color[3]);
            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(ItemId);
            reprItem = new ColoredObject(itemData.ItemId, 1, itemColor);
        }
        else
        {
            reprItem = ItemRegistry.Create(ItemId);
        }

        if (reprItem is SObject obj)
        {
            obj.Edibility = -300;
            if (PreserveId != null)
            {
                obj.preservedParentSheetIndex.Value = PreserveId;
            }
        }
        return reprItem;
    }

    public override string ToString()
    {
        if (PreserveId != null)
            return $"Sold({Buyer}, {Price}, {ItemId}/{PreserveId})";
        return $"Sold({Buyer}, {Price}, {ItemId})";
    }
}

public sealed class ShopkeepSessionLog
{
    public string Shop = "Unknown";
    public bool IsRoboShopkeep { get; set; } = false;
    public int Date { get; set; } = 0; // days played
    public List<SoldRecord> Sales { get; set; } = [];
}

public sealed class MerchantProgressData
{
    #region saved progress
    public List<ShopkeepSessionLog> Logs { get; set; } = [];
    public int AdvertiseLevel = 4;
    public int RoboShopkeepLevel = 5;
    public bool AutoRestockUnlocked = false;
    public bool AutoRestockEnabled = false;
    #endregion

    private const string Stat_Sessions = $"{ModEntry.ModId}_ShopkeepSessions";
    private const string Stat_Earnings = $"{ModEntry.ModId}_ShopkeepEarnings";
    private string key = "merchant";
    internal ulong TotalEarnings { get; set; } = 0;

    private void FinishLoading()
    {
        foreach (ShopkeepSessionLog log in Logs)
        {
            ulong totalEarnings = 0;
            foreach (SoldRecord sale in log.Sales)
            {
                totalEarnings += sale.Price;
            }
            if (!log.IsRoboShopkeep)
                TotalEarnings += totalEarnings;
        }
        Game1.player.stats.Set(Stat_Sessions, Logs.Count);
        Game1.player.stats.Set(Stat_Earnings, (uint)TotalEarnings);
    }

    public static MerchantProgressData Read()
    {
        string key = $"progress-{Game1.uniqueIDForThisGame}-{Game1.player.UniqueMultiplayerID}";
        ModEntry.Log($"Read progress data '{key}'");
        MerchantProgressData saveData = ModEntry.help.Data.ReadGlobalData<MerchantProgressData>(key) ?? new();
        saveData.key = key;
        saveData.FinishLoading();
        return saveData;
    }

    public void Write()
    {
        ModEntry.Log($"Wrote progress data '{key}'");
        ModEntry.help.Data.WriteGlobalData(key, this);
    }

    public ShopkeepSessionLog SaveShopkeepSession(ShopkeepSessionLog newLog, ulong totalEarnings)
    {
        if (!newLog.IsRoboShopkeep)
            TotalEarnings += totalEarnings;
        Logs.Add(newLog);
        Game1.player.stats.Set(Stat_Sessions, Logs.Count);
        Game1.player.stats.Set(Stat_Earnings, (uint)TotalEarnings);
        return newLog;
    }

    public bool TryGetMostRecentLogForLocation(
        string locationName,
        [NotNullWhen(true)] out ShopkeepSessionLog? log,
        out int logIdx
    )
    {
        log = null;
        logIdx = -1;
        for (int i = Logs.Count - 1; i >= 0; i--)
        {
            log = Logs[i];
            if (log.Shop == locationName && !log.IsRoboShopkeep)
            {
                logIdx = i;
                return true;
            }
            log = null;
        }
        return false;
    }

    internal static void ListProgressForDeletedSaves()
    {
        string savesFolder = Program.GetSavesFolder();
        List<string> saveFiles = [];
        HashSet<ulong> allSaveIds = [];
        foreach (string item in Directory.EnumerateDirectories(savesFolder))
        {
            string saveName = Path.GetFileName(item);
            string pathToSave = Path.Combine(savesFolder, item, saveName);
            if (File.Exists(pathToSave))
            {
                saveFiles.Add(pathToSave);
                string[] split = saveName.Split('_', 2);
                if (split.Length == 2 && ulong.TryParse(split[1], out ulong saveId))
                {
                    allSaveIds.Add(saveId);
                }
            }
        }
        ModEntry.Log($"Found save files:\n\t'{string.Join("\n\t", saveFiles)}'", LogLevel.Info);

        List<string> progessFileWithoutSave = [];
        string progressDataDir = Path.Combine(Constants.DataPath, ".smapi", "mod-data", ModEntry.ModId.ToLower());
        foreach (string item in Directory.EnumerateFiles(progressDataDir))
        {
            string[] split = Path.GetFileName(item).Split('_', 3);
            if (
                split.Length == 3
                && split[0] == "progress"
                && ulong.TryParse(split[1], out ulong saveId)
                && !allSaveIds.Contains(saveId)
            )
            {
                progessFileWithoutSave.Add(item);
            }
        }
        ModEntry.Log(
            $"Progress files with no associated save files:\n\t{string.Join("\n\t", progessFileWithoutSave)}",
            LogLevel.Info
        );
    }
}
