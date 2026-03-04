using Merchant.Management;
using Merchant.Models;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Delegates;

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

    private static void MakeTouristActor(
        TouristEntry tourist,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        Point entryPoint,
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
        pickedActors.Add(new(tourist, entryPoint));
        excluding.Add(tourist.TrstId);
        if (tourist.TrstData.NPC != null)
            excluding.Add(tourist.TrstData.NPC);
    }

    internal List<CustomerActor> MakeTouristActors(
        int maxCount,
        Point entryPoint,
        List<ForSaleTarget> forSaleTargets,
        HashSet<string> excluding,
        ref List<CustomerActor> pickedActors
    )
    {
        foreach (ActiveTourismWave wave in ActiveWaves.Values)
        {
            int waveCount = Random.Shared.Next(wave.WaveData.TouristMinCount, wave.WaveData.TouristMaxCount);
            if (waveCount <= 0)
                continue;

            List<int> range = Random.Shared.GetShuffledIdx(0, wave.Tourists.Count);
            foreach (int idx in range)
            {
                MakeTouristActor(wave.Tourists[idx], forSaleTargets, excluding, entryPoint, ref pickedActors);
                if (pickedActors.Count >= waveCount)
                    break;
                if (pickedActors.Count >= maxCount)
                    return pickedActors;
            }
        }

        return pickedActors;
    }
}
