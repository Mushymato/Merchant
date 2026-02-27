using Merchant.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Machines;

namespace Merchant.Misc;

internal static class AssetManager
{
    private const string Asset_TextureCashregister = $"{ModEntry.ModId}/cashregister";
    internal const string Asset_Strings = $"{ModEntry.ModId}\\Strings";
    internal const string Asset_CustomerData = $"{ModEntry.ModId}/Customers";
    internal const string CashRegisterId = $"{ModEntry.ModId}_CashRegister";
    internal const string CashRegisterQId = $"(BC){ModEntry.ModId}_CashRegister";
    internal const string DoorbellCue = $"{ModEntry.ModId}_doorbell";

    private static Dictionary<string, CustomerData>? customerData = null;
    public static Dictionary<string, CustomerData> CustomerData =>
        customerData ??= Game1.content.Load<Dictionary<string, CustomerData>>(Asset_CustomerData);

    public static CustomerData? GetCustomerData(string key)
    {
        if (CustomerData.TryGetValue(key, out CustomerData? data))
            return data;
        return null;
    }

    public static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
    }

    internal static string LoadString(string key) => Game1.content.LoadString($"{Asset_Strings}:{key}");

    internal static string LoadString(string key, params object[] substitutions) =>
        Game1.content.LoadString($"{Asset_Strings}:{key}", substitutions);

    internal static string LoadStringReturnNullIfNotFound(string key, params object[] substitutions) =>
        Game1.content.LoadStringReturnNullIfNotFound($"{Asset_Strings}:{key}", substitutions);

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_CustomerData)))
        {
            customerData = null;
        }
    }

    public static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        IAssetName name = e.NameWithoutLocale;
        if (name.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(Edit_BigCraftables, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo("Data/Machines"))
        {
            e.Edit(Edit_Machines, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo(Asset_TextureCashregister))
        {
            e.LoadFromModFile<Texture2D>("assets/cashregister.png", AssetLoadPriority.Low);
        }
        else if (name.IsEquivalentTo("Data/AudioChanges"))
        {
            e.Edit(Edit_AudioChanges, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo(Asset_CustomerData))
        {
            e.LoadFrom(() => new Dictionary<string, CustomerData>(), AssetLoadPriority.Exclusive);
        }
        else if (name.IsEquivalentTo(Asset_Strings))
        {
            string stringsAsset = Path.Combine("i18n", e.Name.LanguageCode.ToString() ?? "default", "strings.json");
            if (File.Exists(Path.Combine(ModEntry.help.DirectoryPath, stringsAsset)))
            {
                e.LoadFromModFile<Dictionary<string, string>>(stringsAsset, AssetLoadPriority.Exclusive);
            }
            else
            {
                e.LoadFromModFile<Dictionary<string, string>>("i18n/default/strings.json", AssetLoadPriority.Exclusive);
            }
        }
    }

    private static void Edit_AudioChanges(IAssetData asset)
    {
        IDictionary<string, AudioCueData> data = asset.AsDictionary<string, AudioCueData>().Data;
        data[DoorbellCue] = new()
        {
            Id = DoorbellCue,
            FilePaths =
            [
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "doorbell01.ogg"),
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "doorbell02.ogg"),
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "doorbell03.ogg"),
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "doorbell04.ogg"),
            ],
            Category = "Sound",
            StreamedVorbis = false,
            Looped = false,
            UseReverb = true,
        };
    }

    public static void Edit_Machines(IAssetData asset)
    {
        IDictionary<string, MachineData> data = asset.AsDictionary<string, MachineData>().Data;
        data[CashRegisterQId] = new() { InteractMethod = GameDelegates.InteractMethod };
    }

    public static void Edit_BigCraftables(IAssetData asset)
    {
        IDictionary<string, BigCraftableData> data = asset.AsDictionary<string, BigCraftableData>().Data;
        data[CashRegisterId] = new()
        {
            Name = CashRegisterId,
            DisplayName = $"[LocalizedText {Asset_Strings}:CashRegister_Name]",
            Description = $"[LocalizedText {Asset_Strings}:CashRegister_Desc]",
            Price = 5000,
            Fragility = 0,
            CanBePlacedOutdoors = true,
            CanBePlacedIndoors = true,
            IsLamp = true,
            Texture = Asset_TextureCashregister,
            SpriteIndex = 0,
            ContextTags = [ModEntry.ModId],
            CustomFields = null,
        };
    }
}
