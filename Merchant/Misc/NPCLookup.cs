using Merchant.Misc;
using StardewValley;

internal static class NPCLookup
{
    private static List<NPC>? sorted = null;
    private static int bisect = 0;

    internal static void Clear() => sorted = null;

    internal static IEnumerable<NPC> PickNRandomNPCs(Farmer player, int count = 10, int startIdx = 0)
    {
        sorted ??= PopulateSortedNPCList(player);
        int validCount = sorted.Count - startIdx;
        if (validCount == 0)
            yield break;
        List<int> ranges = Enumerable.Range(startIdx, validCount).ToList();
        Random.Shared.ShuffleInPlace(ranges);
        for (int i = 0; i < Math.Min(ranges.Count, count); i++)
        {
            yield return sorted[ranges[i]];
        }
    }

    internal static IEnumerable<NPC> PickCustomerNPCs(Farmer player, int maxCount)
    {
        foreach (NPC npc in PickNRandomNPCs(player, 4, bisect))
        {
            maxCount--;
            yield return npc;
            if (maxCount == 0)
                yield break;
        }
        foreach (NPC npc in PickNRandomNPCs(player, 8, 0))
        {
            maxCount--;
            yield return npc;
            if (maxCount == 0)
                yield break;
        }
    }

    private static List<NPC> PopulateSortedNPCList(Farmer player)
    {
        List<NPC> newSortedList = [];
        Utility.ForEachVillager(npc =>
        {
            if (npc.CanSocialize)
                newSortedList.Add(npc);
            return true;
        });
        newSortedList.Sort(
            (npcA, npcB) =>
            {
                int? friendshipA = player.tryGetFriendshipLevelForNPC(npcA.Name);
                int? friendshipB = player.tryGetFriendshipLevelForNPC(npcB.Name);
                if (friendshipA == friendshipB)
                    return 0;
                if (friendshipA == null)
                    return -1;
                if (friendshipB == null)
                    return 1;
                return (friendshipA.Value / (float)Utility.GetMaximumHeartsForCharacter(npcA)).CompareTo(
                    friendshipB.Value / (float)Utility.GetMaximumHeartsForCharacter(npcB)
                );
            }
        );
        bisect = newSortedList.Count;
        for (int i = 0; i < newSortedList.Count; i++)
        {
            if (
                player.getFriendshipHeartLevelForNPC(newSortedList[i].Name)
                >= Utility.GetMaximumHeartsForCharacter(newSortedList[i])
            )
            {
                bisect = i;
                break;
            }
        }
        return newSortedList;
    }
}
