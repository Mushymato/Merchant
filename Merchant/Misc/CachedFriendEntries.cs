using System.Diagnostics.CodeAnalysis;
using Merchant.Management;
using Merchant.Models;
using StardewValley;
using StardewValley.Delegates;

namespace Merchant.Misc;

internal sealed class CachedFriendEntries(Farmer player)
{
    private bool populated = false;
    private readonly List<FriendEntry> sortedFriends = [];
    private readonly List<FriendEntry> nonFriends = [];
    private int sortedFriendsBisect = -1;
    private readonly GameStateQueryContext gsqContext = new(null, player, null, null, Random.Shared);

    internal void Reset()
    {
        populated = false;
        sortedFriends.Clear();
        nonFriends.Clear();
        sortedFriendsBisect = -1;
    }

    private void Repopulate()
    {
        if (populated)
            return;

        sortedFriends.Clear();
        nonFriends.Clear();

        Utility.ForEachVillager(npc =>
        {
            if (npc.Name != null && npc.CanSocialize)
            {
                if (!player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship))
                {
                    if (!ModEntry.config.AllowUnmetCustomers)
                    {
                        nonFriends.Add(
                            new(
                                npc,
                                AssetManager.Customers.Get(npc.Name),
                                null,
                                Utility.GetMaximumHeartsForCharacter(npc)
                            )
                        );
                        return true;
                    }
                }
                CustomerData? cxData = AssetManager.Customers.Get(npc.Name);
                FriendEntry? friendEntry = new(npc, cxData, friendship, Utility.GetMaximumHeartsForCharacter(npc));
                if (cxData?.WillComeToShop(gsqContext) ?? true)
                    sortedFriends.Add(friendEntry);
                else
                    nonFriends.Add(friendEntry);
            }
            return true;
        });

        sortedFriends.Sort((npcA, npcB) => npcA.FrenPercent.CompareTo(npcB.FrenPercent));
        sortedFriendsBisect = sortedFriends.Count;
        for (int i = 0; i < sortedFriends.Count; i++)
        {
            if (sortedFriends[i].IsMaxedHeart)
            {
                sortedFriendsBisect = i;
                break;
            }
        }

        populated = true;
    }

    internal bool TryGetFriendByName(
        string name,
        [NotNullWhen(true)] out FriendEntry? friend,
        bool includeNonFriends = false
    )
    {
        Repopulate();
        IEnumerable<FriendEntry> friendEntries;
        if (includeNonFriends)
            friendEntries = sortedFriends.Concat(nonFriends);
        else
            friendEntries = sortedFriends;
        foreach (FriendEntry friendEntry in friendEntries)
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
        LocationTopology locationTopology,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        ref List<CustomerActor> pickedActors
    )
    {
        if (friend.Npc.IsInvisible)
            return;
        if (excluding.Contains(friend.Name))
            return;
        if (friend.CxData != null && Random.Shared.NextSingle() > friend.CxData.Chance)
            return;
        if (forSaleTargets.All(forSale => friend.GetGiftTasteForSaleItem(forSale) == NPC.gift_taste_hate))
            return;
        pickedActors.Add(new(friend, locationTopology));
        excluding.Add(friend.Name);
    }

    internal List<CustomerActor> MakeCustomerActors(
        int maxCount,
        LocationTopology locationTopology,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        ref List<CustomerActor> pickedActors
    )
    {
        Repopulate();

        int bffs = Math.Max(1, maxCount / 3);

        List<int> range = Random.Shared.GetShuffledIdx(sortedFriendsBisect, sortedFriends.Count);
        foreach (int idx in range)
        {
            FriendEntry friendEntry = sortedFriends[idx];
            MakeCustomerActor(friendEntry, locationTopology, forSaleTargets, excluding, ref pickedActors);
            if (pickedActors.Count >= bffs)
                break;
        }

        int norms = maxCount - pickedActors.Count;
        if (norms <= 0)
            return pickedActors;

        range = Random.Shared.GetShuffledIdx(0, sortedFriends.Count);
        foreach (int idx in range)
        {
            FriendEntry friendEntry = sortedFriends[idx];
            MakeCustomerActor(friendEntry, locationTopology, forSaleTargets, excluding, ref pickedActors);
            if (pickedActors.Count >= norms)
                break;
        }

        return pickedActors;
    }
}
