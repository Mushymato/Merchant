using StardewValley;

namespace Merchant.Models;

internal static class ContextTags
{
    public static List<string[]> SplitContextTags(this List<string>? rawTags)
    {
        if (rawTags == null || rawTags.Count == 0)
            return [];
        List<string[]> splitTags = [];
        foreach (string tagGroup in rawTags)
        {
            if (!string.IsNullOrEmpty(tagGroup))
                splitTags.Add(tagGroup.Split(','));
        }
        return splitTags;
    }

    public static bool CheckContextTags(this List<string[]> splitTags, Item item)
    {
        foreach (string[] tags in splitTags)
        {
            if (tags.All(item.HasContextTag))
                return true;
        }
        return false;
    }
}
