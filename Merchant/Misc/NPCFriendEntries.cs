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

    public virtual bool WillComeToShop(GameStateQueryContext context)
    {
        if (BaseCxData == null)
            return true;
        if (BaseCxData.Condition == null)
            return true;
        return GameStateQuery.CheckConditions(BaseCxData.Condition, context);
    }
}

public sealed record FriendEntry(NPC Npc, CustomerData? CxData, Friendship? Fren, int MaxHeartCount)
    : BaseFriendEntry(Npc, CxData, Fren, MaxHeartCount)
{
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

public sealed record TouristFriendEntry(NPC Npc, TouristData TrstData, TourismWaveData WaveData)
    : BaseFriendEntry(Npc, TrstData, null, -2)
{
    public override float GetHaggleBaseTargetPointer(ForSaleTarget forSale) => 0.6f;

    public override float GetHaggleTargetOverRange(ForSaleTarget forSale) => 0.4f;

    public override int GetGiftTasteForSaleItem(ForSaleTarget forSale)
    {
        if (TrstData.DesiredContextTags?.All(forSale.Thing.HasContextTag) ?? false)
            return NPC.gift_taste_like;
        if (WaveData.DesiredContextTags?.All(forSale.Thing.HasContextTag) ?? false)
            return NPC.gift_taste_like;
        return NPC.gift_taste_hate;
    }
}

internal class NPCFriendEntries(Farmer player)
{
    private List<FriendEntry>? sortedFriends = null;
    private int bisect = -1;

    internal void Reset()
    {
        sortedFriends = null;
        bisect = -1;
    }

    private void PickNRandomNPCs(
        ref List<CustomerActor> picked,
        Point entryPoint,
        int count,
        bool bestFriendsOnly,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding
    )
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
            ranges = Enumerable.Range(0, sortedFriends.Count - 1).ToList();
        }
        if (ranges.Count == 0)
            return;

        Random.Shared.ShuffleInPlace(ranges);
        for (int i = 0; i < Math.Min(ranges.Count, count); i++)
        {
            FriendEntry friend = sortedFriends[ranges[i]];
            if (
                !friend.Npc.IsInvisible
                && !excluding.Contains(friend.Npc.Name)
                && (friend.CxData == null || Random.Shared.NextSingle() <= friend.CxData.Chance)
                && forSaleTargets.Any(forSale => friend.GetGiftTasteForSaleItem(forSale) != NPC.gift_taste_hate)
            )
            {
                picked.Add(new(friend, entryPoint));
                excluding.Add(friend.Npc.Name);
            }
        }
    }

    internal List<CustomerActor> MakeCustomerActors(
        int maxCount,
        Point entryPoint,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding
    )
    {
        int bffs = Math.Max(1, maxCount / 3);
        List<CustomerActor> pickedActors = [];
        PickNRandomNPCs(ref pickedActors, entryPoint, bffs, true, forSaleTargets, excluding);
        ModEntry.Log($"Picked {pickedActors.Count}/{maxCount} customers (bffs {sortedFriends?.Count - bisect})");
        PickNRandomNPCs(ref pickedActors, entryPoint, maxCount - pickedActors.Count, false, forSaleTargets, excluding);
        ModEntry.Log($"Picked {pickedActors.Count}/{maxCount} customers");
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
                    AssetManager.Customers.Get(npc.Name),
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
