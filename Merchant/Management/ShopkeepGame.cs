using System.Diagnostics.CodeAnalysis;
using Merchant.Misc;
using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Objects;

namespace Merchant.Management;

public sealed class ShopkeepGame : IMinigame
{
    internal const int STAMINA_COST_SHOPKEEPING = 10;
    internal const int STAMINA_COST_HAGGLING = 4;

    private readonly GameLocation location;
    private readonly Farmer player;
    private readonly Point tileAboveCashRegister;
    private readonly (Point, int) playerPreviousPosition;
    private readonly Point playerPreviousViewport;

    #region state

    internal bool Unloaded = false;

    internal enum GameLoopState
    {
        Start,
        Browse,
        Haggle,
        Report,
        Unload,
    }

    private readonly StateManager<GameLoopState> state = new(GameLoopState.Start, nameof(GameLoopState));
    #endregion

    #region settings
    public bool doMainGameUpdates() => true;

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
        this.playerPreviousViewport = new(Game1.viewport.X, Game1.viewport.Y);
        changeScreenSize();
    }

    private void OnRendering(object? sender, RenderingEventArgs e)
    {
        if (Unloaded)
            return;
        // adjust minigame rendering timing by nulling it before render
        if (Game1.currentMinigame == this)
            DynamicMethods.Set_Game1_currentMinigame(null);
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        if (Unloaded)
            return;
        // restore minigame after render
        if (Game1.currentMinigame == null)
        {
            DynamicMethods.Set_Game1_currentMinigame(this);
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
            case StardewValley.Mods.RenderSteps.Menu:
                draw(e.SpriteBatch);
                break;
        }
    }

    public static bool TryStartMinigame(
        GameLocation? location,
        Farmer? player,
        Point cashRegisterPoint,
        ShopkeepBrowsing browsing,
        [NotNullWhen(false)] out string? failReason
    )
    {
        failReason = null;
        if (location == null || player == null || Game1.CurrentEvent != null)
        {
            failReason =
                $"Failed to start {nameof(ShopkeepGame)}: {location?.NameOrUniqueName ?? "NULL-LOCATION"} {player?.Name ?? "NULL-PLAYER"}";
            ModEntry.Log(failReason, LogLevel.Error);
            return false;
        }

        if (browsing.WaitingActors.Count == 0)
        {
            failReason = I18n.FailReason_EveryoneHate();
            return false;
        }
        if (Game1.IsGreenRainingHere(location))
        {
            failReason = I18n.FailReason_GreenRain();
            return false;
        }
        if (player.Stamina < 1 + STAMINA_COST_HAGGLING + STAMINA_COST_SHOPKEEPING)
        {
            failReason = I18n.FailReason_TooTired();
            return false;
        }
        if (location.farmers.Count > 1)
        {
            failReason = I18n.FailReason_OtherFarmer();
            return false;
        }

        Point tileAboveCashRegister = new(cashRegisterPoint.X, cashRegisterPoint.Y - 1);
        if (!Topology.IsTileStandable(location, tileAboveCashRegister, CollisionMask.All))
        {
            failReason = I18n.FailReason_TilePosition();
            return false;
        }

        ShopkeepGame shopkeepGame = new(location, player, browsing, tileAboveCashRegister);
        shopkeepGame.PostCreateSetup();
        return true;
    }

    private void PostCreateSetup()
    {
        ModEntry.help.Events.Display.Rendering += OnRendering;
        ModEntry.help.Events.Display.RenderedStep += OnRenderedStep;
        ModEntry.help.Events.Display.Rendered += OnRendered;

        Game1.activeClickableMenu = null;
        Game1.onScreenMenus.RemoveWhere(menu => menu is Toolbar);

        PlayMusic("event2");

        // ban other players from entering (hopefully)
        if (location.ParentBuilding.GetData() is BuildingData buildingData)
        {
            location.ParentBuilding.humanDoor.X = -1;
            location.ParentBuilding.humanDoor.Y = -1;
        }

        player.completelyStopAnimatingOrDoingAction();
        player.Stamina -= STAMINA_COST_SHOPKEEPING;
        player.TemporaryItem = ItemRegistry.Create("(P)0");

        player.setTileLocation(tileAboveCashRegister.ToVector2());
        player.faceDirection(2);

        Game1.currentMinigame = this;
    }

    private void PlayMusic(string musicName)
    {
        Game1.stopMusicTrack(MusicContext.MiniGame);
        if (!location.IsMiniJukeboxPlaying())
            Game1.changeMusicTrack(musicName, false, MusicContext.MiniGame);
    }

    public void unload()
    {
        browsing.Cleanup();
        if (ModEntry.ProgressData.AutoRestockEnabled)
            AutoRestockEmptyTables();
        haggling = null;

        ModEntry.help.Events.Display.Rendering -= OnRendering;
        ModEntry.help.Events.Display.RenderedStep -= OnRenderedStep;
        ModEntry.help.Events.Display.Rendered -= OnRendered;

        Game1.activeClickableMenu = null;
        if (!Game1.onScreenMenus.Any(menu => menu is Toolbar))
            Game1.onScreenMenus.Add(new Toolbar());
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

        Game1.viewport.X = playerPreviousViewport.X;
        Game1.viewport.Y = playerPreviousViewport.Y;

        Unloaded = true;
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
        // This is getting called in draw menu bc this is basically menu
        // Draw haggling
        haggling?.Draw(b);
    }

    public bool tick(GameTime time)
    {
        // general updates
        if (Game1.activeClickableMenu != null)
        {
            Game1.PushUIMode();
            Game1.activeClickableMenu.performHoverAction(Game1.getMouseX(), Game1.getMouseY());
            Game1.PopUIMode();
            if (Game1.activeClickableMenu is ConfirmationDialog)
                return false;
        }
        else if (haggling?.haggleDialogueBox != null)
        {
            Game1.PushUIMode();
            haggling?.haggleDialogueBox.update(time);
            Game1.PopUIMode();
        }
        else
        {
            Game1.UpdateGameClock(time);
            if (Game1.timeOfDay >= 2500)
                PrepareReport();
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
        }
        else if (browsing.Update(time, ref haggling))
        {
            PrepareReport();
        }
        else
        {
            // allow panning viewport with WASD keys
            int panX = 0;
            int panY = 0;
            if (Game1.options.gamepadControls)
            {
                GamePadState gamePadState = Game1.input.GetGamePadState();
                if (gamePadState.ThumbSticks.Left.X < -0.25)
                {
                    panX -= 4;
                }
                else if (gamePadState.ThumbSticks.Left.X > 0.25)
                {
                    panX += 4;
                }
                if (gamePadState.ThumbSticks.Left.Y > 0.25)
                {
                    panY -= 4;
                }
                else if (gamePadState.ThumbSticks.Left.Y < -0.25)
                {
                    panY += 4;
                }
            }
            else
            {
                Keys[] pressedKeys = Game1.oldKBState.GetPressedKeys();
                foreach (Keys k in pressedKeys)
                {
                    if (Game1.options.doesInputListContain(Game1.options.moveDownButton, k))
                    {
                        panY += 4;
                    }
                    else if (Game1.options.doesInputListContain(Game1.options.moveRightButton, k))
                    {
                        panX += 4;
                    }
                    else if (Game1.options.doesInputListContain(Game1.options.moveUpButton, k))
                    {
                        panY -= 4;
                    }
                    else if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, k))
                    {
                        panX -= 4;
                    }
                }
            }

            // allow panning
            // set pan if changed
            if (panX != 0 || panY != 0)
            {
                ModEntry.Log($"B4 {panX},{panY} -> {Game1.viewport.X},{Game1.viewport.Y}");
                Game1.panScreen(panX * 3, panY * 3);
                ModEntry.Log($"AF {panX},{panY} -> {Game1.viewport.X},{Game1.viewport.Y}");
            }
        }
    }

    private void PrepareReport()
    {
        PlayMusic("harveys_theme_jazz");
        Game1.activeClickableMenu = browsing.FinalizeAndReport();
        state.SetAndLock(GameLoopState.Report);
    }

    private void AutoRestockEmptyTables()
    {
        Queue<(Chest, int)> chestItemQueue = [];
        foreach (SObject obj in location.objects.Values)
        {
            if (obj is not Chest chest)
                continue;
            for (int i = 0; i < chest.Items.Count; i++)
            {
                Item item = chest.Items[i];
                if (!ForSaleTarget.CanOfferForSale(item, player))
                    continue;
                chestItemQueue.Enqueue(new(chest, i));
            }
        }

        Queue<Furniture> tableQueue = [];
        foreach (Furniture table in location.furniture)
        {
            if (ModEntry.tableShim.HasSpaceForItems(table))
                tableQueue.Enqueue(table);
        }

        while (tableQueue.TryPeek(out Furniture? table) && chestItemQueue.TryDequeue(out (Chest, int) chestItem))
        {
            Item? item = chestItem.Item1.Items[chestItem.Item2];
            string itemId = item.QualifiedItemId;
            if (ModEntry.tableShim.TryPlaceItemOnTable(table, ref item))
            {
                ModEntry.Log(
                    $"Restock {table.QualifiedItemId}({table.TileLocation}) with {itemId} ({item?.Stack ?? 0} left)"
                );
                chestItem.Item1.Items[chestItem.Item2] = item;
            }
            if (item != null)
                chestItemQueue.Enqueue(chestItem);
            if (!ModEntry.tableShim.HasSpaceForItems(table))
                tableQueue.Dequeue();
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
            if (player.Stamina < 9)
            {
                PrepareReport();
            }
            else
            {
                state.Current = GameLoopState.Browse;
            }
            return;
        }
    }
    #endregion

    #region gameloop report
    private void DoReport(GameTime time)
    {
        if (Game1.activeClickableMenu == null)
        {
            state.Unlock();
            state.SetAndLock(GameLoopState.Unload);
        }
        browsing.UpdateActorsOnly(time);
    }
    #endregion

    #region inputs
    public void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (Game1.activeClickableMenu is not null)
        {
            Game1.PushUIMode();
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            if (Game1.activeClickableMenu.isWithinBounds(mouseX, mouseY))
            {
                Game1.activeClickableMenu.receiveLeftClick(mouseX, mouseY, playSound);
            }
            else
            {
                Game1.activeClickableMenu.exitThisMenu();
                Game1.activeClickableMenu = null;
            }
            Game1.PopUIMode();
        }
        else if (state.Current == GameLoopState.Haggle)
        {
            haggling?.Pick();
        }
        else if (state.Current == GameLoopState.Report)
        {
            state.Unlock();
            state.SetAndLock(GameLoopState.Unload);
        }
    }

    public void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (Game1.activeClickableMenu is not null)
        {
            Game1.PushUIMode();
            Game1.activeClickableMenu.receiveRightClick(Game1.getMouseX(), Game1.getMouseY(), playSound);
            Game1.PopUIMode();
        }
        else if (state.Current == GameLoopState.Haggle)
        {
            haggling?.Giveup();
        }
    }

    public void receiveKeyPress(Keys k)
    {
        if (Game1.activeClickableMenu is not null)
        {
            Game1.PushUIMode();
            Game1.activeClickableMenu.receiveKeyPress(k);
            Game1.PopUIMode();
            return;
        }

        if (Game1.options.doesInputListContain(Game1.options.useToolButton, k))
        {
            if (state.Current == GameLoopState.Haggle)
            {
                haggling?.Giveup();
                return;
            }
        }

        if (Game1.options.doesInputListContain(Game1.options.actionButton, k))
        {
            if (state.Current == GameLoopState.Haggle)
            {
                haggling?.Pick();
                return;
            }
        }

        if (Game1.options.doesInputListContain(Game1.options.menuButton, k))
        {
            if (state.Current == GameLoopState.Report)
            {
                state.Unlock();
                state.SetAndLock(GameLoopState.Unload);
            }
            else if (state.Current != GameLoopState.Haggle)
            {
                Game1.activeClickableMenu = new ConfirmationDialog(I18n.QuitConfirm(), ConfirmForceQuit);
            }
            return;
        }
    }

    private void ConfirmForceQuit(Farmer who)
    {
        PrepareReport();
    }

    #endregion

    #region unused

    public void receiveKeyRelease(Keys k) { }

    public void releaseLeftClick(int x, int y) { }

    public void releaseRightClick(int x, int y) { }

    public void leftClickHeld(int x, int y) { }

    public void receiveEventPoke(int data) { }

    #endregion
}
