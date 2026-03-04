using System.Diagnostics.CodeAnalysis;
using Merchant.Management;
using Merchant.Models;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;

namespace Merchant.Misc;

internal sealed class CachedFriendEntries(Farmer player)
{
    private List<FriendEntry>? sortedFriends = null;
    private int sortedFriendsBisect = -1;
    private readonly GameStateQueryContext gsqContext = new(null, player, null, null, Random.Shared);

    internal void ResetFriends()
    {
        sortedFriends = null;
        sortedFriendsBisect = -1;
    }

    private List<FriendEntry> PopulateSortedFriends()
    {
        List<FriendEntry> newSortedList = [];
        Utility.ForEachVillager(npc =>
        {
            if (npc.Name != null && npc.CanSocialize)
            {
                if (!player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship))
                {
                    if (!ModEntry.config.AllowUnmetCustomers)
                        return true;
                }
                FriendEntry friendEntry = new(
                    npc,
                    AssetManager.Customers.Get(npc.Name),
                    friendship,
                    Utility.GetMaximumHeartsForCharacter(npc)
                );
                if (friendEntry.WillComeToShop(gsqContext))
                    newSortedList.Add(friendEntry);
            }
            return true;
        });
        newSortedList.Sort((npcA, npcB) => npcA.FrenPercent.CompareTo(npcB.FrenPercent));
        sortedFriendsBisect = newSortedList.Count;
        for (int i = 0; i < newSortedList.Count; i++)
        {
            if (newSortedList[i].IsMaxedHeart)
            {
                sortedFriendsBisect = i;
                break;
            }
        }
        return newSortedList;
    }

    internal bool TryGetFriendByName(string name, [NotNullWhen(true)] out FriendEntry? friend)
    {
        sortedFriends ??= PopulateSortedFriends();

        foreach (FriendEntry friendEntry in sortedFriends)
        {
            if (friendEntry.Name == name)
            {
                friend = friendEntry;
                return true;
            }
        }
        friend = null;
        return false;
    }

    private static void MakeCustomerActor(
        FriendEntry friend,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        Point entryPoint,
        ref List<CustomerActor> pickedActors
    )
    {
        if (friend.Npc.IsInvisible)
            return;
        if (excluding.Contains(friend.Npc.Name))
            return;
        if (friend.CxData != null && Random.Shared.NextSingle() > friend.CxData.Chance)
            return;
        if (forSaleTargets.All(forSale => friend.GetGiftTasteForSaleItem(forSale) == NPC.gift_taste_hate))
            return;
        pickedActors.Add(new(friend, entryPoint));
        excluding.Add(friend.Npc.Name);
    }

    internal List<CustomerActor> MakeCustomerActors(
        int maxCount,
        Point entryPoint,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding
    )
    {
        List<CustomerActor> pickedActors = [];
        sortedFriends ??= PopulateSortedFriends();

        int bffs = Math.Max(1, maxCount / 3);
        List<int> range = Random.Shared.GetShuffledIdx(sortedFriendsBisect, sortedFriends.Count);
        foreach (int idx in range)
        {
            ModEntry.Log($"Bffs: {idx}");
            FriendEntry friendEntry = sortedFriends[idx];
            MakeCustomerActor(friendEntry, forSaleTargets, excluding, entryPoint, ref pickedActors);
            if (pickedActors.Count >= bffs)
                break;
        }

        range = Random.Shared.GetShuffledIdx(0, sortedFriends.Count);
        foreach (int idx in range)
        {
            ModEntry.Log($"Norm: {idx}");
            FriendEntry friendEntry = sortedFriends[idx];
            MakeCustomerActor(friendEntry, forSaleTargets, excluding, entryPoint, ref pickedActors);
            if (pickedActors.Count >= maxCount)
                break;
        }

        return pickedActors;
    }
}
