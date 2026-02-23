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
    float MaxMult
)
{
    #region make
    public static ShopkeepHaggle Make(Farmer player, CustomerActor buyer, ForSaleTarget forSaleTarget, float decorBonus)
    {
        float minMult = 0.8f + decorBonus;
        float maxMult = minMult + 1f;

        ModEntry.LogDebug($"Haggle Mult: {minMult} -> {maxMult}");

        ShopkeepHaggle newHaggle = new(player, buyer, forSaleTarget, minMult, maxMult);
        newHaggle.SetNextDialogue("Haggle_Ask", true);
        newHaggle.CalculateBounds();
        return newHaggle;
    }
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
    private float targetPointer = Buyer.GetHaggleBaseTargetPointer(ForSale.Thing);
    private readonly float targetOverRange = Buyer.GetHaggleTargetOverRange();
    private float nextTargetPointer = -1;
    public float PickedMult
    {
        get => field;
        private set
        {
            field = value;
            if (value >= 0)
                PickedPrice = (uint)
                    MathF.Ceiling(ForSale.Thing.sellToStorePrice(Player.UniqueMultiplayerID) * PickedMult);
        }
    } = MinMult;
    public uint PickedPrice { get; private set; } = (uint)ForSale.Thing.sellToStorePrice(Player.UniqueMultiplayerID);

    private int pointerPitch = -1;
    private ICue? pointerSound;

    private void SetNextDialogue(string key, bool transitioning = false)
    {
        Game1.activeClickableMenu = new DialogueBox(
            Buyer.GetMerchantDialogue(key, ForSale.Thing.DisplayName, PickedPrice)
        )
        {
            showTyping = false,
            transitioning = transitioning,
            transitionInitialized = !transitioning,
        };
    }

    private bool BeginHaggleRound()
    {
        if (nextTargetPointer > -1)
        {
            targetPointer = nextTargetPointer;
            CalculateTargetPointerBounds();
            nextTargetPointer = -1;
        }

        if (Tries > 0)
            SetNextDialogue("Haggle_Ask", false);
        pointer = 0f;
        pointerPitch = -1;
        state.Current = HaggleState.Increase;
        state.SetNext(HaggleState.Decrease, pointerPeriodMS, State_DecreaseStart);
        Tries++;
        return false;
    }

    public bool Update(GameTime time)
    {
        state.Update(time);
        switch (state.Current)
        {
            case HaggleState.Done:
                Buyer.DoneHaggling();
                Game1.exitActiveMenu();
                return true;
            case HaggleState.Begin:
                return BeginHaggleRound();
            case HaggleState.Picked:
                if (nextTargetPointer > -1)
                    CalculateTargetPointerBounds(true);
                return false;
            case HaggleState.Increase:
                pointer = MathF.Pow((float)(1.0 - state.TimerProgress), 2);
                break;
            case HaggleState.Decrease:
                pointer = MathF.Pow(state.TimerProgress, 2);
                break;
        }

        int pitch = (int)(pointer * totalPitch) * 100;
        if (pointerPitch != pitch)
        {
            pointerSound?.Stop(AudioStopOptions.Immediate);
            Game1.playSound("flute", pitch, out pointerSound);
            pointerPitch = pitch;
        }

        return false;
    }

    public bool HaggleExpired() => Tries >= maxTries;

    public void Pick()
    {
        state.Current = HaggleState.Picked;
        ModEntry.Log($"Pick: pointer {pointer} target {targetPointer} over {targetOverRange}");
        PickedMult = Utility.Lerp(MinMult, MaxMult, pointer);
        if (pointer <= targetPointer)
        {
            state.SetNext(HaggleState.Done, pickedPauseMS);
            ForSale.Sold = new(Buyer.Name, ForSale.Thing.QualifiedItemId, PickedPrice);
            Game1.playSound("reward");
            SetNextDialogue("Haggle_Success");
        }
        else if (HaggleExpired())
        {
            SetupHaggleFailed();
        }
        else
        {
            Game1.playSound("smallSelect");
            if (pointer - targetPointer <= targetOverRange)
            {
                nextTargetPointer = Utility.Lerp(targetPointer, pointer, Random.Shared.NextSingle());
                state.SetNext(HaggleState.Begin, pickedPauseMS);
                SetNextDialogue("Haggle_Compromise");
            }
            else
            {
                state.SetNext(HaggleState.Begin, pickedPauseMS);
                SetNextDialogue("Haggle_Overpriced");
            }
        }
    }

    private void State_DecreaseStart(HaggleState oldState, HaggleState newState)
    {
        if (HaggleExpired())
        {
            SetupHaggleFailed();
        }
        else
        {
            state.SetNext(HaggleState.Begin, pickedPauseMS);
        }
    }

    private void SetupHaggleFailed()
    {
        state.SetNext(HaggleState.Done, pickedPauseMS);
        Game1.playSound("fishEscape");
        SetNextDialogue("Haggle_Fail");
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
    private Rectangle buyerMugShotRect = Buyer.getMugShotSourceRect();

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
        remainingTriesBounds = new(haggleBarSlideBounds.X, haggleBarSlideBounds.Y - 60, 180, 60);

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
        for (int i = 0; i <= maxTries - Tries; i++)
        {
            b.Draw(
                Game1.mouseCursors,
                new(remainingTriesBounds.X + 6 + i * 60, remainingTriesBounds.Y + 6),
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
