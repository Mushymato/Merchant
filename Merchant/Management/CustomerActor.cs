using Merchant.Misc;
using Merchant.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.Pathfinding;
using StardewValley.TokenizableStrings;

namespace Merchant.Management;

public sealed class CustomerActor : NPC
{
    internal static readonly Event BogusEvent = new();
    #region make
    private readonly Point entryPoint;
    internal readonly BaseFriendEntry sourceFriend;

    public CustomerActor(BaseFriendEntry sourceFriend, Point entryPoint)
        : base(sourceFriend.Sprite, Vector2.Zero, 2, sourceFriend.Name)
    {
        this.sourceFriend = sourceFriend;
        this.entryPoint = entryPoint;

        Portrait = Game1.mouseCursors;
        portraitOverridden = true;

        forceOneTileWide.Value = true;
        followSchedule = false;
        EventActor = true;
        collidesWithOtherCharacters.Value = false;
        state = new(ActorState.Await, $"{nameof(ActorState)}[{sourceFriend.Name}]");
    }
    #endregion

    #region social
    internal static readonly NPC dummySpeaker = new(
        new AnimatedSprite(Game1.temporaryContent, "Characters\\Abigail", 0, 16, 16),
        Vector2.Zero,
        "",
        0,
        "???",
        Game1.staminaRect,
        eventActor: false
    )
    {
        portraitOverridden = true,
    };

    public Dialogue GetHaggleDialogue(NPC dummySpeaker, CustomerDialogueKind kind, params object[] substitutions)
    {
        dummySpeaker.Name = sourceFriend.Name;
        dummySpeaker.Portrait = Portrait;
        dummySpeaker.displayName = displayName;
        string? rawDialogueText = null;
        if (sourceFriend.BaseCxData?.TryGetDialogueText(kind, out string? dialogueText) ?? false)
        {
            rawDialogueText = string.Format(TokenParser.ParseText(dialogueText) ?? dialogueText, substitutions);
        }
        rawDialogueText ??= AssetManager.LoadString(kind.ToString(), substitutions);

        // fix emotions
        string? specificEmotion = null;
        int maxIdx = rawDialogueText.Length - 1;
        for (int i = maxIdx; i >= 0; i--)
        {
            if (rawDialogueText[i] == '$' && i < maxIdx)
            {
                specificEmotion = rawDialogueText[i..];
                rawDialogueText = rawDialogueText.Replace(specificEmotion, "");
                break;
            }
            else if (!char.IsDigit(rawDialogueText[i]))
            {
                break;
            }
        }

        Dialogue dialogue = new(
            dummySpeaker,
            string.Concat(AssetManager.Asset_Strings, ":", kind.ToString()),
            rawDialogueText
        );
        if (specificEmotion != null)
            dialogue.CurrentEmotion = specificEmotion;

        return dialogue;
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
    }

    private readonly StateManager<ActorState> state;

    private readonly float chanceToBuy = 0.2f + 0.3f * Random.Shared.NextSingle();
    private int browsedCount = 0;
    public ForSaleTarget? ForSale
    {
        get => field;
        set
        {
            field?.HeldBy = null;
            value?.HeldBy = this;
            field = value;
            if (value == null)
                ForSaleBrowsing = null;
        }
    }
    public (Point, int)? ForSaleBrowsing;

    public bool IsLeavingOrFinished => state.Current == ActorState.Leaving || state.Current == ActorState.Finished;

    internal void EnterShop(GameLocation location)
    {
        state.Current = ActorState.Await;
        currentLocation = location;
        reloadSprite(true);
        setTileLocation(entryPoint.ToVector2());
        faceDirection(0);
    }

    public override void reloadSprite(bool onlyAppearance = false)
    {
        base.reloadSprite(true);
        sourceFriend.ApplyChangesToActor(this);
        Portrait ??= Game1.mouseCursors;
    }

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

            List<ForSaleTarget>[] rankedForSaleTargets =
            [
                [], // love
                [], // like
                [], // neutral
                [], // dislike
                [], // hated
            ];
            foreach (ForSaleTarget forSale in availableForSale)
            {
                int giftTaste = sourceFriend.GetGiftTasteForSaleItem(forSale);
                int seq = giftTaste switch
                {
                    gift_taste_love => 0,
                    gift_taste_stardroptea => 0,
                    gift_taste_like => 1,
                    gift_taste_neutral => 2,
                    gift_taste_dislike => 3,
                    _ => 4,
                };
                rankedForSaleTargets[seq].Add(forSale);
            }
            ForSaleTarget? nextForSale = null;
            foreach (List<ForSaleTarget> giftTasteForSale in rankedForSaleTargets)
            {
                if (giftTasteForSale.Count > 0)
                {
                    nextForSale = Random.Shared.ChooseFrom(giftTasteForSale);
                    break;
                }
            }

            if (nextForSale == null)
            {
                if (availableForSaleHeld == null)
                {
                    LeavingTheShop();
                }
                return;
            }

            browsedCount++;
            if (nextForSale == ForSale)
            {
                return;
            }
            ForSale = nextForSale;
            ForSaleBrowsing = Random.Shared.ChooseFrom(ForSale.BrowseAround);
            state.Current = ActorState.Move;
            controller = new PathFindController(
                this,
                currentLocation,
                ForSaleBrowsing.Value.Item1,
                ForSaleBrowsing.Value.Item2,
                ReachedForSaleItem
            )
            {
                nonDestructivePathing = true,
            };
        }
    }

    private void ReachedForSaleItem(Character c, GameLocation location)
    {
        state.Current = ActorState.Considering;
        state.SetNext(ActorState.Decide, Random.Shared.NextSingle() * 750, DecideBuy);
    }

    private void DecideBuy()
    {
        if (ForSale != null)
        {
            int giftTaste = sourceFriend.GetGiftTasteForSaleItem(ForSale);
            if (giftTaste == gift_taste_hate)
            {
                doEmote(angryEmote);
                LeavingTheShop();
                return;
            }
            float bonusChanceToBuy = giftTaste == gift_taste_love ? 0.2f : 0f;
            if (Random.Shared.NextSingle() < chanceToBuy + bonusChanceToBuy + browsedCount * 0.1f)
            {
                doEmote(giftTaste == gift_taste_love ? heartEmote : exclamationEmote);
                state.SetNext(ActorState.Buy, 500);
                return;
            }
        }

        ForSale = null;
        state.SetNext(ActorState.Await, Random.Shared.NextSingle() * 750);
    }

    internal void LeavingTheShop()
    {
        if (IsLeavingOrFinished)
            return;
        ForSale = null;
        state.Current = ActorState.Leaving;
        controller = new PathFindController(this, currentLocation, entryPoint, -1, LeftTheShop)
        {
            nonDestructivePathing = true,
        };
    }

    private void LeftTheShop(Character c, GameLocation location)
    {
        ModEntry.Log($"LeftTheShop {c.displayName}");
        ForSale = null;
        state.SetAndLock(ActorState.Finished);
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
        else if (state.Current == ActorState.Move && controller == null)
        {
            ModEntry.Log($"Actor '{Name}' stuck in Move, do force unstuck.", LogLevel.Warn);
            if (ForSale != null && ForSaleBrowsing != null && TilePoint != ForSaleBrowsing.Value.Item1)
            {
                state.Current = ActorState.Considering;
                setTilePosition(ForSaleBrowsing.Value.Item1);
                faceDirection(ForSaleBrowsing.Value.Item2);
                ReachedForSaleItem(this, currentLocation);
            }
            else
            {
                LeavingTheShop();
            }
        }
    }

    public void UpdateDuringReporting(GameTime time, GameLocation location)
    {
        IClickableMenu menu = Game1.activeClickableMenu;
        DynamicMethods.Set_Game1_activeClickableMenu(null);
        LeavingTheShop();
        update(time, location);
        DynamicMethods.Set_Game1_activeClickableMenu(menu);
    }

    public override void DrawShadow(SpriteBatch b)
    {
        if (sourceFriend.ShowShadow)
            base.DrawShadow(b);
    }
    #endregion
}
