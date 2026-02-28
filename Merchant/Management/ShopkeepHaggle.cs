using Merchant.Misc;
using Merchant.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Extensions;
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
    public const float MAX_MULT = 1.5f;

    #region make
    public static ShopkeepHaggle Make(Farmer player, CustomerActor buyer, ForSaleTarget forSaleTarget, float decorBonus)
    {
        float minMult = MIN_MULT + decorBonus / 2f;
        float maxMult = MAX_MULT + decorBonus;

        Func<float, float> PatternFn = Random.Shared.NextBool() ? Ease.InQuad : Ease.InOutQuad;

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

    private const double pickedPauseMS = 1000.0;
    private const int totalPitch = 12;
    private const int maxTries = 3;

    public readonly StateManager<HaggleState> state = new(HaggleState.Begin);
    public bool IsReadyToStart =>
        state.Current == HaggleState.Begin && Game1.activeClickableMenu is DialogueBox { transitioning: false };
    private float pointer = 0;

    public int Tries { get; private set; } = 0;
    private float targetPointer = Buyer.GetHaggleBaseTargetPointer(ForSale);
    private float targetOverRange = 0.1f + (0.15f + Buyer.GetHaggleTargetOverRange()) * Random.Shared.NextSingle();
    private float nextTargetPointer = -1;
    private readonly uint basePrice = (uint)Math.Max(ForSale.Thing.sellToStorePrice(Player.UniqueMultiplayerID), 1);
    private uint leewayPrice = 0;
    private float allowancePointer = -1f;

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
        allowancePointer = targetPointer + targetOverRange;
        if (Tries > 0)
            SetNextDialogue(CxDialogueKind.Haggle_Ask, PntToPrice(targetPointer));
        pointer = 0f;
        pointerPitch = -1;
        state.Current = HaggleState.Increase;
        state.SetNext(HaggleState.Decrease, ModEntry.config.HaggleSpeed, State_DecreaseStart);
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

        if (pickedPrice <= targetPrice + leewayPrice)
        {
            SetupHaggleSuccess(Math.Min(pickedPrice, targetPrice));
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
                targetOverRange -= nextTargetPointer - targetPointer;
                ModEntry.Log($"TargetPointer {targetPointer} -> {nextTargetPointer}");
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

    public void Giveup()
    {
        if (state.Current == HaggleState.Picked)
            return;

        SetupHaggleSuccess(PntToPrice(0f));
    }

    private void SetupHaggleSuccess(uint pickedPrice)
    {
        state.SetNext(HaggleState.Done, pickedPauseMS, DoneAndLock);
        ForSale.Sold = SoldRecord.Make(Buyer.Name, pickedPrice, ForSale.Thing);
        Game1.playSound("reward");
        SetNextDialogue(CxDialogueKind.Haggle_Success, pickedPrice);
    }

    private void SetupHaggleFailed(uint pickedPrice)
    {
        state.SetNext(HaggleState.Done, pickedPauseMS, DoneAndLock);
        Game1.playSound("fishEscape");
        SetNextDialogue(CxDialogueKind.Haggle_Fail, pickedPrice);
    }

    private void DoneAndLock()
    {
        state.SetAndLock(HaggleState.Done);
    }

    private void State_DecreaseStart()
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
    private static readonly Vector2 hagglePointerOrigin = new(
        sourceRectHagglePointerA.Width / 2,
        sourceRectHagglePointerA.Height / 2
    );
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

        leewayPrice = (uint)Math.Ceiling(basePrice * buyerMugShotRect.Width * 2f / haggleBarSlideWidth);
        ModEntry.Log($"leewayPrice {leewayPrice}");

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
                - buyerMugShotRect.Width * 2,
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
        ForSale.Thing.drawInMenu(b, haggleBarIconPos, 1f);
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

        // allowance pointer
        if (allowancePointer > -1)
        {
            float allowancePos = haggleBarSlideBounds.X + allowancePointer * haggleBarSlideWidth;
            b.Draw(
                Game1.mouseCursors,
                new(allowancePos, haggleBarSlideBounds.Y + 16 + sourceRectHagglePointerA.Height * 2),
                sourceRectHagglePointerA,
                Color.White * 0.4f,
                0f,
                hagglePointerOrigin,
                4f,
                SpriteEffects.None,
                1f
            );
        }

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
            PntToPrice(pointer) <= (PntToPrice(targetPointer) + leewayPrice)
                ? sourceRectHagglePointerB
                : sourceRectHagglePointerA,
            Color.White,
            rotate,
            hagglePointerOrigin,
            4f,
            SpriteEffects.None,
            1f
        );
    }
    #endregion
}
