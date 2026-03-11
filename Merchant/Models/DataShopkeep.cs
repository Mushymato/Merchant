using StardewValley;

namespace Merchant.Models;

public sealed class ShopkeepThemeBoostData
{
    public string? Description;
    public List<string>? ContextTags { get; set; } = null;

    public float Value
    {
        get => field;
        set => field = Math.Clamp(value, 0f, 0.5f);
    } = 0f;

    public override string ToString()
    {
        return string.Concat(Value.ToString(), '#', ContextTags != null ? string.Join(',', ContextTags) : "ANY");
    }

    internal List<string[]> SplitContextTags => field ??= ContextTags.SplitContextTags();

    public static ShopkeepThemeBoostData? GetThemedBoostForItem(List<ShopkeepThemeBoostData>? themedBoosts, Item item)
    {
        if (themedBoosts == null || themedBoosts.Count == 0)
            return null;
        foreach (ShopkeepThemeBoostData curBoost in themedBoosts)
        {
            if (curBoost.Value > 0f && curBoost.SplitContextTags.CheckContextTags(item))
            {
                return curBoost;
            }
        }
        return null;
    }
}
