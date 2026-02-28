global using SObject = StardewValley.Object;
using System.Diagnostics;
using Merchant.Misc;
using Merchant.Models;
using Merchant.ModIntegration;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

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
    internal static ModConfig Config { get; private set; } = null!;
    internal static IModHelper help = null!;
    private static readonly PerScreen<MerchantProgressData?> progressData = new();
    internal static MerchantProgressData? ProgressData => progressData.Value;
    private static readonly PerScreen<NPCFriendEntries?> friendEntries = new();
    internal static NPCFriendEntries FriendEntries => friendEntries.Value ??= new NPCFriendEntries(Game1.player);

    public override void Entry(IModHelper helper)
    {
        DynamicMethods.Make();

        I18n.Init(helper.Translation);

        mon = Monitor;
        help = helper;
        Config = help.ReadConfig<ModConfig>();
        help.Events.GameLoop.GameLaunched += OnGameLaunched;
        help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        help.Events.GameLoop.Saving += OnSaving;

        AssetManager.Register();
        GameDelegates.Register();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (
            Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu")
            is IGenericModConfigMenuApi gmcm
        )
        {
            Config.Register(ModManifest, gmcm);
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        progressData.Value = null;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        progressData.Value = MerchantProgressData.Read();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        progressData.Value?.Write();
        FriendEntries.Clear();
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
