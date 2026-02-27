using System.Diagnostics.CodeAnalysis;
using StardewValley;

namespace Merchant.Models;

public sealed record SoldRecord(string Buyer, string ItemId, uint Price);

public sealed class ShopkeepSessionLog
{
    public string Shop = "Unknown";
    public bool IsAutoShopkeep { get; set; } = false;
    public int Date { get; set; } = 0; // days played
    public List<SoldRecord> Sales { get; set; } = [];
}

public sealed class MerchantProgressData
{
    private string key { get; set; } = "merchant";
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

    public void SaveShopkeepSession(
        string locationName,
        List<SoldRecord> sales,
        bool isAutoShopkeep,
        ulong totalEarnings
    )
    {
        ShopkeepSessionLog newLog = new()
        {
            Shop = locationName,
            IsAutoShopkeep = isAutoShopkeep,
            Date = Game1.Date.TotalDays,
            Sales = sales,
        };

        if (isAutoShopkeep)
            TotalEarnings += totalEarnings;

        Logs.Add(newLog);
    }

    public bool TryGetMostRecentLogForLocation(string locationName, [NotNullWhen(true)] out ShopkeepSessionLog? log)
    {
        log = null;
        for (int i = Logs.Count - 1; i >= 0; i--)
        {
            log = Logs[i];
            if (log.Shop == locationName)
                return true;
            log = null;
        }
        return false;
    }
}
