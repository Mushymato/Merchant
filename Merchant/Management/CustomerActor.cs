using Merchant.Misc;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Pathfinding;

namespace Merchant.Management;

public sealed class CustomerActor : NPC
{
    #region make
    private readonly Friendship? friendship;
    private readonly Point entryPoint;

    public CustomerActor(NPC sourceNPC, GameLocation location, Farmer player, Point entryPoint)
        : base(
            new AnimatedSprite(sourceNPC.Sprite.textureName.Value),
            Vector2.Zero,
            location.NameOrUniqueName,
            sourceNPC.FacingDirection,
            sourceNPC.Name,
            sourceNPC.Portrait,
            true
        )
    {
        NetFields.CopyFrom(sourceNPC.NetFields);
        this.entryPoint = entryPoint;
        forceOneTileWide.Value = true;
        followSchedule = false;
        if (!player.friendshipData.TryGetValue(sourceNPC.Name, out friendship))
        {
            friendship = null;
        }
    }
    #endregion

    #region social
    public Dialogue GetMerchantDialogue(string key, params object[] substitutions)
    {
        string merchantKey = $"{ModEntry.ModId}_{key}";
        if (TryGetDialogue(merchantKey, substitutions) is Dialogue dialogue)
            return dialogue;
        return new Dialogue(
            this,
            string.Concat(AssetManager.Asset_Strings, ":", key),
            AssetManager.LoadString(key, substitutions)
        );
    }

    public float GetFriendshipHaggleBonus()
    {
        // TODO: custom haggle bonus
        if (friendship == null)
            return 0.1f;
        if (friendship.Points <= 1)
            return 0.15f;
        return 0.15f + MathF.Log10(friendship.Points / 2000f) * 0.25f;
    }

    public float GetHaggleBaseTargetPointer(Item forSale)
    {
        float haggleBaseTarget = GetFriendshipHaggleBonus();
        switch (getGiftTasteForThisItem(forSale))
        {
            case gift_taste_stardroptea:
            case gift_taste_love:
                haggleBaseTarget += 0.2f;
                break;
            case gift_taste_like:
                haggleBaseTarget += 0.1f;
                break;
        }
        return haggleBaseTarget + 0.3f * Random.Shared.NextSingle();
    }

    public float GetHaggleTargetOverRange()
    {
        // TODO: custom target over range bonus
        if (friendship == null)
            return 0.1f;
        return 0.2f + Math.Min(0.4f, friendship.Points / 50000f);
    }
    #endregion

    #region browsing
    internal enum ActorState
    {
        Await,
        Move,
        Check,
        Buy,
        Leaving,
        Finished,
    }

    private readonly StateManager<ActorState> state = new(ActorState.Await);

    private int browsedCount = 0;
    private const int maxBrowsedCount = 5;
    public ForSaleTarget? ForSale
    {
        get => field;
        set
        {
            field?.HeldBy = null;
            value?.HeldBy = this;
            field = value;
        }
    }

    public bool IsLeaving => state.Current == ActorState.Leaving || state.Current == ActorState.Finished;

    public void UpdateBuyTarget(
        List<ForSaleTarget>? availableForSale,
        List<ForSaleTarget>? availableForSaleHeld,
        out ForSaleTarget? hagglingForSaleTarget
    )
    {
        hagglingForSaleTarget = null;
        if (state.Current == ActorState.Buy)
        {
            if (ForSale == null)
            {
                state.Current = ActorState.Await;
            }
            else
            {
                hagglingForSaleTarget = ForSale;
            }
        }
        if (state.Current == ActorState.Await)
        {
            if (availableForSale == null)
            {
                if (availableForSaleHeld == null)
                {
                    DoneHaggling();
                }
                return;
            }
            if (availableForSale.Count == 0)
            {
                return;
            }
            state.Current = ActorState.Move;
            List<ForSaleTarget> likedForSaleTargets = availableForSale.Where(ForSaleNotHated).ToList();
            if (likedForSaleTargets.Count == 0)
            {
                if (availableForSaleHeld == null || !availableForSaleHeld.Where(ForSaleNotHated).Any())
                {
                    DoneHaggling();
                }
                return;
            }
            ForSale = Random.Shared.ChooseFrom(likedForSaleTargets);
            (Point endPoint, int facing) = Random.Shared.ChooseFrom(ForSale.BrowseAround);
            controller = new PathFindController(this, currentLocation, endPoint, facing, ReachedForSaleItem);
        }
    }

    private readonly Dictionary<ForSaleTarget, int> cachedGiftTastes = [];

    private bool ForSaleNotHated(ForSaleTarget forSale)
    {
        if (!cachedGiftTastes.TryGetValue(forSale, out int giftTaste))
        {
            giftTaste = getGiftTasteForThisItem(forSale.Thing);
            cachedGiftTastes[forSale] = giftTaste;
        }
        return giftTaste != gift_taste_dislike && giftTaste != gift_taste_hate;
    }

    private void FinishedBuying(Character c, GameLocation location)
    {
        ForSale = null;
        cachedGiftTastes.Clear();
        state.Current = ActorState.Finished;
        location.characters.Remove(this);
    }

    private void ReachedForSaleItem(Character c, GameLocation location)
    {
        state.Current = ActorState.Check;
        browsedCount++;
        if (Random.Shared.NextSingle() < 0.3f + browsedCount * 0.1f)
        {
            doEmote(16);
            state.SetNext(ActorState.Buy, 1000);
        }
        else
        {
            ForSale = null;
            state.SetNext(browsedCount >= maxBrowsedCount ? ActorState.Leaving : ActorState.Await, 1000);
        }
    }

    internal void DoneHaggling()
    {
        ForSale = null;
        state.Current = ActorState.Leaving;
        controller = new PathFindController(this, currentLocation, entryPoint, -1, FinishedBuying);
    }

    public override void update(GameTime time, GameLocation location)
    {
        base.update(time, location);
        state.Update(time);
        if (state.Current == ActorState.Leaving && TilePoint == entryPoint)
        {
            FinishedBuying(this, location);
        }
    }
    #endregion
}
