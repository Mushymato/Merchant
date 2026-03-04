global using SObject = StardewValley.Object;
using System.Diagnostics;
using Merchant.Management;
using Merchant.Misc;
using Merchant.Models;
using Merchant.ModIntegration;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Triggers;

namespace Merchant;

public sealed class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif
    public const string ModId = "mushymato.Merchant";
    private static IMonitor? mon;
    internal static IModHelper help = null!;
    internal static ModConfig config = null!;
    private static readonly PerScreen<MerchantProgressData?> progressData = new();
    internal static MerchantProgressData? ProgressData => progressData.Value;
    private static readonly PerScreen<NPCFriendEntries?> friendEntries = new();
    internal static NPCFriendEntries FriendEntries => friendEntries.Value ??= new NPCFriendEntries(Game1.player);

    internal static bool HasBETAS = false;

    internal static ITableShim tableShim = new TableShimBase();

    public override void Entry(IModHelper helper)
    {
        DynamicMethods.Make();

        I18n.Init(helper.Translation);

        mon = Monitor;
        help = helper;
        config = help.ReadConfig<ModConfig>();
        help.Events.GameLoop.GameLaunched += OnGameLaunched;
        help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        help.Events.GameLoop.Saving += OnSaving;
        help.Events.Player.Warped += OnWarped;

        help.ConsoleCommands.Add(
            "merchant-forcequit",
            "Force quit current merchant shopkeep session",
            ConsoleForceQuit
        );

        AssetManager.Register();
        GameDelegates.Register();
    }

    private void ConsoleForceQuit(string arg1, string[] arg2)
    {
        if (Game1.currentMinigame is ShopkeepGame shopkeepGame)
        {
            shopkeepGame.Unloaded = true;
            shopkeepGame.forceQuit();
            Game1.currentMinigame = null;
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (
            Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu")
            is IGenericModConfigMenuApi gmcm
        )
        {
            config.Register(ModManifest, gmcm);
        }

        if (
            (
                Helper
                    .ModRegistry.Get("leroymilo.FurnitureFramework")
                    ?.Manifest.Version.IsNewerThan(new SemanticVersion("3.3.0"))
                ?? false
            )
            && Helper.ModRegistry.GetApi<IFurnitureFrameworkAPI>("leroymilo.FurnitureFramework")
                is IFurnitureFrameworkAPI ffApi
        )
        {
            Log($"Using FurnitureFramework table shim");
            tableShim = new TableShimFF(ffApi);
        }

        HasBETAS = Helper.ModRegistry.IsLoaded("Spiderbuttons.BETAS");
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        progressData.Value = null;
        friendEntries.Value = null;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        progressData.Value = MerchantProgressData.Read();
        friendEntries.Value = null;
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        progressData.Value?.Write();
        friendEntries.Value?.ResetFriends();
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        friendEntries.Value?.ResetFriends();
    }

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
