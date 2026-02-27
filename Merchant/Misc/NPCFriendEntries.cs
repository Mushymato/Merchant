using System.Diagnostics.CodeAnalysis;
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

internal class NPCFriendEntries(Farmer player)
{
    private List<FriendEntry>? sorted = null;
    private int bisect = 0;

    internal void Clear() => sorted = null;

    private IEnumerable<FriendEntry> PickNRandomNPCs(int count, bool bestFriendsOnly)
    {
        sorted ??= PopulateSortedNPCList();

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

    internal IEnumerable<FriendEntry> PickCustomerNPCs(int maxCount)
    {
        int bffs = maxCount / 3;
        foreach (FriendEntry npc in PickNRandomNPCs(bffs, true))
        {
            maxCount--;
            yield return npc;
            if (maxCount == 0)
                yield break;
        }
        foreach (FriendEntry npc in PickNRandomNPCs(maxCount, false))
        {
            maxCount--;
            yield return npc;
            if (maxCount == 0)
                yield break;
        }
    }

    private List<FriendEntry> PopulateSortedNPCList()
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

    internal bool TryGetByName(string name, [NotNullWhen(true)] out NPC? npc)
    {
        sorted ??= PopulateSortedNPCList();

        foreach (FriendEntry friend in sorted)
        {
            if (friend.Npc.Name == name)
            {
                npc = friend.Npc;
                return true;
            }
        }

        npc = null;
        return false;
    }
}
