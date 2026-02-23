using StardewValley;

namespace Merchant.Models;

public sealed record SoldRecord(string Buyer, string ItemId, uint Price);

public sealed class ShopkeepSessionLog
{
    public bool IsAutoShopkeep;
    public int Date; // days played
    public List<SoldRecord> Sales = [];
}

public sealed class MerchantProgressData
{
    private string key = "merchant";
    public ulong TotalEarningsManual = 0;
    public ulong TotalEarningsAuto = 0;
    public List<ShopkeepSessionLog> Logs = [];

    private void Validate()
    {
        foreach (ShopkeepSessionLog log in Logs)
        {
            ulong totalEarnings = 0;
            foreach (SoldRecord sale in log.Sales)
            {
                totalEarnings += sale.Price;
            }
            if (log.IsAutoShopkeep)
                TotalEarningsAuto += totalEarnings;
            else
                TotalEarningsManual += totalEarnings;
        }
    }

    public static MerchantProgressData Read()
    {
        string key = $"merchant-{Game1.player.slotName}";
        MerchantProgressData saveData = ModEntry.help.Data.ReadGlobalData<MerchantProgressData>(key) ?? new();
        saveData.key = key;
        saveData.Validate();
        return saveData;
    }

    public void Write()
    {
        ModEntry.help.Data.WriteGlobalData(key, this);
    }

    public void SaveShopkeepSession(List<SoldRecord> sales, bool isAutoShopkeep, ulong totalEarnings)
    {
        ShopkeepSessionLog newLog = new()
        {
            IsAutoShopkeep = isAutoShopkeep,
            Date = Game1.Date.TotalDays,
            Sales = sales,
        };

        if (isAutoShopkeep)
            TotalEarningsAuto += totalEarnings;
        else
            TotalEarningsManual += totalEarnings;

        Logs.Add(newLog);

        Write();
    }
}
