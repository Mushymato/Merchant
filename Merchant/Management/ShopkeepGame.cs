using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Minigames;

namespace Merchant.Management;

public sealed class ShopkeepGame : IMinigame
{
    private readonly IModHelper helper;
    private readonly GameLocation location;
    private readonly Farmer player;

    #region state
    private TimeSpan gameTimer = TimeSpan.Zero;

    internal enum GameLoopState
    {
        Start,
        Browse,
        Haggle,
        Exit,
        Unloaded,
    }

    private readonly StateManager<GameLoopState> state = new(GameLoopState.Start);
    #endregion

    #region settings
    public bool doMainGameUpdates() => true;

    public string minigameId() => $"{ModEntry.ModId}:{nameof(ShopkeepGame)}";

    public bool overrideFreeMouseMovement() => true;
    #endregion

    #region setup teardown
    private ShopkeepGame(IModHelper helper, GameLocation location, Farmer player)
    {
        this.helper = helper;
        this.location = location;
        this.player = player;
        this.browsingHelper = new(location);
        changeScreenSize();
    }

    private void OnRendering(object? sender, RenderingEventArgs e)
    {
        // adjust minigame rendering timing by nulling it before render
        if (Game1.currentMinigame == this)
            Game1.currentMinigame = null;
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        if (state.Current == GameLoopState.Unloaded)
            return;
        // restore minigame after render
        if (Game1.currentMinigame == null)
        {
            Game1.currentMinigame = this;
            draw(e.SpriteBatch);
        }
    }

    public static ShopkeepGame? StartMinigame(
        IModHelper helper,
        GameLocation? location,
        Farmer? player,
        [CallerMemberName] string? caller = null
    )
    {
        ModEntry.LogDebug($"ShopkeepGame.StartMinigame {caller}");
        if (location == null || player == null)
        {
            ModEntry.Log(
                $"Failed to start {nameof(ShopkeepGame)}: {location?.NameOrUniqueName ?? "NULL-LOCATION"} {player?.Name ?? "NULL-PLAYER"}",
                LogLevel.Error
            );
            return null;
        }
        ShopkeepGame shopkeepGame = new(helper, Game1.currentLocation, Game1.player);
        helper.Events.Display.Rendering += shopkeepGame.OnRendering;
        helper.Events.Display.Rendered += shopkeepGame.OnRendered;
        Game1.activeClickableMenu = null;
        Game1.displayHUD = false;
        Game1.currentMinigame = shopkeepGame;
        return shopkeepGame;
    }

    public void unload()
    {
        if (state.Current == GameLoopState.Unloaded)
            return;
        ModEntry.LogDebug("ShopkeepGame.unload");
        haggling = null;
        helper.Events.Display.Rendering -= OnRendering;
        helper.Events.Display.Rendered -= OnRendered;
        Game1.activeClickableMenu = null;
        Game1.displayHUD = true;
        state.Current = GameLoopState.Unloaded;
    }

    public bool forceQuit()
    {
        unload();
        return true;
    }

    public void changeScreenSize()
    {
        Game1.viewport.X =
            location.Map.Layers[0].LayerWidth * 64 / 2
            - (int)(Game1.game1.localMultiplayerWindow.Width / 2 / Game1.options.zoomLevel);
        Game1.viewport.Y =
            location.Map.Layers[0].LayerHeight * 64 / 2
            - (int)(Game1.game1.localMultiplayerWindow.Height / 2 / Game1.options.zoomLevel);
        haggling?.CalculateBounds();
    }
    #endregion

    #region gameloop
    private static readonly Vector2 TimerDrawPos = Vector2.One * 12f;

    public void draw(SpriteBatch b)
    {
        // Draw Timer
        b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, 64), Color.Black * 0.5f);
        b.DrawString(Game1.dialogueFont, gameTimer.ToString(), TimerDrawPos, Color.White);

        // Draw Haggling
        haggling?.Draw(b);
    }

    public bool tick(GameTime time)
    {
        // general updates
        gameTimer += time.ElapsedGameTime;
        if (Game1.activeClickableMenu != null)
        {
            Game1.PushUIMode();
            Game1.activeClickableMenu.update(time);
            Game1.PopUIMode();
        }
        state.Update(time);
        // state behavior
        switch (state.Current)
        {
            case GameLoopState.Start:
                DoStart(time);
                return false;
            case GameLoopState.Browse:
                DoBrowse(time);
                return false;
            case GameLoopState.Haggle:
                DoHaggle(time);
                return false;
            default:
            case GameLoopState.Exit:
                return true;
        }
    }
    #endregion

    #region gameloop start
    private void DoStart(GameTime time)
    {
        state.SetNext(GameLoopState.Browse, 100);
    }
    #endregion

    #region gameloop browse
    private readonly ShopkeepBrowsing browsingHelper;

    private void DoBrowse(GameTime time)
    {
        if (haggling != null)
        {
            if (haggling.IsReadyToStart)
            {
                state.Current = GameLoopState.Haggle;
            }
        }
        else
        {
            string buyerName = "Krobus";
            if (browsingHelper.MakeCustomerActor(buyerName) is not NPC buyer)
            {
                state.Current = GameLoopState.Exit;
                return;
            }
            haggling = ShopkeepHaggle.Make(player, buyer, ItemRegistry.Create("(O)Book_Void"));
        }
    }
    #endregion

    #region gameloop haggle
    private ShopkeepHaggle? haggling = null;

    private void DoHaggle(GameTime time)
    {
        if (haggling == null)
        {
            state.Current = GameLoopState.Browse;
            return;
        }

        haggling.Update(time);
        if (!haggling.BeginHaggleRound())
        {
            ModEntry.LogDebug($"Haggle Done {haggling.Count} {haggling.state} {haggling.PickedMult}");
            haggling = null;
            state.Current = GameLoopState.Exit;
            return;
        }
    }
    #endregion

    #region inputs
    public void receiveLeftClick(int x, int y, bool playSound = true)
    {
        ModEntry.LogDebug($"ShopkeepGame.receiveLeftClick: {x} {y} {playSound}");
        if (state.Current == GameLoopState.Haggle)
        {
            haggling?.Pick();
        }
    }

    public void receiveRightClick(int x, int y, bool playSound = true)
    {
        ModEntry.LogDebug($"ShopkeepGame.receiveRightClick: {x} {y} {playSound}");
    }

    public void receiveKeyPress(Keys k)
    {
        ModEntry.LogDebug($"ShopkeepGame.receiveKeyPress: {k}");
    }

    public void receiveKeyRelease(Keys k)
    {
        ModEntry.LogDebug($"ShopkeepGame.receiveKeyRelease: {k}");
    }
    #endregion

    #region unused
    public void releaseLeftClick(int x, int y) { }

    public void releaseRightClick(int x, int y) { }

    public void leftClickHeld(int x, int y) { }

    public void receiveEventPoke(int data) { }
    #endregion
}
