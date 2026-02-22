using Merchant.Misc;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Merchant.Management;

internal sealed record HagglePatternSlice(HagglePatternSlice.PatternFn Fn, float Start, float End)
{
    public delegate float PatternFn(float prog);

    public float Get(float prog)
    {
        float value = Fn(Start + prog * (End - Start));
        return value;
    }
}

internal sealed record HagglePattern(HagglePatternSlice[] Slices)
{
    private int currentIdx = 0;

    public float Get(float prog)
    {
        HagglePatternSlice slice = Slices[currentIdx];
        if (prog > slice.End)
        {
            currentIdx = (currentIdx + 1) % Slices.Length;
            slice = Slices[currentIdx];
        }
        else if (prog < slice.Start)
        {
            currentIdx = (currentIdx - 1) % Slices.Length;
            slice = Slices[currentIdx];
        }
        return slice.Get(prog);
    }

    internal static HagglePattern Default()
    {
        // TODO: piecewise funcs?
        return new HagglePattern([new((prog) => MathF.Pow(prog, 2), 0f, 1f)]);
    }
}

public sealed record ShopkeepHaggle(Farmer Player, NPC Buyer, Item ForSale, float MinMult, float MaxMult, int MaxCount)
{
    public enum HaggleState
    {
        Begin,
        Increase,
        Decrease,
        Picked,
        DoneSuccess,
        DoneFailed,
    }

    private const double pointerPeriodMS = 1500.0;
    private const double pickedPauseMS = 1000.0;
    private const int totalPitch = 12;

    public readonly StateManager<HaggleState> state = new(HaggleState.Begin);
    public bool IsReadyToStart =>
        state.Current == HaggleState.Begin && Game1.activeClickableMenu is DialogueBox { transitioning: false };
    public bool IsDone => state.Current == HaggleState.DoneSuccess || state.Current == HaggleState.DoneFailed;
    private float pointer = 0;
    private HagglePattern hagglePattern = HagglePattern.Default();

    public int Count { get; private set; } = 0;
    public float TargetPointer { get; private set; } = 0.25f + Random.Shared.NextSingle() / 2;
    private float nextTargetPointer = -1;
    public float PickedMult
    {
        get => field;
        private set
        {
            field = value;
            if (value >= 0)
                PickedPrice = (int)MathF.Ceiling(ForSale.sellToStorePrice(Player.UniqueMultiplayerID) * PickedMult);
        }
    } = -1;
    public int PickedPrice { get; private set; } = -1;
    private const string haggleDialogueKey = $"{ModEntry.ModId}_haggle";

    #region drawing
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

    private Rectangle buyerMugShotRect = Rectangle.Empty;

    private static readonly Rectangle sourceRectHaggleBarIconBox = new(293, 360, 26, 24);
    private static readonly Rectangle sourceRectHaggleBarSlide = new(319, 360, 1, 24);
    private static readonly Rectangle sourceRectHaggleBarCap = new(323, 360, 6, 24);
    private static readonly Rectangle sourceRectHagglePointerA = new(310, 392, 16, 16);
    private static readonly Rectangle sourceRectHagglePointerB = new(294, 392, 16, 16);

    private int pointerPitch = -1;
    private ICue? pointerSound;
    #endregion

    public static ShopkeepHaggle Make(Farmer player, NPC buyer, Item forSale)
    {
        ShopkeepHaggle newHaggle = new(player, buyer, forSale, 0.5f, 1.5f, 3);
        newHaggle.Initialize();
        return newHaggle;
    }

    private void Initialize()
    {
        SetNextDialogue(AssetManager.LoadString("speak.haggle.ask", ForSale.DisplayName), true);
        buyerMugShotRect = Buyer.getMugShotSourceRect();
        CalculateBounds();
    }

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
        CalculateTargetPointerBounds();
    }

    private void CalculateTargetPointerBounds(bool useNextTargetPnt = false)
    {
        targetPointerPos = new(
            Utility.Lerp(
                haggleBarSlideBounds.X - buyerMugShotRect.Width * 4,
                haggleBarSlideBounds.X + haggleBarSlideWidth,
                useNextTargetPnt ? Utility.Lerp(nextTargetPointer, TargetPointer, state.TimerProgress) : TargetPointer
            ),
            haggleBarSlideBounds.Y
        );
    }

    private void SetNextDialogue(string dialogueStr, bool transitioning = false)
    {
        Game1.activeClickableMenu = new DialogueBox(new Dialogue(Buyer, haggleDialogueKey, dialogueStr))
        {
            showTyping = false,
            transitioning = transitioning,
            transitionInitialized = !transitioning,
        };
    }

    public bool BeginHaggleRound()
    {
        if (IsDone)
        {
            Game1.exitActiveMenu();
            return false;
        }
        if (state.Current != HaggleState.Begin)
        {
            return true;
        }

        if (nextTargetPointer > -1)
        {
            TargetPointer = nextTargetPointer;
            CalculateTargetPointerBounds();
            nextTargetPointer = -1;
        }

        if (Count > 0)
            SetNextDialogue(AssetManager.LoadString("speak.haggle.ask", ForSale.DisplayName), false);
        pointer = 0f;
        pointerPitch = -1;
        state.Current = HaggleState.Increase;
        state.SetNext(HaggleState.Decrease, pointerPeriodMS, State_DecreaseStart);
        Count++;
        return true;
    }

    public void Update(GameTime time)
    {
        state.Update(time);
        switch (state.Current)
        {
            case HaggleState.Increase:
                pointer = hagglePattern.Get((float)(1.0 - state.TimerProgress));
                break;
            case HaggleState.Decrease:
                pointer = hagglePattern.Get(state.TimerProgress);
                break;
            case HaggleState.Picked:
                if (nextTargetPointer > -1)
                    CalculateTargetPointerBounds(true);
                break;
        }

        int pitch = (int)(pointer * totalPitch) * 100;
        if (pointerPitch != pitch)
        {
            pointerSound?.Stop(AudioStopOptions.Immediate);
            Game1.playSound("flute", pitch, out pointerSound);
            pointerPitch = pitch;
        }
    }

    public void Pick()
    {
        state.Current = HaggleState.Picked;
        PickedMult = Utility.Lerp(MinMult, MaxMult, pointer);
        if (pointer <= TargetPointer)
        {
            state.SetNext(HaggleState.DoneSuccess, pickedPauseMS);
            Game1.playSound("reward");
            SetNextDialogue(AssetManager.LoadString("speak.haggle.success", ForSale.DisplayName, PickedPrice));
        }
        else if (Count >= MaxCount)
        {
            state.SetNext(HaggleState.DoneFailed, pickedPauseMS);
            Game1.playSound("fishEscape");
            SetNextDialogue(AssetManager.LoadString("speak.haggle.fail", ForSale.DisplayName, PickedPrice));
        }
        else
        {
            nextTargetPointer = Utility.Lerp(TargetPointer, pointer, Random.Shared.NextSingle());
            Game1.playSound("smallSelect");
            state.SetNext(HaggleState.Begin, pickedPauseMS);
            SetNextDialogue(AssetManager.LoadString("speak.haggle.haggle", ForSale.DisplayName, PickedPrice));
        }
    }

    private void State_DecreaseStart(HaggleState oldState, HaggleState newState)
    {
        state.SetNext(Count >= MaxCount ? HaggleState.DoneFailed : HaggleState.Begin, pointerPeriodMS);
    }

    private static readonly Vector2 HaggleDrawPos = new(12, 12 + 64);

    public void Draw(SpriteBatch b)
    {
#if DEBUG
        b.Draw(Game1.staminaRect, new Rectangle(0, 64, Game1.viewport.Width, 64), Color.Black * 0.5f);
        b.DrawString(
            Game1.dialogueFont,
            $"Haggle {Count} {state}: Pick {Utility.Lerp(MinMult, MaxMult, pointer):0.00} ({MinMult:0.00} - {MaxMult:0.00}, Target {TargetPointer:0.00})",
            HaggleDrawPos,
            Color.White
        );
#endif

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
        ForSale.drawInMenu(b, haggleBarIconPos, 1f + pointer / 2f);

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
        float rotate = state.Current == HaggleState.Picked ? 4 * MathF.PI * state.TimerProgress : 0f;
        b.Draw(
            Game1.mouseCursors,
            new(pointerPos, haggleBarSlideBounds.Y + 16 + sourceRectHagglePointerA.Height * 2),
            pointer > TargetPointer ? sourceRectHagglePointerA : sourceRectHagglePointerB,
            Color.White,
            rotate,
            new(sourceRectHagglePointerA.Width / 2, sourceRectHagglePointerA.Height / 2),
            4f,
            SpriteEffects.None,
            1f
        );
    }
}
