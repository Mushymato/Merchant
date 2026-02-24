using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace Merchant.Management;

public sealed class ShopkeepGame : IMinigame
{
    private readonly GameLocation location;
    private readonly Farmer player;

    #region state
    private TimeSpan gameTimer = TimeSpan.Zero;

    internal enum GameLoopState
    {
        Start,
        Browse,
        Haggle,
        Report,
        Unload,
    }

    private readonly StateManager<GameLoopState> state = new(GameLoopState.Start);
    #endregion

    #region settings
    public bool doMainGameUpdates() => haggling == null;

    public string minigameId() => I18n.Minigame_Id();

    public bool overrideFreeMouseMovement() => true;
    #endregion

    #region setup teardown
    private ShopkeepGame(GameLocation location, Farmer player, ShopkeepBrowsing browsing)
    {
        this.location = location;
        this.player = player;
        this.browsing = browsing;
        changeScreenSize();
    }

    private void OnRendering(object? sender, RenderingEventArgs e)
    {
        // adjust minigame rendering timing by nulling it before render
        if (Game1.currentMinigame == this)
            DynamicMethods.Set_Game1_currentMinigame(null);
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        // restore minigame after render
        if (Game1.currentMinigame == null)
        {
            DynamicMethods.Set_Game1_currentMinigame(this);
            draw(e.SpriteBatch);
        }
    }

    public static ShopkeepGame? StartMinigame(GameLocation? location, Farmer? player, ShopkeepBrowsing browsing)
    {
        if (location == null || player == null)
        {
            ModEntry.Log(
                $"Failed to start {nameof(ShopkeepGame)}: {location?.NameOrUniqueName ?? "NULL-LOCATION"} {player?.Name ?? "NULL-PLAYER"}",
                LogLevel.Error
            );
            return null;
        }
        ShopkeepGame shopkeepGame = new(location, player, browsing);
        ModEntry.help.Events.Display.Rendering += shopkeepGame.OnRendering;
        ModEntry.help.Events.Display.Rendered += shopkeepGame.OnRendered;
        Game1.activeClickableMenu = null;
        Game1.displayHUD = false;
        Game1.currentMinigame = shopkeepGame;
        Game1.changeMusicTrack("event2", false, MusicContext.MiniGame);
        player.completelyStopAnimatingOrDoingAction();
        return shopkeepGame;
    }

    public void unload()
    {
        ModEntry.LogDebug("ShopkeepGame.unload");
        browsing.FinalizeAndCleanup();
        haggling = null;
        ModEntry.help.Events.Display.Rendering -= OnRendering;
        ModEntry.help.Events.Display.Rendered -= OnRendered;
        Game1.stopMusicTrack(MusicContext.MiniGame);
        Game1.activeClickableMenu = null;
        Game1.displayHUD = true;
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
            case GameLoopState.Report:
                return false;
            default:
            case GameLoopState.Unload:
                return true;
        }
    }

    #endregion

    #region gameloop start
    private void DoStart(GameTime time)
    {
        state.SetNext(GameLoopState.Browse, 1000);
    }
    #endregion

    #region gameloop browse
    private readonly ShopkeepBrowsing browsing;

    private void DoBrowse(GameTime time)
    {
        if (haggling != null && haggling.IsReadyToStart)
        {
            state.Current = GameLoopState.Haggle;
            return;
        }
        if (browsing.Update(time, ref haggling))
        {
            Game1.stopMusicTrack(MusicContext.MiniGame);
            Game1.changeMusicTrack("harveys_theme_jazz", false, MusicContext.MiniGame);
            state.SetNext(GameLoopState.Report, 1000);
            return;
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

        if (haggling.Update(time))
        {
            haggling = null;
            state.Current = player.Stamina < 9 ? GameLoopState.Report : GameLoopState.Browse;
            return;
        }
    }
    #endregion

    #region inputs
    public void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (Game1.activeClickableMenu is ConfirmationDialog confirmationDialog)
        {
            confirmationDialog.receiveLeftClick(x, y);
        }
        else if (state.Current == GameLoopState.Haggle)
        {
            haggling?.Pick();
            return;
        }
        else if (state.Current == GameLoopState.Report)
        {
            state.Current = GameLoopState.Unload;
            return;
        }
    }

    public void receiveKeyPress(Keys k)
    {
        if (Game1.activeClickableMenu is ConfirmationDialog confirmationDialog)
        {
            confirmationDialog.receiveKeyPress(k);
        }
        else if (state.Current == GameLoopState.Haggle)
        {
            if (Game1.options.doesInputListContain(Game1.options.useToolButton, k))
            {
                haggling?.Pick();
            }
        }
        else if (state.Current == GameLoopState.Report)
        {
            if (Game1.options.doesInputListContain(Game1.options.menuButton, k))
            {
                state.Current = GameLoopState.Unload;
            }
        }

        if (Game1.options.doesInputListContain(Game1.options.menuButton, k))
        {
            Game1.activeClickableMenu = new ConfirmationDialog(I18n.QuitConfirm(), confirmForceQuit);
        }
    }

    private void confirmForceQuit(Farmer who)
    {
        state.Current = GameLoopState.Unload;
    }

    #endregion

    #region unused
    public void receiveRightClick(int x, int y, bool playSound = true) { }

    public void receiveKeyRelease(Keys k) { }

    public void releaseLeftClick(int x, int y) { }

    public void releaseRightClick(int x, int y) { }

    public void leftClickHeld(int x, int y) { }

    public void receiveEventPoke(int data) { }

    #endregion
}
