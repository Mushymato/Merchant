using Merchant.Misc;
using Merchant.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.Triggers;

namespace Merchant.Management;

public sealed record ShopkeepHaggle(
    Farmer Player,
    CustomerActor Buyer,
    ForSaleTarget ForSale,
    float MinMult,
    float MaxMult,
    float ThemeBoost,
    Func<float, float> PatternFn
)
{
    public const float MIN_MULT = 0.5f;
    public const float MAX_MULT = 1.5f;

    #region make
    public static ShopkeepHaggle Make(Farmer player, CustomerActor buyer, ForSaleTarget forSale, float decorBonus)
    {
        GetMinAndMaxMult(decorBonus, out float minMult, out float maxMult);

        Func<float, float> patternFn = Random.Shared.NextBool() ? Ease.InQuad : Ease.InOutQuad;

        ShopkeepHaggle newHaggle = new(player, buyer, forSale, minMult, maxMult, forSale.Boost?.Value ?? 0f, patternFn);
        newHaggle.SetNextDialogue(CustomerDialogueKind.Haggle_Ask, newHaggle.PntToPrice(newHaggle.targetPointer));
        newHaggle.CalculateBounds();

        return newHaggle;
    }

    internal static void GetMinAndMaxMult(float decorBonus, out float minMult, out float maxMult)
    {
        minMult = MIN_MULT + decorBonus / 2f;
        maxMult = MAX_MULT + decorBonus;
    }

    internal static readonly NPC dummySpeaker = new(
        new AnimatedSprite("Characters\\Abigail", 0, 16, 16),
        Vector2.Zero,
        "",
        0,
        "???",
        Game1.staminaRect,
        eventActor: true
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

    private const double pickedPauseMS = 1500.0;
    private const int totalPitch = 12;
    private const int maxTries = 3;

    public readonly StateManager<HaggleState> state = new(HaggleState.Begin, nameof(HaggleState));
    public bool IsReadyToStart =>
        state.Current == HaggleState.Begin && haggleDialogueBox is DialogueBox { transitioning: false };

    private float pointer = 0f;

    public float periodMS = ModEntry.config.HaggleSpeed / (1f - ThemeBoost * 0.25f);
    public int Tries { get; private set; } = 0;
    private float targetPointer = Buyer.sourceFriend.GetHaggleBaseTargetPointer(ForSale);
    private float targetOverRange = Buyer.sourceFriend.GetHaggleTargetOverRange(ForSale);
    private float nextTargetPointer = -1;
    private readonly uint basePrice = ForSale.GetBasePrice(Player);
    private uint leewayPrice = 0;
    private float allowancePointer = -1f;

    public uint PntToPrice(float pnt) => CalcPntToPrice(basePrice, pnt, MinMult, MaxMult, ThemeBoost);

    public static uint CalcPntToPrice(uint basePrice, float pnt, float minMult, float maxMult, float themeBoost) =>
        (uint)Math.Ceiling(Utility.Lerp(minMult + themeBoost, maxMult, pnt) * basePrice);

    public float PntToXPos(float pnt) =>
        Utility.Lerp(haggleBarSlideBounds.Left + ThemeBoost * haggleBarSlideWidth, haggleBarSlideBounds.Right, pnt);

    private int pointerPitch = -1;
    private ICue? pointerSound;
    internal DialogueBox? haggleDialogueBox = null;

    private void SetNextDialogue(CustomerDialogueKind kind, uint price, bool transitioning = false)
    {
        haggleDialogueBox = new DialogueBox(
            Buyer.GetHaggleDialogue(dummySpeaker, kind, ForSale.Thing.DisplayName, price)
        )
        {
            showTyping = false,
            transitioning = transitioning,
            transitionInitialized = !transitioning,
        };
        if (Buyer.Portrait == Game1.mouseCursors)
        {
            haggleDialogueBox.characterDialogue.showPortrait = false;
        }
    }

    private void BeginHaggleRound()
    {
        if (ModEntry.config.HaggleAutoClick)
        {
            Giveup();
            return;
        }
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
            SetNextDialogue(CustomerDialogueKind.Haggle_Ask, PntToPrice(targetPointer));
        pointer = 0f;
        pointerPitch = -1;
        state.Current = HaggleState.Increase;
        state.SetNext(HaggleState.Decrease, periodMS, State_DecreaseStart);
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
                Player.Stamina -= ShopkeepGame.STAMINA_COST_HAGGLING;
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
        ModEntry.Log(
            $"Pick: {pointer}({pickedPrice}) vs {targetPointer}({targetPrice}) {leewayPrice} {targetOverRange}"
        );

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
                nextTargetPointer = targetPointer + delta / 3 + 2 * delta * Random.Shared.NextSingle() / 3;
                targetOverRange -= nextTargetPointer - targetPointer;
                state.SetNext(HaggleState.Begin, pickedPauseMS);
                SetNextDialogue(CustomerDialogueKind.Haggle_Compromise, pickedPrice);
            }
            else
            {
                state.SetNext(HaggleState.Begin, pickedPauseMS);
                SetNextDialogue(CustomerDialogueKind.Haggle_Overpriced, pickedPrice);
            }
            Game1.playSound("smallSelect");
        }
    }

    public void Giveup()
    {
        if (state.Current == HaggleState.Picked)
            return;

        pointer = targetPointer / 2;
        state.Current = HaggleState.Picked;
        SetupHaggleSuccess(PntToPrice(pointer));
    }

    private void SetupHaggleSuccess(uint pickedPrice)
    {
        state.SetNext(HaggleState.Done, pickedPauseMS, DoneAndLock);
        ForSale.Sold = SoldRecord.Make(Buyer, pickedPrice, ForSale.Thing);
        Game1.playSound("reward");
        SetNextDialogue(CustomerDialogueKind.Haggle_Success, pickedPrice);
        TriggerActionManager.Raise(
            GameDelegates.Trigger_Merchant_Sold,
            location: Buyer.currentLocation,
            player: Player,
            targetItem: ForSale.Thing
        );
        if (ModEntry.HasBETAS)
            TriggerActionManager.Raise(
                "Spiderbuttons.BETAS_ItemShipped",
                location: Buyer.currentLocation,
                player: Player,
                targetItem: ForSale.Thing
            );
    }

    private void SetupHaggleFailed(uint pickedPrice)
    {
        state.SetNext(HaggleState.Done, pickedPauseMS, DoneAndLock);
        Game1.playSound("fishEscape");
        SetNextDialogue(CustomerDialogueKind.Haggle_Fail, pickedPrice);
    }

    private void DoneAndLock()
    {
        state.SetAndLock(HaggleState.Done);
    }

    private void State_DecreaseStart()
    {
        state.SetNext(HaggleState.Begin, periodMS);
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
    private Rectangle haggleBarBoostedFillBounds = Rectangle.Empty;
    private Vector2 haggleBarCapPos = Vector2.Zero;
    private Vector2 targetPointerPos = Vector2.Zero;
    private Rectangle remainingTriesBounds = Rectangle.Empty;
    private Rectangle buyerMugShotRect = Buyer.sourceFriend.MugShotSourceRect;
    private Rectangle sourceRectHaggleBarIconBox = sourceRectHaggleBarNormalIconBox;

    private static readonly Rectangle sourceRectHaggleBarNormalIconBox = new(293, 360, 26, 24);
    private static readonly Rectangle sourceRectHaggleBarBoostedIconBox = new(163, 399, 26, 24);
    private static readonly Rectangle sourceRectHaggleBarBoostedFill = new(432, 439, 9, 9);
    private static readonly Rectangle sourceRectHaggleBarSlide = new(319, 360, 1, 24);
    private static readonly Rectangle sourceRectHaggleBarCap = new(323, 360, 6, 24);
    private static readonly Rectangle sourceRectHagglePointerA = new(310, 392, 16, 16);
    private static readonly Rectangle sourceRectHagglePointerB = new(294, 392, 16, 16);
    private static readonly Rectangle sourceRectTargetOver = new(325, 449, 5, 15);
    private static readonly Rectangle sourceRectRemainingTriesBox = new(0, 320, 60, 60);

    private static readonly Vector2 hagglePointerOrigin = new(
        sourceRectHagglePointerA.Width / 2,
        sourceRectHagglePointerA.Height / 2
    );

    private static readonly Vector2 targetOverOrigin = new(
        sourceRectTargetOver.Width / 2,
        sourceRectTargetOver.Height / 2
    );

    internal void CalculateBounds()
    {
        Vector2 position = Utility.getTopLeftPositionForCenteringOnScreen(haggleBarTotalWidth, haggleBarHeight, 0, 0);
        haggleBarIconBoxPos = new(
            position.X + ModEntry.config.HaggleUIOffset.X,
            MathF.Min(position.Y, Game1.viewport.Height - 600 * (Game1.options.uiScale / Game1.options.zoomLevel))
                + ModEntry.config.HaggleUIOffset.Y
        );
        haggleBarIconPos = new(haggleBarIconBoxPos.X + 16, haggleBarIconBoxPos.Y + 16);
        haggleBarSlideBounds = new(
            (int)haggleBarIconBoxPos.X + haggleBarIconWidth,
            (int)haggleBarIconBoxPos.Y,
            haggleBarSlideWidth,
            haggleBarHeight
        );
        haggleBarCapPos = new(haggleBarIconBoxPos.X + haggleBarIconWidth + haggleBarSlideWidth, haggleBarIconBoxPos.Y);
        remainingTriesBounds = new(haggleBarSlideBounds.X, haggleBarSlideBounds.Y - 72, 196, 80);

        if (ThemeBoost > 0)
        {
            sourceRectHaggleBarIconBox = sourceRectHaggleBarBoostedIconBox;
            haggleBarBoostedFillBounds = new(
                haggleBarSlideBounds.Left - 12,
                haggleBarSlideBounds.Top + 16,
                (int)MathF.Ceiling(ThemeBoost * (haggleBarSlideWidth + 12)),
                64
            );
        }
        else
        {
            sourceRectHaggleBarIconBox = sourceRectHaggleBarNormalIconBox;
            haggleBarBoostedFillBounds = Rectangle.Empty;
        }

        leewayPrice = (uint)Math.Ceiling(basePrice * buyerMugShotRect.Width * 2f / haggleBarSlideWidth);

        CalculateTargetPointerBounds();
    }

    private void CalculateTargetPointerBounds(bool useNextTargetPnt = false)
    {
        targetPointerPos = new(
            PntToXPos(
                useNextTargetPnt ? Utility.Lerp(nextTargetPointer, targetPointer, state.TimerProgress) : targetPointer
            )
                - buyerMugShotRect.Width * 2,
            haggleBarSlideBounds.Y - 16
        );
    }

    public void Draw(SpriteBatch b)
    {
        // dialogue

        haggleDialogueBox?.draw(b);
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
        // pointer boost
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            sourceRectHaggleBarBoostedFill,
            haggleBarBoostedFillBounds.X,
            haggleBarBoostedFillBounds.Y,
            haggleBarBoostedFillBounds.Width,
            haggleBarBoostedFillBounds.Height,
            Color.White,
            4,
            false
        );

        // allowance pointer
        if (allowancePointer > 0 && allowancePointer < 1)
        {
            float allowancePos = PntToXPos(allowancePointer);
            b.Draw(
                Game1.mouseCursors,
                new(allowancePos, haggleBarSlideBounds.Y + 30 + targetOverOrigin.Y * 2),
                sourceRectTargetOver,
                Color.White,
                0f,
                targetOverOrigin,
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
        float pointerPos = PntToXPos(pointer);
        float rotate =
            state.Current == HaggleState.Picked && ForSale.Sold != null ? 6 * MathF.PI * state.TimerProgress : 0f;
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
