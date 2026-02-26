using Merchant.Models;
using StardewValley;
using StardewValley.Delegates;

namespace Merchant.Misc;

public record FriendEntry(NPC Npc, Friendship Fren, int MaxHeartCount)
{
    public const int OneHeart = 250;
    public readonly CustomerData? CxData = AssetManager.GetCustomerData(Npc.Name);
    public float FrenPercent => Fren.Points / (float)(OneHeart * MaxHeartCount);
    public bool IsMaxedHeart => Fren.Points == OneHeart * MaxHeartCount;

    public bool WillComeToShop(GameStateQueryContext context)
    {
        if (CxData == null)
            return true;
        if ((context.Random ?? Random.Shared).NextSingle() > CxData.Chance)
            return false;
        if (CxData.Condition == null)
            return true;
        return GameStateQuery.CheckConditions(CxData.Condition, context);
    }
}

internal static class NPCLookup
{
    private static List<FriendEntry>? sorted = null;
    private static int bisect = 0;

    internal static void Clear() => sorted = null;

    private static IEnumerable<FriendEntry> PickNRandomNPCs(Farmer player, int count, bool bestFriendsOnly)
    {
        sorted ??= PopulateSortedNPCList(player);

        List<int> ranges;
        if (bestFriendsOnly)
        {
            if (bisect == sorted.Count)
                yield break;
            ranges = Enumerable.Range(bisect, sorted.Count - bisect).ToList();
        }
        else
        {
            ranges = Enumerable.Range(0, bisect).ToList();
        }
        if (ranges.Count == 0)
            yield break;

        Random.Shared.ShuffleInPlace(ranges);
        for (int i = 0; i < Math.Min(ranges.Count, count); i++)
        {
            yield return sorted[ranges[i]];
        }
    }

    internal static IEnumerable<FriendEntry> PickCustomerNPCs(Farmer player, int maxCount)
    {
        maxCount = Math.Min(maxCount, 3);
        foreach (FriendEntry npc in PickNRandomNPCs(player, 5, true))
        {
            maxCount--;
            yield return npc;
            if (maxCount == 0)
                yield break;
        }
        foreach (FriendEntry npc in PickNRandomNPCs(player, maxCount, false))
        {
            maxCount--;
            yield return npc;
            if (maxCount == 0)
                yield break;
        }
    }

    private static List<FriendEntry> PopulateSortedNPCList(Farmer player)
    {
        GameStateQueryContext context = new();
        List<FriendEntry> newSortedList = [];
        Utility.ForEachVillager(npc =>
        {
            if (
                npc.Name != null
                && npc.CanSocialize
                && player.friendshipData.TryGetValue(npc.Name, out Friendship friendship)
            )
            {
                FriendEntry friendEntry = new(npc, friendship, Utility.GetMaximumHeartsForCharacter(npc));
                if (friendEntry.WillComeToShop(context))
                    newSortedList.Add(friendEntry);
            }
            return true;
        });
        newSortedList.Sort(
            (npcA, npcB) =>
            {
                if (npcA.Fren.Points == npcB.Fren.Points)
                    return 0;
                return npcA.FrenPercent.CompareTo(npcB.FrenPercent);
            }
        );
        bisect = newSortedList.Count;
        for (int i = 0; i < newSortedList.Count; i++)
        {
            if (newSortedList[i].IsMaxedHeart)
            {
                bisect = i;
                break;
            }
        }
        return newSortedList;
    }
}
