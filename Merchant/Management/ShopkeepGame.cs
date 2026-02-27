using Merchant.Misc;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Objects;

namespace Merchant.Management;

public sealed class ShopkeepGame : IMinigame
{
    private readonly GameLocation location;
    private readonly Farmer player;
    private readonly Point tileAboveCashRegister;
    private readonly (Point, int) playerPreviousPosition;

    #region state

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
    private ShopkeepGame(GameLocation location, Farmer player, ShopkeepBrowsing browsing, Point tileAboveCashRegister)
    {
        this.location = location;
        this.player = player;
        this.browsing = browsing;
        this.tileAboveCashRegister = tileAboveCashRegister;
        this.playerPreviousPosition = (player.TilePoint, player.FacingDirection);
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

    private void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        switch (e.Step)
        {
            case StardewValley.Mods.RenderSteps.World_Background:
                browsing.DrawShadows(e.SpriteBatch);
                break;
            case StardewValley.Mods.RenderSteps.World_Sorted:
                browsing.DrawCharacters(e.SpriteBatch);
                break;
            case StardewValley.Mods.RenderSteps.World_AlwaysFront:
                browsing.DrawCharacterEmotes(e.SpriteBatch);
                break;
        }
    }

    public static ShopkeepGame? StartMinigame(
        GameLocation? location,
        Farmer? player,
        Point cashRegisterPoint,
        ShopkeepBrowsing browsing
    )
    {
        if (location == null || player == null || Game1.CurrentEvent != null)
        {
            ModEntry.Log(
                $"Failed to start {nameof(ShopkeepGame)}: {location?.NameOrUniqueName ?? "NULL-LOCATION"} {player?.Name ?? "NULL-PLAYER"} EVENT:{Game1.CurrentEvent}",
                LogLevel.Error
            );
            return null;
        }
        if (player.Stamina < 25)
        {
            Game1.drawObjectDialogue(I18n.FailReason_TooTired());
            return null;
        }
        if (location.farmers.Count > 1)
        {
            Game1.drawObjectDialogue(I18n.FailReason_OtherFarmer());
            return null;
        }
        Point tileAboveCashRegister = new(cashRegisterPoint.X, cashRegisterPoint.Y - 1);
        if (!Topology.IsTileStandable(location, tileAboveCashRegister, CollisionMask.All))
        {
            Game1.drawObjectDialogue(I18n.FailReason_TilePosition());
            return null;
        }
        ShopkeepGame shopkeepGame = new(location, player, browsing, tileAboveCashRegister);
        shopkeepGame.PostCreateSetup();
        return shopkeepGame;
    }

    private void PostCreateSetup()
    {
        ModEntry.help.Events.Display.Rendering += OnRendering;
        ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;
        ModEntry.help.Events.Display.Rendered += OnRendered;

        Game1.activeClickableMenu = null;
        Game1.displayHUD = false;
        Game1.currentMinigame = this;
        Game1.changeMusicTrack("event2", false, MusicContext.MiniGame);

        // ban other players from entering (hopefully)
        if (location.ParentBuilding.GetData() is BuildingData buildingData)
        {
            location.ParentBuilding.humanDoor.X = -1;
            location.ParentBuilding.humanDoor.Y = -1;
        }

        player.completelyStopAnimatingOrDoingAction();
        player.Stamina -= 20;
        player.TemporaryItem = ItemRegistry.Create("(P)0");

        player.setTileLocation(tileAboveCashRegister.ToVector2());
        player.faceDirection(2);
    }

    public void unload()
    {
        ModEntry.LogDebug("ShopkeepGame.unload");
        browsing.FinalizeAndCleanup();
        if (ModEntry.config.EnableAutoRestock)
            AutoRestockEmptyTables();
        haggling = null;

        ModEntry.help.Events.Display.Rendering -= OnRendering;
        ModEntry.help.Events.Display.RenderedStep -= OnRenderedStep;
        ModEntry.help.Events.Display.Rendered -= OnRendered;

        Game1.activeClickableMenu = null;
        Game1.displayHUD = true;
        Game1.stopMusicTrack(MusicContext.MiniGame);

        if (location.ParentBuilding.GetData() is BuildingData buildingData)
        {
            location.ParentBuilding.humanDoor.X = buildingData.HumanDoor.X;
            location.ParentBuilding.humanDoor.Y = buildingData.HumanDoor.Y;
            // TODO: maybe abuse _actionTiles to display message?
        }

        player.setTileLocation(playerPreviousPosition.Item1.ToVector2());
        player.faceDirection(playerPreviousPosition.Item2);
        player.TemporaryItem = null;
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
    public void draw(SpriteBatch b)
    {
        // Draw day time money box by itself
        if (Game1.activeClickableMenu == null)
            Game1.dayTimeMoneyBox.draw(b);
        // Draw Haggling
        haggling?.Draw(b);
    }

    public bool tick(GameTime time)
    {
        // general updates
        Game1.UpdateGameClock(time);
        if (Game1.activeClickableMenu != null)
        {
            Game1.PushUIMode();
            Game1.activeClickableMenu.update(time);
            Game1.PopUIMode();
            if (Game1.activeClickableMenu is ConfirmationDialog)
                return false;
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
                DoReport(time);
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
            state.Current = GameLoopState.Report;
            return;
        }
    }

    private void AutoRestockEmptyTables()
    {
        Furniture testTable = ItemRegistry.Create<Furniture>("(F)DesertTable");
        testTable.Location = location;
        Queue<(Chest, int)> chestItemQueue = [];
        foreach (SObject obj in location.objects.Values)
        {
            if (obj is not Chest chest)
                continue;
            for (int i = 0; i < chest.Items.Count; i++)
            {
                Item item = chest.Items[i];
                if (item == null)
                    continue;
                if (!testTable.performObjectDropInAction(item, true, null))
                    continue;
                if (!ForSaleTarget.CanOfferForSale(item, player))
                    continue;
                chestItemQueue.Enqueue(new(chest, i));
            }
        }
        foreach (Furniture table in location.furniture)
        {
            if (!table.IsTable())
                continue;

            if (chestItemQueue.TryDequeue(out (Chest, int) chestItem))
            {
                Item? item = chestItem.Item1.Items[chestItem.Item2];
                if (table.performObjectDropInAction(item, true, null))
                {
                    table.performObjectDropInAction(item, false, null);
                    item = item.ConsumeStack(1);
                    chestItem.Item1.Items[chestItem.Item2] = item;
                }
                if (item == null)
                    continue;
                chestItemQueue.Enqueue(chestItem);
            }
        }
        foreach (SObject obj in location.objects.Values)
        {
            if (obj is not Chest chest)
                continue;
            chest.Items.RemoveEmptySlots();
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

    #region gameloop report
    private void DoReport(GameTime time)
    {
        browsing.UpdateActorsOnly(time);
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
            return;
        }

        if (Game1.options.doesInputListContain(Game1.options.useToolButton, k))
        {
            if (state.Current == GameLoopState.Haggle)
            {
                haggling?.Pick();
            }
        }

        if (Game1.options.doesInputListContain(Game1.options.menuButton, k))
        {
            if (state.Current == GameLoopState.Report)
            {
                state.Current = GameLoopState.Unload;
            }
            else
            {
                Game1.activeClickableMenu = new ConfirmationDialog(I18n.QuitConfirm(), confirmForceQuit);
            }
        }
    }

    private void confirmForceQuit(Farmer who)
    {
        state.Current = GameLoopState.Unload;
        Game1.activeClickableMenu = null;
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
