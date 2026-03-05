using StardewValley;

namespace Merchant.Models;

public sealed class ShopkeepThemeBoostData
{
    public string? Id
    {
        get => field ??= ToString();
        set => field = value;
    } = null;
    public string? Description;
    public List<string>? ContextTags { get; set; } = null;

    public float Value
    {
        get => field;
        set => field = Math.Clamp(value, 0f, 0.5f);
    } = 0f;

    public override string ToString()
    {
        return string.Concat($"{Value:P2} ", ContextTags != null ? string.Join(',', ContextTags) : "ANY");
    }

    private List<string[]> SplitContextTags => field ??= ContextTags.SplitContextTags();

    public static ShopkeepThemeBoostData? GetThemedBoostForItem(List<ShopkeepThemeBoostData>? themedBoosts, Item item)
    {
        if (themedBoosts == null || themedBoosts.Count == 0)
            return null;
        foreach (ShopkeepThemeBoostData curBoost in themedBoosts)
        {
            if (curBoost.SplitContextTags.CheckContextTags(item))
            {
                return curBoost;
            }
        }
        return null;
    }
}
