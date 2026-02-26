using Merchant.Misc;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Merchant.Management;

public sealed record ShopkeepHaggle(
    Farmer Player,
    CustomerActor Buyer,
    ForSaleTarget ForSale,
    float MinMult,
    float MaxMult,
    Func<float, float> PatternFn
)
{
    public const float MIN_MULT = 0.5f;
    public const float MAX_MULT_DELTA = 1f;

    #region make
    public static ShopkeepHaggle Make(Farmer player, CustomerActor buyer, ForSaleTarget forSaleTarget, float decorBonus)
    {
        float minMult = MIN_MULT + decorBonus;
        float maxMult = minMult + MAX_MULT_DELTA;

        ModEntry.LogDebug($"Haggle Mult: {minMult} -> {maxMult}");

        int whichFn = Random.Shared.Next(0, 3);
        Func<float, float> PatternFn = whichFn switch
        {
            1 => Ease.OutQuad,
            2 => Ease.InOutCubic,
            _ => Ease.InQuad,
        };

        ShopkeepHaggle newHaggle = new(player, buyer, forSaleTarget, minMult, maxMult, PatternFn);
        newHaggle.SetNextDialogue(CxDialogueKind.Haggle_Ask, newHaggle.PntToPrice(newHaggle.targetPointer));
        newHaggle.CalculateBounds();
        return newHaggle;
    }

    internal static readonly NPC dummySpeaker = new(
        new AnimatedSprite("Characters\\Abigail", 0, 16, 16),
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
    #endregion

    #region haggle loop
    public enum HaggleState
    {
        Begin,
        Increase,
        Decrease,
        Picked,
        Done,
    }

    private const double pointerPeriodMS = 1500.0;
    private const double pickedPauseMS = 1000.0;
    private const int totalPitch = 12;
    private const int maxTries = 3;

    public readonly StateManager<HaggleState> state = new(HaggleState.Begin);
    public bool IsReadyToStart =>
        state.Current == HaggleState.Begin && Game1.activeClickableMenu is DialogueBox { transitioning: false };
    private float pointer = 0;

    public int Tries { get; private set; } = 0;
    private float targetPointer = Buyer.GetHaggleBaseTargetPointer(ForSale);
    private float targetOverRange = 0.25f * Random.Shared.NextSingle() + Buyer.GetHaggleTargetOverRange();
    private float nextTargetPointer = -1;
    private readonly uint basePrice = (uint)Math.Max(ForSale.Thing.sellToStorePrice(Player.UniqueMultiplayerID), 1);

    public uint PntToPrice(float pnt) => (uint)Math.Ceiling(Utility.Lerp(MinMult, MaxMult, pnt) * basePrice);

    private int pointerPitch = -1;
    private ICue? pointerSound;

    private void SetNextDialogue(CxDialogueKind kind, uint price, bool transitioning = false)
    {
        Game1.activeClickableMenu = new DialogueBox(
            Buyer.GetMerchantDialogue(dummySpeaker, kind, ForSale.Thing.DisplayName, price)
        )
        {
            showTyping = false,
            transitioning = transitioning,
            transitionInitialized = !transitioning,
        };
    }

    private void BeginHaggleRound()
    {
        if (HaggleExpired())
        {
            state.Current = HaggleState.Picked;
            SetupHaggleFailed(PntToPrice(targetPointer));
            return;
        }
        if (nextTargetPointer > -1)
        {
            targetPointer = nextTargetPointer;
            CalculateTargetPointerBounds();
            nextTargetPointer = -1;
        }

        if (Tries > 0)
            SetNextDialogue(CxDialogueKind.Haggle_Ask, PntToPrice(targetPointer));
        pointer = 0f;
        pointerPitch = -1;
        state.Current = HaggleState.Increase;
        state.SetNext(HaggleState.Decrease, pointerPeriodMS, State_DecreaseStart);
        Tries++;
    }

    public bool Update(GameTime time)
    {
        state.Update(time);
        float targetPtrBound = targetPointer;
        float nextPointer = pointer;
        switch (state.Current)
        {
            case HaggleState.Done:
                Buyer.LeavingTheShop();
                Player.Stamina -= 4;
                Game1.exitActiveMenu();
                return true;
            case HaggleState.Begin:
                BeginHaggleRound();
                return false;
            case HaggleState.Picked:
                if (nextTargetPointer > -1)
                    CalculateTargetPointerBounds(true);
                return false;
            case HaggleState.Increase:
                targetPtrBound -= 0.1f;
                nextPointer = PatternFn((float)(1.0 - state.TimerProgress));
                break;
            case HaggleState.Decrease:
                targetPtrBound += 0.11f;
                nextPointer = PatternFn(state.TimerProgress);
                break;
        }

        bool preState = pointer <= targetPtrBound;
        pointer = nextPointer;
        bool postState = pointer <= targetPtrBound;
        if (preState != postState)
        {
            Game1.playSound("junimoKart_coin");
        }

        int pitch = (int)(pointer * totalPitch) * 100;
        if (pointerPitch != pitch)
        {
            pointerSound?.Stop(AudioStopOptions.Immediate);
            Game1.playSound("flute", pitch, out pointerSound);
            pointerSound.Volume = 0.1f * Game1.options.soundVolumeLevel;
            pointerPitch = pitch;
        }

        return false;
    }

    public bool HaggleExpired() => Tries >= maxTries;

    public void Pick()
    {
        if (state.Current == HaggleState.Picked)
            return;

        state.Current = HaggleState.Picked;
        uint pickedPrice = PntToPrice(pointer);
        uint targetPrice = PntToPrice(targetPointer);
        ModEntry.Log($"Pick: {pointer}({pickedPrice}) vs {targetPointer}({targetPrice})");

        if (pickedPrice <= targetPrice)
        {
            SetupHaggleSuccess(pickedPrice);
        }
        else if (HaggleExpired())
        {
            SetupHaggleFailed(pickedPrice);
        }
        else
        {
            float delta = pointer - targetPointer;
            if (delta <= targetOverRange)
            {
                nextTargetPointer = targetPointer + delta * Random.Shared.NextSingle();
                if (PntToPrice(nextTargetPointer) >= pickedPrice)
                {
                    SetupHaggleSuccess(pickedPrice);
                    return;
                }
                else
                {
                    targetOverRange -= nextTargetPointer - targetPointer;
                }
                state.SetNext(HaggleState.Begin, pickedPauseMS);
                SetNextDialogue(CxDialogueKind.Haggle_Compromise, pickedPrice);
            }
            else
            {
                state.SetNext(HaggleState.Begin, pickedPauseMS);
                SetNextDialogue(CxDialogueKind.Haggle_Overpriced, pickedPrice);
            }
            Game1.playSound("smallSelect");
        }
    }

    private void SetupHaggleSuccess(uint pickedPrice)
    {
        state.SetNext(HaggleState.Done, pickedPauseMS);
        ForSale.Sold = new(Buyer.Name, ForSale.Thing.QualifiedItemId, pickedPrice);
        Game1.playSound("reward");
        SetNextDialogue(CxDialogueKind.Haggle_Success, pickedPrice);
    }

    private void SetupHaggleFailed(uint pickedPrice)
    {
        state.SetNext(HaggleState.Done, pickedPauseMS);
        Game1.playSound("fishEscape");
        SetNextDialogue(CxDialogueKind.Haggle_Fail, pickedPrice);
    }

    private void State_DecreaseStart(HaggleState oldState, HaggleState newState)
    {
        state.SetNext(HaggleState.Begin, pickedPauseMS);
    }
    #endregion

    #region haggle draw
    private const int haggleBarTotalWidth = 1200;
    private const int haggleBarIconWidth = 104;
    private const int haggleBarCapWidth = 24;
    private const int haggleBarSlideWidth = haggleBarTotalWidth - haggleBarIconWidth - haggleBarCapWidth;
    private const int haggleBarHeight = 96;
    private Vector2 haggleBarIconBoxPos = Vector2.Zero;
    private Vector2 haggleBarIconPos = Vector2.Zero;
    private Rectangle haggleBarSlideBounds = Rectangle.Empty;
    private Vector2 haggleBarCapPos = Vector2.Zero;
    private Vector2 targetPointerPos = Vector2.Zero;
    private Rectangle remainingTriesBounds = Rectangle.Empty;
    private Rectangle buyerMugShotRect = Buyer.sourceFriend.Npc.getMugShotSourceRect();

    private static readonly Rectangle sourceRectHaggleBarIconBox = new(293, 360, 26, 24);
    private static readonly Rectangle sourceRectHaggleBarSlide = new(319, 360, 1, 24);
    private static readonly Rectangle sourceRectHaggleBarCap = new(323, 360, 6, 24);
    private static readonly Rectangle sourceRectHagglePointerA = new(310, 392, 16, 16);
    private static readonly Rectangle sourceRectHagglePointerB = new(294, 392, 16, 16);
    private static readonly Rectangle sourceRectRemainingTriesBox = new(0, 320, 60, 60);

    internal void CalculateBounds()
    {
        Vector2 position = Utility.getTopLeftPositionForCenteringOnScreen(haggleBarTotalWidth, haggleBarHeight, 0, 0);
        haggleBarIconBoxPos = new(position.X, MathF.Min(position.Y, Game1.viewport.Height - 600));
        haggleBarIconPos = new(haggleBarIconBoxPos.X + 16, haggleBarIconBoxPos.Y + 16);
        haggleBarSlideBounds = new(
            (int)haggleBarIconBoxPos.X + haggleBarIconWidth,
            (int)haggleBarIconBoxPos.Y,
            haggleBarSlideWidth,
            haggleBarHeight
        );
        haggleBarCapPos = new(haggleBarIconBoxPos.X + haggleBarIconWidth + haggleBarSlideWidth, haggleBarIconBoxPos.Y);
        remainingTriesBounds = new(haggleBarSlideBounds.X, haggleBarSlideBounds.Y - 72, 196, 80);

        CalculateTargetPointerBounds();
    }

    private void CalculateTargetPointerBounds(bool useNextTargetPnt = false)
    {
        targetPointerPos = new(
            Utility.Lerp(
                haggleBarSlideBounds.X,
                haggleBarSlideBounds.X + haggleBarSlideWidth,
                useNextTargetPnt ? Utility.Lerp(nextTargetPointer, targetPointer, state.TimerProgress) : targetPointer
            )
                - buyerMugShotRect.Width * 4,
            haggleBarSlideBounds.Y - 16
        );
    }

    public void Draw(SpriteBatch b)
    {
        // tries remaining
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            sourceRectRemainingTriesBox,
            remainingTriesBounds.X,
            remainingTriesBounds.Y,
            remainingTriesBounds.Width,
            remainingTriesBounds.Height,
            Color.White
        );
        for (int i = 0; i <= maxTries - Math.Max(Tries, 1); i++)
        {
            b.Draw(
                Game1.mouseCursors,
                new(remainingTriesBounds.X + 14 + i * 60, remainingTriesBounds.Y + 14),
                sourceRectHagglePointerA,
                Color.White,
                0f,
                Vector2.Zero,
                3f,
                SpriteEffects.None,
                1f
            );
        }
        // haggle bar
        b.Draw(
            Game1.mouseCursors,
            haggleBarIconBoxPos,
            sourceRectHaggleBarIconBox,
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            1f
        );
        ForSale.Thing.drawInMenu(b, haggleBarIconPos, 1f + pointer / 2f);
        b.Draw(
            Game1.mouseCursors,
            haggleBarSlideBounds,
            sourceRectHaggleBarSlide,
            Color.White,
            0f,
            Vector2.Zero,
            SpriteEffects.None,
            1f
        );
        b.Draw(
            Game1.mouseCursors,
            haggleBarCapPos,
            sourceRectHaggleBarCap,
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            1f
        );

        // haggle pointer
        b.Draw(
            Buyer.Sprite.Texture,
            targetPointerPos,
            buyerMugShotRect,
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            1f
        );
        float pointerPos = haggleBarSlideBounds.X + pointer * haggleBarSlideWidth;
        float rotate =
            state.Current == HaggleState.Picked && ForSale.Sold != null ? 4 * MathF.PI * state.TimerProgress : 0f;
        b.Draw(
            Game1.mouseCursors,
            new(pointerPos, haggleBarSlideBounds.Y + 16 + sourceRectHagglePointerA.Height * 2),
            pointer > targetPointer ? sourceRectHagglePointerA : sourceRectHagglePointerB,
            Color.White,
            rotate,
            new(sourceRectHagglePointerA.Width / 2, sourceRectHagglePointerA.Height / 2),
            4f,
            SpriteEffects.None,
            1f
        );
    }
    #endregion
}
