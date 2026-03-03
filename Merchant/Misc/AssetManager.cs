using Merchant.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Shops;

namespace Merchant.Misc;

internal sealed class CachedLazyLoader<T>(string assetName, string? additionallyInvalidateOn = null)
{
    private Dictionary<string, T>? cachedData = null;

    public T? Get(string? key)
    {
        if (key == null)
            return default;
        cachedData ??= Game1.content.Load<Dictionary<string, T>>(assetName);
        if (cachedData.TryGetValue(key, out T? data))
            return data;
        return default;
    }

    public void Invalidate(IReadOnlySet<IAssetName> names)
    {
        if (
            names.Any(name =>
                name.IsEquivalentTo(assetName)
                || additionallyInvalidateOn == null
                || name.IsEquivalentTo(additionallyInvalidateOn)
            )
        )
        {
            cachedData = null;
        }
    }
}

internal static class AssetManager
{
    private const string Asset_TextureCraftables = $"{ModEntry.ModId}/craftables";
    internal const string Asset_Strings = $"{ModEntry.ModId}.i18n";
    internal const string Asset_CustomerData = $"{ModEntry.ModId}/Customers";
    internal const string Asset_ShopkeepContextData = $"{ModEntry.ModId}/ShopkeepContexts";
    internal const string Asset_TourismWavesData = $"{ModEntry.ModId}/TourismWaves";
    internal const string Asset_Tourists = $"{ModEntry.ModId}/Tourists";

    internal const string CashRegisterId = $"{ModEntry.ModId}_CashRegister";
    internal const string CashRegisterQId = $"(BC){ModEntry.ModId}_CashRegister";
    internal const string ContextTag_CashRegister = $"{ModEntry.ModId}_cash_register";
    internal const string DoorbellCue = $"{ModEntry.ModId}_doorbell";
    internal const string MapProp_EntryPoint = $"{ModEntry.ModId}_EntryPoint";
    internal const string MapProp_ShopkeepContextId = $"{ModEntry.ModId}_ShopkeepContextId";
    internal const string Default_TourismWave = "Default";

    private const AssetEditPriority ReallyEarly = AssetEditPriority.Early - 100;

    public static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
    }

    #region lazy loaders
    internal static readonly CachedLazyLoader<CustomerData> Customers = new(Asset_CustomerData);
    internal static readonly CachedLazyLoader<ShopkeepContextData> ShopkeepContexts = new(Asset_ShopkeepContextData);
    internal static readonly CachedLazyLoader<TourismWaveData> TourismWaves = new(Asset_TourismWavesData);
    internal static readonly CachedLazyLoader<TouristData> Tourists = new(Asset_Tourists);

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        Customers.Invalidate(e.NamesWithoutLocale);
        ShopkeepContexts.Invalidate(e.NamesWithoutLocale);
        TourismWaves.Invalidate(e.NamesWithoutLocale);
        Tourists.Invalidate(e.NamesWithoutLocale);
    }
    #endregion

    internal static string LoadString(string key) => Game1.content.LoadString($"{Asset_Strings}:{key}");

    internal static string LoadString(string key, params object[] substitutions) =>
        Game1.content.LoadString($"{Asset_Strings}:{key}", substitutions);

    internal static string LoadStringReturnNullIfNotFound(string key, params object[] substitutions) =>
        Game1.content.LoadStringReturnNullIfNotFound($"{Asset_Strings}:{key}", substitutions);

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
        else if (name.IsEquivalentTo("Data/Shops"))
        {
            e.Edit(Edit_Shops, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo("Data/AudioChanges"))
        {
            e.Edit(Edit_AudioChanges, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo("Data/Events/FishShop"))
        {
            e.Edit(Edit_Events_FishShop, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo(Asset_ShopkeepContextData))
        {
            e.LoadFrom(() => new Dictionary<string, ShopkeepContextData>(), AssetLoadPriority.Exclusive);
        }
        else if (name.IsEquivalentTo(Asset_CustomerData))
        {
            e.LoadFromModFile<Dictionary<string, CustomerData>>(
                "assets/data_customers.json",
                AssetLoadPriority.Exclusive
            );
            e.Edit(Edit_CustomerData, ReallyEarly);
        }
        else if (name.IsEquivalentTo(Asset_TourismWavesData))
        {
            e.LoadFromModFile<Dictionary<string, TourismWaveData>>(
                "assets/data_tourism_waves.json",
                AssetLoadPriority.Exclusive
            );
        }
        else if (name.IsEquivalentTo(Asset_Tourists))
        {
            e.LoadFromModFile<Dictionary<string, TourismWaveData>>(
                "assets/data_tourists.json",
                AssetLoadPriority.Exclusive
            );
        }
        else if (name.IsEquivalentTo(Asset_TextureCraftables))
        {
            e.LoadFromModFile<Texture2D>("assets/tx_craftables.png", AssetLoadPriority.Low);
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

    private static void Edit_CustomerData(IAssetData asset)
    {
        IDictionary<string, CustomerData> data = asset.AsDictionary<string, CustomerData>().Data;
        foreach ((string key, CharacterData charaData) in Game1.characterData)
        {
            if (GameStateQuery.IsImmutablyFalse(charaData.CanSocialize))
                continue;
            data.TryAdd(key, new CustomerData());
        }
    }

    private static void Edit_Events_FishShop(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        data[$"{ModEntry.ModId}_WillyWalrus/NpcVisibleHere Willy/EarnedMoney 25000"] =
            $"distantBanjo/6 8/farmer 5 9 0 Willy 5 4 2/setSkipActions AddItem (BC)mushymato.Merchant_CashRegister/skippable/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.01\"/faceDirection farmer 2/playsound doorClose/textAboveHead Willy \"[LocalizedText {Asset_Strings}:WillyWalrus.Willy.02]\"/pause 1000/faceDirection farmer 0/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.03\"/showFrame Willy 25/positionOffset Willy 0 8 true/move farmer 0 -3 0/textAboveHead Willy \"[LocalizedText {Asset_Strings}:WillyWalrus.Willy.04]\"/showFrame Willy 0/positionOffset Willy 0 -8 true/pause 1500/faceDirection farmer 2/itemAboveHead (BC)mushymato.Merchant_CashRegister/setSkipActions null/pause 3300/addItem (BC)mushymato.Merchant_CashRegister/message \"[LocalizedText {Asset_Strings}:WillyWalrus.Message]\"/faceDirection farmer 0/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.05\"/emote farmer 8/emote Willy 40/faceDirection Willy 1/pause 500/faceDirection Willy 3/pause 500/faceDirection Willy 2 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.06\"/quickQuestion #[LocalizedText {Asset_Strings}:WillyWalrus.Player.A1]#[LocalizedText {Asset_Strings}:WillyWalrus.Player.A2](break)speak Willy \"{Asset_Strings}:WillyWalrus.Willy.R1\"(break)speak Willy \"{Asset_Strings}:WillyWalrus.Willy.R2\"/speed Willy 1/advancedMove Willy false -3 0 0 4 2 0 0 -2 1 1/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.07\"/faceDirection farmer 3 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.08\"/faceDirection farmer 2 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.09\"/faceDirection farmer 3 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.10\"/emote farmer 40/speed Willy 1/advancedMove Willy false 0 1 2 0 1 1/faceDirection farmer 2 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.11\"/faceDirection Willy 0 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.12\"/emote farmer 40/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.13\"/emote Willy 32/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.14\"/pause 2000/end";
    }

    private static void Edit_AudioChanges(IAssetData asset)
    {
        IDictionary<string, AudioCueData> data = asset.AsDictionary<string, AudioCueData>().Data;
        data[DoorbellCue] = new()
        {
            Id = DoorbellCue,
            FilePaths =
            [
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "sfx_doorbell01.ogg"),
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "sfx_doorbell02.ogg"),
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "sfx_doorbell03.ogg"),
                Path.Combine(ModEntry.help.DirectoryPath, "assets", "sfx_doorbell04.ogg"),
            ],
            Category = "Sound",
            StreamedVorbis = false,
            Looped = false,
            UseReverb = true,
        };
    }

    private static void Edit_Shops(IAssetData asset)
    {
        IDictionary<string, ShopData> data = asset.AsDictionary<string, ShopData>().Data;
        if (data.ContainsKey("Carpenter"))
            data["Carpenter"].Items.Add(new() { Id = CashRegisterQId, ItemId = CashRegisterQId });
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
            Price = 2500,
            Fragility = 0,
            CanBePlacedOutdoors = true,
            CanBePlacedIndoors = true,
            IsLamp = false,
            Texture = Asset_TextureCraftables,
            SpriteIndex = 0,
            ContextTags = [ContextTag_CashRegister],
            CustomFields = null,
        };
    }
}
