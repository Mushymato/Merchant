using Merchant.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Shops;

namespace Merchant.Misc;

internal static class AssetManager
{
    private const string Asset_TextureCraftables = $"{ModEntry.ModId}/craftables";
    internal const string Asset_Strings = $"{ModEntry.ModId}.i18n";
    internal const string Asset_CustomerData = $"{ModEntry.ModId}/Customers";
    internal const string Asset_ShopkeepLocationData = $"{ModEntry.ModId}/ShopkeepLocations";
    internal const string CashRegisterId = $"{ModEntry.ModId}_CashRegister";
    internal const string CashRegisterQId = $"(BC){ModEntry.ModId}_CashRegister";
    internal const string ContextTag_CashRegister = $"{ModEntry.ModId}_cash_register";
    internal const string DoorbellCue = $"{ModEntry.ModId}_doorbell";

    private const AssetEditPriority ReallyEarly = AssetEditPriority.Early - 100;

    private static Dictionary<string, CustomerData>? customerData = null;

    public static CustomerData? GetCustomerData(string key)
    {
        customerData ??= Game1.content.Load<Dictionary<string, CustomerData>>(Asset_CustomerData);
        if (customerData.TryGetValue(key, out CustomerData? data))
            return data;
        return null;
    }

    private static Dictionary<string, ShopkeepLocationData>? shopkeepLocData = null;

    public static ShopkeepLocationData? GetShopkeepLocationData(string key)
    {
        shopkeepLocData ??= Game1.content.Load<Dictionary<string, ShopkeepLocationData>>(Asset_ShopkeepLocationData);
        if (shopkeepLocData.TryGetValue(key, out ShopkeepLocationData? data))
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
        if (
            e.NamesWithoutLocale.Any(name =>
                name.IsEquivalentTo(Asset_CustomerData) || name.IsEquivalentTo("Data/Characters")
            )
        )
        {
            customerData = null;
        }
        if (
            e.NamesWithoutLocale.Any(name =>
                name.IsEquivalentTo(Asset_ShopkeepLocationData) || name.IsEquivalentTo("Data/Buildings")
            )
        )
        {
            shopkeepLocData = null;
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
        else if (name.IsEquivalentTo(Asset_ShopkeepLocationData))
        {
            e.LoadFromModFile<Dictionary<string, ShopkeepLocationData>>(
                "assets/data_shopkeep_locations.json",
                AssetLoadPriority.Exclusive
            );
            e.Edit(Edit_ShopkeepLocations, ReallyEarly);
        }
        else if (name.IsEquivalentTo(Asset_CustomerData))
        {
            e.LoadFromModFile<Dictionary<string, CustomerData>>(
                "assets/data_customers.json",
                AssetLoadPriority.Exclusive
            );
            e.Edit(Edit_CustomerData, ReallyEarly);
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

    private static void Edit_ShopkeepLocations(IAssetData asset)
    {
        IDictionary<string, ShopkeepLocationData> data = asset.AsDictionary<string, ShopkeepLocationData>().Data;
        foreach ((string key, BuildingData buildingData) in Game1.buildingData)
        {
            if (buildingData.IndoorMap == null)
                continue;
            data.TryAdd(key, new());
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
        data[$"{ModEntry.ModId}_WillyWalrus"] =
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
