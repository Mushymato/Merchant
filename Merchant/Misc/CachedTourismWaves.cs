using System.Text;
using Merchant.Management;
using Merchant.Models;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.TokenizableStrings;

namespace Merchant.Misc;

internal record ActiveTourismWave(string WaveId, TourismWaveData WaveData, List<TouristEntry> Tourists);

internal sealed class CachedTourismWaves(Farmer player)
{
    private int cacheDay = -1;
    private Dictionary<string, ActiveTourismWave>? activeWaves = null;
    private readonly GameStateQueryContext gsqContext = new(null, player, null, null, Random.Shared);

    internal Dictionary<string, ActiveTourismWave> ActiveWaves
    {
        get
        {
            if (Game1.Date.TotalDays == cacheDay && activeWaves != null)
                return activeWaves;
            cacheDay = Game1.Date.TotalDays;
            ActiveTourismWave defaultWave = new(
                TourismWaveData.DefaultWave,
                new() { TouristMinCount = 1, TouristMaxCount = 4 },
                []
            );
            activeWaves = [];
            activeWaves[defaultWave.WaveId] = defaultWave;
            foreach ((string waveId, TourismWaveData waveData) in AssetManager.TourismWaves.Data)
            {
                if (GameStateQuery.CheckConditions(waveData.Condition, gsqContext))
                {
                    waveData.TouristMaxCount = Math.Max(waveData.TouristMinCount, waveData.TouristMaxCount);
                    activeWaves[waveId] = new(waveId, waveData, []);
                }
            }
            foreach ((string trstId, TouristData trstData) in AssetManager.Tourists.Data)
            {
                if (!trstData.WillComeToShop(gsqContext))
                    continue;

                foreach (string waveId in trstData.AppearsDuring)
                {
                    if (activeWaves.TryGetValue(waveId, out ActiveTourismWave? wave))
                    {
                        wave.Tourists.Add(new TouristEntry(trstId, trstData, wave.WaveData));
                    }
                }
            }
            return activeWaves;
        }
    }

    internal bool HasActiveWaves()
    {
        return ActiveWaves.Count > 1;
    }

    private static void MakeTouristActor(
        TouristEntry tourist,
        LocationTopology pathableLocation,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        ref List<CustomerActor> pickedActors
    )
    {
        if (excluding.Contains(tourist.TrstId))
            return;
        if (tourist.TrstData.NPC != null && excluding.Contains(tourist.TrstData.NPC))
            return;
        if (tourist.BaseCxData != null && Random.Shared.NextSingle() > tourist.BaseCxData.Chance)
            return;
        if (forSaleTargets.All(forSale => tourist.GetGiftTasteForSaleItem(forSale) == NPC.gift_taste_hate))
            return;
        pickedActors.Add(new(tourist, pathableLocation));
        excluding.Add(tourist.TrstId);
        if (tourist.TrstData.NPC != null)
            excluding.Add(tourist.TrstData.NPC);
    }

    internal List<CustomerActor> MakeTouristActors(
        int maxCount,
        LocationTopology locationTopology,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        ref List<CustomerActor> pickedActors
    )
    {
        foreach (ActiveTourismWave wave in ActiveWaves.Values)
        {
            int totalMatchingItems = int.MaxValue;
            if (wave.WaveData.SplitContextTags.Any())
                totalMatchingItems = forSaleTargets.Count(forSale =>
                    wave.WaveData.SplitContextTags.CheckContextTags(forSale.Thing)
                );
            if (totalMatchingItems == 0)
                continue;

            int waveCount = Math.Min(
                totalMatchingItems,
                Random.Shared.Next(
                    wave.WaveData.TouristMinCount,
                    Math.Max(wave.WaveData.TouristMinCount, wave.WaveData.TouristMaxCount)
                )
            );
            if (waveCount <= 0)
                continue;

            List<int> range = Random.Shared.GetShuffledIdx(0, wave.Tourists.Count);
            foreach (int idx in range)
            {
                MakeTouristActor(wave.Tourists[idx], locationTopology, forSaleTargets, excluding, ref pickedActors);
                if (pickedActors.Count >= waveCount)
                    break;
                if (pickedActors.Count >= maxCount)
                    return pickedActors;
            }
        }

        return pickedActors;
    }

    internal string FormatSummary()
    {
        StringBuilder sb = new();
        sb.Append(I18n.Tourism_Title());
        sb.Append(ShopBonusStats.LINEBREAK);
        foreach (ActiveTourismWave wave in ActiveWaves.Values)
        {
            if (wave.WaveId == TourismWaveData.DefaultWave)
                continue;
            sb.Append('^');
            sb.Append(TokenParser.ParseText(wave.WaveData.DisplayName) ?? wave.WaveId);
            if (TokenParser.ParseText(wave.WaveData.Description) is string desc)
            {
                sb.Append("^  ");
                sb.Append(desc);
            }
        }
        return sb.ToString();
    }
}
