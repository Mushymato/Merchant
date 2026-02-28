using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace Merchant.Models;

public sealed record SoldRecord(string Buyer, uint Price, string ItemId, string? PreserveId, byte[]? Color)
{
    public static SoldRecord Make(string buyer, uint price, Item item)
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
        return new SoldRecord(buyer, price, item.QualifiedItemId, preserveId, colorBytes);
    }

    public Item CreateReprItem()
    {
        Item? reprItem;
        if (Color != null)
        {
            Color itemColor = new(Color[0], Color[1], Color[2], Color[3]);
            reprItem = new ColoredObject(ItemId, 1, itemColor);
        }
        else
        {
            reprItem = ItemRegistry.Create(ItemId);
        }

        if (reprItem is SObject obj)
            obj.Edibility = -300;
        return reprItem;
    }
}

public sealed class ShopkeepSessionLog
{
    public string Shop = "Unknown";
    public bool IsAutoShopkeep { get; set; } = false;
    public int Date { get; set; } = 0; // days played
    public List<SoldRecord> Sales { get; set; } = [];
}

public sealed class MerchantProgressData
{
    private string key = "merchant";
    internal ulong TotalEarnings { get; set; } = 0;
    internal uint TotalItemsSold { get; set; } = 0;

    public List<ShopkeepSessionLog> Logs { get; set; } = [];

    private void Validate()
    {
        foreach (ShopkeepSessionLog log in Logs)
        {
            ulong totalEarnings = 0;
            foreach (SoldRecord sale in log.Sales)
            {
                totalEarnings += sale.Price;
            }
            if (!log.IsAutoShopkeep)
                TotalEarnings += totalEarnings;
        }
    }

    public static MerchantProgressData Read()
    {
        string key = $"progress-{Game1.uniqueIDForThisGame}-{Game1.player.UniqueMultiplayerID}";
        ModEntry.Log($"Read progress data '{key}'");
        MerchantProgressData saveData = ModEntry.help.Data.ReadGlobalData<MerchantProgressData>(key) ?? new();
        saveData.key = key;
        saveData.Validate();
        return saveData;
    }

    public void Write()
    {
        ModEntry.Log($"Wrote progress data '{key}'");
        ModEntry.help.Data.WriteGlobalData(key, this);
    }

    public ShopkeepSessionLog SaveShopkeepSession(ShopkeepSessionLog newLog, ulong totalEarnings)
    {
        if (!newLog.IsAutoShopkeep)
            TotalEarnings += totalEarnings;
        Logs.Add(newLog);
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
            if (log.Shop == locationName)
            {
                logIdx = i;
                return true;
            }
            log = null;
        }
        return false;
    }
}
