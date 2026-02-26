using Merchant.Misc;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Pathfinding;
using StardewValley.TokenizableStrings;

namespace Merchant.Management;

public enum CxDialogueKind
{
    Haggle_Ask,
    Haggle_Compromise,
    Haggle_Overpriced,
    Haggle_Fail,
    Haggle_Success,
}

public sealed class CustomerActor : NPC
{
    internal static readonly Event BogusEvent = new();
    #region make
    private readonly Point entryPoint;
    internal readonly FriendEntry sourceFriend;

    public CustomerActor(FriendEntry sourceFriend, Point entryPoint)
        : base(
            new AnimatedSprite(sourceFriend.Npc.Sprite.textureName.Value),
            Vector2.Zero,
            sourceFriend.Npc.speed,
            sourceFriend.Npc.Name
        )
    {
        NetFields.CopyFrom(sourceFriend.Npc.NetFields);
        this.sourceFriend = sourceFriend;
        this.entryPoint = entryPoint;
        forceOneTileWide.Value = true;
        followSchedule = false;
        EventActor = true;
    }
    #endregion

    #region social
    public Dialogue GetMerchantDialogue(NPC dummySpeaker, CxDialogueKind kind, params object[] substitutions)
    {
        dummySpeaker.Portrait = sourceFriend.Npc.Portrait;
        dummySpeaker.displayName = sourceFriend.Npc.displayName;
        if (sourceFriend.CxData != null)
        {
            string? dialogue = kind switch
            {
                CxDialogueKind.Haggle_Ask => sourceFriend.CxData.Haggle_Ask,
                CxDialogueKind.Haggle_Compromise => sourceFriend.CxData.Haggle_Compromise,
                CxDialogueKind.Haggle_Overpriced => sourceFriend.CxData.Haggle_Fail,
                CxDialogueKind.Haggle_Fail => sourceFriend.CxData.Haggle_Overpriced,
                CxDialogueKind.Haggle_Success => sourceFriend.CxData.Haggle_Success,
                _ => null,
            };
            if (dialogue != null)
            {
                return new Dialogue(
                    dummySpeaker,
                    string.Concat(AssetManager.Asset_Strings, ":", kind.ToString()),
                    string.Format(TokenParser.ParseText(dialogue) ?? dialogue, substitutions)
                );
            }
        }
        return new Dialogue(
            dummySpeaker,
            string.Concat(AssetManager.Asset_Strings, ":", kind.ToString()),
            AssetManager.LoadString(kind.ToString(), substitutions)
        );
    }

    public float GetFriendshipHaggleBonus()
    {
        // TODO: custom haggle bonus
        if (sourceFriend.Fren.Points <= 1)
            return 0.15f;
        return 0.15f + MathF.Log10(sourceFriend.Fren.Points / 2000f) * 0.25f;
    }

    public float GetHaggleBaseTargetPointer(ForSaleTarget forSale)
    {
        float haggleBaseTarget = GetFriendshipHaggleBonus();
        int giftTaste = GetGiftTasteForSaleItem(forSale);
        switch (giftTaste)
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
        // 0.05f per heart, up to 0.25f
        if (sourceFriend.Fren.Points >= 1250)
            return 0.25f;
        return 0.05f * (sourceFriend.Fren.Points / 250f);
    }

    private readonly Dictionary<ForSaleTarget, int> cachedGiftTastes = [];

    private int GetGiftTasteForSaleItem(ForSaleTarget forSale)
    {
        if (!cachedGiftTastes.TryGetValue(forSale, out int giftTaste))
        {
            giftTaste = sourceFriend.Npc.getGiftTasteForThisItem(forSale.Thing);
            cachedGiftTastes[forSale] = giftTaste;
        }
        return giftTaste;
    }
    #endregion

    #region browsing
    internal enum ActorState
    {
        Await,
        Move,
        Considering,
        Decide,
        Buy,
        Leaving,
        Finished,
        Decorative,
    }

    private readonly StateManager<ActorState> state = new(ActorState.Await);

    private int browsedCount = 0;
    private readonly int maxBrowsedCount = Random.Shared.Next(4, 7);
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

    public bool IsLeavingOrFinished => state.Current == ActorState.Leaving || state.Current == ActorState.Finished;

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
                    LeavingTheShop();
                return;
            }
            if (availableForSale.Count == 0)
            {
                return;
            }

            List<ForSaleTarget> preferredForSaleTargets = availableForSale
                .Where(forSale =>
                {
                    int giftTaste = GetGiftTasteForSaleItem(forSale);
                    return giftTaste != gift_taste_hate && giftTaste != gift_taste_dislike;
                })
                .ToList();

            ForSaleTarget nextForSale;
            if (preferredForSaleTargets.Count > 0)
            {
                nextForSale = Random.Shared.ChooseFrom(preferredForSaleTargets);
            }
            else if (availableForSale.Count > 0)
            {
                nextForSale = Random.Shared.ChooseFrom(availableForSale);
            }
            else
            {
                if (availableForSaleHeld == null)
                    LeavingTheShop();
                return;
            }

            browsedCount++;
            if (nextForSale == ForSale)
            {
                return;
            }
            ForSale = nextForSale;

            state.Current = ActorState.Move;
            (Point endPoint, int facing) = Random.Shared.ChooseFrom(ForSale.BrowseAround);
            controller = new PathFindController(this, currentLocation, endPoint, facing, ReachedForSaleItem);
        }
    }

    private void ReachedForSaleItem(Character c, GameLocation location)
    {
        state.Current = ActorState.Considering;
        state.SetNext(ActorState.Decide, Random.Shared.NextSingle() * 750, DecideBuy);
    }

    private void DecideBuy(ActorState oldState, ActorState newState)
    {
        if (ForSale != null)
        {
            int giftTaste = GetGiftTasteForSaleItem(ForSale);
            if (Random.Shared.NextSingle() < 0.3f + browsedCount * 0.1f)
            {
                doEmote(giftTaste == gift_taste_love ? 20 : 16);
                state.SetNext(ActorState.Buy, 500);
                return;
            }
        }

        ForSale = null;
        state.SetNext(
            browsedCount >= maxBrowsedCount ? ActorState.Leaving : ActorState.Await,
            Random.Shared.NextSingle() * 750
        );
    }

    internal void LeavingTheShop()
    {
        if (IsLeavingOrFinished)
            return;
        ForSale = null;
        state.Current = ActorState.Leaving;
        controller = new PathFindController(this, currentLocation, entryPoint, -1, LeftTheShop);
    }

    private void LeftTheShop(Character c, GameLocation location)
    {
        ModEntry.Log($"LeftTheShop {c.displayName}");
        ForSale = null;
        cachedGiftTastes.Clear();
        state.Current = ActorState.Finished;
        Position = Vector2.Zero;
        IsInvisible = true;
    }

    public override void update(GameTime time, GameLocation location)
    {
        base.update(time, location);
        // controller updates from vanilla
        if (!Game1.IsMasterGame)
        {
            if (controller == null && !freezeMotion)
            {
                updateMovement(location, time);
            }
            if (controller != null && !freezeMotion && controller.update(time))
            {
                controller = null;
            }
        }
        state.Update(time);
        if (state.Current == ActorState.Leaving && TilePoint == entryPoint)
        {
            LeftTheShop(this, location);
        }
    }
    #endregion
}
