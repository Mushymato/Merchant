using StardewValley;

namespace Merchant.Models;

public sealed class ShopkeepContextData
{
    // Theme
    public List<ShopkeepThemeBoostData>? ThemedBoosts { get; set; } = null;

    public ShopkeepThemeBoostData? GetThemedBoostForItem(Item item)
    {
        if (ThemedBoosts == null || ThemedBoosts.Count == 0)
            return null;
        SObject? obj = item as SObject;
        foreach (ShopkeepThemeBoostData curBoost in ThemedBoosts)
        {
            if (curBoost.RequiredContextTags?.All((obj ?? item).HasContextTag) ?? false)
            {
                return curBoost;
            }
        }
        return null;
    }
}

public sealed class ShopkeepThemeBoostData
{
    public string? Id
    {
        get => field ??= ToString();
        set => field = value;
    } = null;
    public string? Description;
    public List<string>? RequiredContextTags { get; set; } = null;
    public float Value
    {
        get => field;
        set => field = Math.Clamp(value, 0f, 0.5f);
    } = 0f;

    public override string ToString()
    {
        return string.Concat(
            $"{Value:P2} ",
            RequiredContextTags != null ? string.Join(',', RequiredContextTags) : "ANY"
        );
    }
}
