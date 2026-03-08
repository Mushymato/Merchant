using Merchant.Management;
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

internal sealed class CachedLazyLoader<T>(string assetName, string? additionallyInvalidateOn = null)
{
    private Dictionary<string, T>? cachedData = null;
    public Dictionary<string, T> Data => cachedData ??= Game1.content.Load<Dictionary<string, T>>(assetName);

    public T? Get(string? key)
    {
        if (key == null)
            return default;
        if (Data.TryGetValue(key, out T? data))
            return data;
        return default;
    }

    public bool Invalidate(IReadOnlySet<IAssetName> names)
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
            return true;
        }
        return false;
    }
}

internal static class AssetManager
{
    private const string Asset_TextureCraftables = $"{ModEntry.ModId}/craftables";
    internal const string Asset_Strings = $"{ModEntry.ModId}.i18n";
    internal const string Asset_CustomerData = $"{ModEntry.ModId}/Customers";
    internal const string Asset_ShopkeepThemeBoostData = $"{ModEntry.ModId}/ShopkeepThemeBoosts";
    internal const string Asset_TourismWavesData = $"{ModEntry.ModId}/TourismWaves";
    internal const string Asset_Tourists = $"{ModEntry.ModId}/Tourists";

    internal const string CashRegisterId = $"{ModEntry.ModId}_CashRegister";
    internal const string CashRegisterQId = $"(BC){ModEntry.ModId}_CashRegister";
    internal const string RoboShopkeepId = $"{ModEntry.ModId}_RoboShopkeep";
    internal const string RoboShopkeepQId = $"(BC){ModEntry.ModId}_RoboShopkeep";
    internal const string ContextTag_RoboShopkeep = $"{ModEntry.ModId}_robo_shopkeep_object";
    internal const string Metadata_ShopkeepThemeBoosts = $"{ModEntry.ModId}/ShopkeepThemeBoosts";
    internal const string Metadata_ShopkeepCondition = $"{ModEntry.ModId}/ShopkeepCondition";
    internal const string Metadata_ShopkeepNotAllowedMessage = $"{ModEntry.ModId}/ShopkeepNotAllowedMessage";
    internal const string UpgradeShopId = $"{ModEntry.ModId}_Upgrades";

    private const string ThemeBoost_Flowers = $"{ModEntry.ModId}_Flowers";
    private const string ThemeBoost_Eggs = $"{ModEntry.ModId}_Eggs";
    private const string ThemeBoost_Milk = $"{ModEntry.ModId}_Milk";

    public static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
    }

    #region lazy loaders
    internal static readonly CachedLazyLoader<CustomerData> Customers = new(Asset_CustomerData);
    internal static readonly CachedLazyLoader<ShopkeepThemeBoostData> ShopkeepContexts = new(
        Asset_ShopkeepThemeBoostData
    );
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
        else if (name.IsEquivalentTo("Data/Buildings"))
        {
            e.Edit(Edit_Buildings, AssetEditPriority.Late);
        }
        else if (name.IsEquivalentTo("Data/AudioChanges"))
        {
            e.Edit(Edit_AudioChanges, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo("Data/Events/FishShop"))
        {
            e.Edit(Edit_Events_FishShop, AssetEditPriority.Default);
        }
        else if (name.IsEquivalentTo(Asset_CustomerData))
        {
            e.LoadFrom(Load_Customers, AssetLoadPriority.Exclusive);
        }
        else if (name.IsEquivalentTo(Asset_Tourists))
        {
            e.LoadFrom(Load_Tourists, AssetLoadPriority.Exclusive);
        }
        else if (name.IsEquivalentTo(Asset_TourismWavesData))
        {
            e.LoadFrom(Load_TourismWaves, AssetLoadPriority.Exclusive);
        }
        else if (name.IsEquivalentTo(Asset_ShopkeepThemeBoostData))
        {
            e.LoadFrom(Load_ShopkeepThemeBoosts, AssetLoadPriority.Exclusive);
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

    private static Dictionary<string, ShopkeepThemeBoostData> Load_ShopkeepThemeBoosts()
    {
        return new Dictionary<string, ShopkeepThemeBoostData>()
        {
            [ThemeBoost_Flowers] = new()
            {
                Description = $"[LocalizedText {Asset_Strings}:Theme_Flowers]",
                ContextTags = ["flower_item", "edible_flower_item", "wildflour_floral_item"],
                Value = 0.2f,
            },
            [ThemeBoost_Eggs] = new()
            {
                Description = $"[LocalizedText {Asset_Strings}:Theme_Eggs]",
                ContextTags = ["egg_item"],
                Value = 0.2f,
            },
            [ThemeBoost_Milk] = new()
            {
                Description = $"[LocalizedText {Asset_Strings}:Theme_Milk]",
                ContextTags = ["milk_item"],
                Value = 0.2f,
            },
        };
    }

    private static Dictionary<string, TourismWaveData> Load_TourismWaves()
    {
        return new Dictionary<string, TourismWaveData>()
        {
            [$"{ModEntry.ModId}_BooksellerDay"] = new()
            {
                Condition = "mushymato.Merchant_BOOK_SELLER_IN_TOWN",
                DisplayName = $"[LocalizedText {Asset_Strings}:Tourism_BooksellerDay_Name]",
                Description = $"[LocalizedText {Asset_Strings}:Tourism_BooksellerDay_Desc]",
                ContextTags = ["book_item"],
                TouristMinCount = 3,
                Dialogue = new Dictionary<string, CustomerDialogue>
                {
                    [$"{ModEntry.ModId}_Bookbuyer"] = new()
                    {
                        Haggle_Ask = $"[LocalizedText {Asset_Strings}:Haggle_Ask_Bookseller]",
                        Haggle_Compromise = $"[LocalizedText {Asset_Strings}:Haggle_Compromise_Bookseller]",
                        Haggle_Overpriced = $"[LocalizedText {Asset_Strings}:Haggle_Overpriced_Bookseller]",
                        Haggle_Success = $"[LocalizedText {Asset_Strings}:Haggle_Success_Bookseller]",
                        Haggle_Fail = $"[LocalizedText {Asset_Strings}:Haggle_Fail_Bookseller]",
                    },
                },
            },
        };
    }

    private static object Load_Tourists()
    {
        return new Dictionary<string, TouristData>()
        {
            // Booklovers
            [$"{ModEntry.ModId}_Marcello"] = new()
            {
                AppearsDuring = [$"{ModEntry.ModId}_BooksellerDay"],
                ContextTags = ["book_item"],
                DisplayName = $"[LocalizedText {Asset_Strings}:Marcello_Name]",
                Portrait = ModEntry.HasTDITExtras ? "Portraits/Marcello" : null,
                Sprite = "Characters/Marcello",
            },
            [$"{ModEntry.ModId}_Booklover_Penny"] = new()
            {
                AppearsDuring = [$"{ModEntry.ModId}_BooksellerDay"],
                NPC = "Penny",
                Chance = 0.5f,
            },
            [$"{ModEntry.ModId}_Booklover_Elliott"] = new()
            {
                AppearsDuring = [$"{ModEntry.ModId}_BooksellerDay"],
                NPC = "Elliott",
                Chance = 0.5f,
            },
            // Bear
            [$"{ModEntry.ModId}_Bear"] = new()
            {
                Condition = "PLAYER_HAS_SEEN_EVENT Current 2120303",
                ContextTags = ["id_o_724", "id_o_731"],
                DisplayName = $"[LocalizedText Strings/NPCNames:Bear]",
                Portrait = "Portraits/Bear",
                Sprite = "Characters/Bear",
                MugShotSourceRect = new(8, 0, 16, 28),
                Size = new(32, 32),
                ShowShadow = false,
                Chance = 0.1f,
            },
        };
    }

    private static Dictionary<string, CustomerData> Load_Customers()
    {
        Dictionary<string, CustomerData> customerData = [];
        foreach ((string key, CharacterData charaData) in Game1.characterData)
        {
            if (GameStateQuery.IsImmutablyFalse(charaData.CanSocialize))
                continue;
            customerData[key] = new CustomerData();
        }
        customerData["Krobus"] = new()
        {
            Condition = "PLAYER_HAS_MAIL Current ccMovieTheater",
            OverrideAppearanceId = "MovieTheater",
            Dialogue = new Dictionary<string, CustomerDialogue>
            {
                [$"{ModEntry.ModId}_Mafia"] = new()
                {
                    Haggle_Ask = $"[LocalizedText {Asset_Strings}:Haggle_Ask_Krobus]",
                    Haggle_Compromise = $"[LocalizedText {Asset_Strings}:Haggle_Compromise_Krobus]",
                    Haggle_Overpriced = $"[LocalizedText {Asset_Strings}:Haggle_Overpriced_Krobus]",
                    Haggle_Success = $"[LocalizedText {Asset_Strings}:Haggle_Success_Krobus]",
                    Haggle_Fail = $"[LocalizedText {Asset_Strings}:Haggle_Fail_Krobus]",
                },
            },
        };
        customerData["George"] = new() { Condition = "FALSE" };
        customerData["Dwarf"] = new() { Condition = "FALSE" };
        customerData["Sandy"] = new() { Chance = 0.5f };
        customerData["Wizard"] = new() { Chance = 0.2f };
        customerData["Linus"] = new() { Chance = 0.2f };
        return customerData;
    }

    private static void Edit_Events_FishShop(IAssetData asset)
    {
        IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
        data[$"{ModEntry.ModId}_WillyWalrus/NpcVisibleHere Willy/EarnedMoney 25000"] =
            $"distantBanjo/6 8/farmer 5 9 0 Willy 5 4 2/setSkipActions AddItem (BC)mushymato.Merchant_CashRegister/skippable/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.01\"/faceDirection farmer 2/playsound doorClose/textAboveHead Willy \"[LocalizedText {Asset_Strings}:WillyWalrus.Willy.02]\"/pause 1000/faceDirection farmer 0/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.03\"/showFrame Willy 25/positionOffset Willy 0 8 true/move farmer 0 -3 0/textAboveHead Willy \"[LocalizedText {Asset_Strings}:WillyWalrus.Willy.04]\"/showFrame Willy 0/positionOffset Willy 0 -8 true/pause 1500/faceDirection farmer 2/itemAboveHead (BC)mushymato.Merchant_CashRegister/setSkipActions null/pause 3300/addItem (BC)mushymato.Merchant_CashRegister/message \"[LocalizedText {Asset_Strings}:CashRegister_Desc]\"/faceDirection farmer 0/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.05\"/emote farmer 8/emote Willy 40/faceDirection Willy 1/pause 500/faceDirection Willy 3/pause 500/faceDirection Willy 2 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.06\"/quickQuestion #[LocalizedText {Asset_Strings}:WillyWalrus.Player.A1]#[LocalizedText {Asset_Strings}:WillyWalrus.Player.A2](break)speak Willy \"{Asset_Strings}:WillyWalrus.Willy.R1\"(break)speak Willy \"{Asset_Strings}:WillyWalrus.Willy.R2\"/speed Willy 1/advancedMove Willy false -3 0 0 4 2 0 0 -2 1 1/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.07\"/faceDirection farmer 3 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.08\"/faceDirection farmer 2 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.09\"/faceDirection farmer 3 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.10\"/emote farmer 40/speed Willy 1/advancedMove Willy false 0 1 2 0 1 1/faceDirection farmer 2 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.11\"/faceDirection Willy 0 true/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.12\"/emote farmer 40/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.13\"/emote Willy 32/speak Willy \"{Asset_Strings}:WillyWalrus.Willy.14\"/pause 2000/end";
    }

    private static void Edit_AudioChanges(IAssetData asset)
    {
        IDictionary<string, AudioCueData> data = asset.AsDictionary<string, AudioCueData>().Data;
        data[Cues.DoorbellCue] = new()
        {
            Id = Cues.DoorbellCue,
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

    private static void Edit_Buildings(IAssetData asset)
    {
        IDictionary<string, BuildingData> data = asset.AsDictionary<string, BuildingData>().Data;
        foreach (BuildingData buildingData in data.Values)
        {
            if (buildingData.ValidOccupantTypes.Contains("Barn"))
            {
                (buildingData.Metadata ??= [])[Metadata_ShopkeepThemeBoosts] = ThemeBoost_Milk;
            }
            if (buildingData.ValidOccupantTypes.Contains("Coop"))
            {
                (buildingData.Metadata ??= [])[Metadata_ShopkeepThemeBoosts] = ThemeBoost_Eggs;
            }
        }
        if (data.TryGetValue("Greenhouse", out BuildingData? buildingD))
        {
            (buildingD.Metadata ??= [])[Metadata_ShopkeepThemeBoosts] = ThemeBoost_Flowers;
        }
    }

    private static void Edit_Shops(IAssetData asset)
    {
        IDictionary<string, ShopData> data = asset.AsDictionary<string, ShopData>().Data;
        if (data.ContainsKey("Carpenter"))
            data["Carpenter"].Items.Add(new() { Id = CashRegisterQId, ItemId = CashRegisterQId });
        data[UpgradeShopId] = new()
        {
            Items =
            [
                new()
                {
                    Id = Upgrades.IQ_ADVERTISE,
                    ItemId = Upgrades.IQ_ADVERTISE,
                    UseObjectDataPrice = true,
                    MinStack = 1,
                    MaxStack = 1,
                },
                new()
                {
                    Id = Upgrades.IQ_AUTO_RESTOCK,
                    ItemId = Upgrades.IQ_AUTO_RESTOCK,
                    UseObjectDataPrice = true,
                    MinStack = 1,
                    MaxStack = 1,
                },
                new() { Id = CashRegisterQId, ItemId = CashRegisterQId },
                new()
                {
                    Id = Upgrades.IQ_ROBO_SHOPKEEP_LEVEL,
                    ItemId = Upgrades.IQ_ROBO_SHOPKEEP_LEVEL,
                    UseObjectDataPrice = true,
                    MinStack = 1,
                    MaxStack = 1,
                },
                new() { Id = RoboShopkeepQId, ItemId = RoboShopkeepQId },
            ],
        };
    }

    public static void Edit_Machines(IAssetData asset)
    {
        IDictionary<string, MachineData> data = asset.AsDictionary<string, MachineData>().Data;
        data[CashRegisterQId] = new() { InteractMethod = GameDelegates.InteractMethod_CashRegister };
        data[RoboShopkeepQId] = new() { InteractMethod = GameDelegates.InteractMethod_RoboShopkeep };
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
            ContextTags = [],
            CustomFields = null,
        };
        data[RoboShopkeepId] = new()
        {
            Name = RoboShopkeepId,
            DisplayName = $"[LocalizedText {Asset_Strings}:RoboShopkeep_Name]",
            Description = $"[LocalizedText {Asset_Strings}:RoboShopkeep_Desc]",
            Price = 200000,
            Fragility = 0,
            CanBePlacedOutdoors = true,
            CanBePlacedIndoors = true,
            IsLamp = false,
            Texture = Asset_TextureCraftables,
            SpriteIndex = 1,
            ContextTags = [ContextTag_RoboShopkeep],
            CustomFields = null,
        };
    }
}
