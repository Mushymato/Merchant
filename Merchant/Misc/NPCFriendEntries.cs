using System.Diagnostics.CodeAnalysis;
using Merchant.Management;
using Merchant.Models;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Delegates;

namespace Merchant.Misc;

public abstract record BaseFriendEntry(NPC Npc, BaseCustomerData? BaseCxData, Friendship? Fren, int MaxHeartCount)
{
    public const int OneHeart = 250;
    public readonly bool IsTourist = BaseCxData?.IsTourist() ?? false;

    public readonly int FrenPoints = Fren?.Points ?? -1;
    public readonly float FrenPercent = (Fren?.Points ?? 0) / (float)(OneHeart * MaxHeartCount);
    public readonly bool IsMaxedHeart = (Fren?.Points ?? -1) >= OneHeart * MaxHeartCount;

    public abstract float GetHaggleBaseTargetPointer(ForSaleTarget forSale);

    public abstract float GetHaggleTargetOverRange(ForSaleTarget forSale);

    public abstract int GetGiftTasteForSaleItem(ForSaleTarget forSale);

    public abstract bool WillComeToShop(GameStateQueryContext context);
}

public sealed record FriendEntry(NPC Npc, CustomerData? CxData, Friendship? Fren, int MaxHeartCount)
    : BaseFriendEntry(Npc, CxData, Fren, MaxHeartCount)
{
    public override bool WillComeToShop(GameStateQueryContext context)
    {
        if (CxData == null)
            return true;
        if ((context.Random ?? Random.Shared).NextSingle() > CxData.Chance)
            return false;
        if (CxData.Condition == null)
            return true;
        return GameStateQuery.CheckConditions(CxData.Condition, context);
    }

    public override float GetHaggleBaseTargetPointer(ForSaleTarget forSale)
    {
        float haggleBaseTarget = FrenPoints <= 1 ? 0.15f : 0.15f + MathF.Log10(FrenPoints / 2000f) * 0.25f;

        int giftTaste = GetGiftTasteForSaleItem(forSale);
        switch (giftTaste)
        {
            case NPC.gift_taste_stardroptea:
            case NPC.gift_taste_love:
                haggleBaseTarget += 0.2f;
                break;
            case NPC.gift_taste_like:
                haggleBaseTarget += 0.1f;
                break;
            case NPC.gift_taste_dislike:
                haggleBaseTarget -= 0.1f;
                break;
        }
        return Math.Max(0f, haggleBaseTarget + 0.2f * Random.Shared.NextSingle());
    }

    public override float GetHaggleTargetOverRange(ForSaleTarget forSale)
    {
        if (FrenPoints >= 1250)
            return 0.25f;
        return 0.05f * (FrenPoints / 250f);
    }

    private readonly Dictionary<ForSaleTarget, int> cachedGiftTastes = [];

    public override int GetGiftTasteForSaleItem(ForSaleTarget forSale)
    {
        if (!cachedGiftTastes.TryGetValue(forSale, out int giftTaste))
        {
            giftTaste = Npc.getGiftTasteForThisItem(forSale.Thing);
            cachedGiftTastes[forSale] = giftTaste;
        }
        return giftTaste;
    }
}

public sealed record TouristFriendEntry(NPC Npc, TouristData TrstData) : BaseFriendEntry(Npc, TrstData, null, -2)
{
    public override bool WillComeToShop(GameStateQueryContext context)
    {
        throw new NotImplementedException();
    }

    public override int GetGiftTasteForSaleItem(ForSaleTarget forSale)
    {
        throw new NotImplementedException();
    }

    public override float GetHaggleBaseTargetPointer(ForSaleTarget forSale)
    {
        throw new NotImplementedException();
    }

    public override float GetHaggleTargetOverRange(ForSaleTarget forSale)
    {
        throw new NotImplementedException();
    }
}

internal class NPCFriendEntries(Farmer player)
{
    private List<FriendEntry>? sortedFriends = null;
    private int bisect = 0;

    internal void Clear() => sortedFriends = null;

    private void PickNRandomNPCs(ref List<CustomerActor> picked, Point entryPoint, int count, bool bestFriendsOnly)
    {
        if (count <= 0)
            return;

        sortedFriends ??= PopulateSortedNPCList();

        List<int> ranges;
        if (bestFriendsOnly)
        {
            if (bisect == sortedFriends.Count)
                return;
            ranges = Enumerable.Range(bisect, sortedFriends.Count - bisect).ToList();
        }
        else
        {
            ranges = Enumerable.Range(0, bisect).ToList();
        }
        if (ranges.Count == 0)
            return;

        Random.Shared.ShuffleInPlace(ranges);
        for (int i = 0; i < Math.Min(ranges.Count, count); i++)
        {
            FriendEntry friend = sortedFriends[ranges[i]];
            if (!friend.Npc.IsInvisible)
            {
                picked.Add(new(friend, entryPoint));
            }
        }
    }

    internal List<CustomerActor> MakeCustomerActors(int maxCount, Point entryPoint)
    {
        int bffs = Math.Max(1, maxCount / 3);
        List<CustomerActor> pickedActors = [];
        PickNRandomNPCs(ref pickedActors, entryPoint, bffs, true);
        ModEntry.Log($"Picked {pickedActors.Count} customers (bffs {sortedFriends?.Count - bisect})");
        maxCount -= pickedActors.Count;
        PickNRandomNPCs(ref pickedActors, entryPoint, maxCount, false);
        ModEntry.Log($"Picked {pickedActors.Count} customers");
        return pickedActors;
    }

    private List<FriendEntry> PopulateSortedNPCList()
    {
        GameStateQueryContext context = new(null, null, null, null, Random.Shared);
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
                    AssetManager.GetCustomerData(npc.Name),
                    friendship,
                    Utility.GetMaximumHeartsForCharacter(npc)
                );
                if (friendEntry.WillComeToShop(context))
                    newSortedList.Add(friendEntry);
            }
            return true;
        });
        newSortedList.Sort((npcA, npcB) => npcA.FrenPercent.CompareTo(npcB.FrenPercent));
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
        sortedFriends ??= PopulateSortedNPCList();

        foreach (BaseFriendEntry friend in sortedFriends)
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
