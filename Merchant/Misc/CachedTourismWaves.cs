using Merchant.Models;
using StardewValley;
using StardewValley.Delegates;

namespace Merchant.Misc;

internal record ActiveTourismWave(TourismWaveData Data, List<NPC> Tourists);

internal sealed class CachedTourismWaves(Farmer player)
{
    private Dictionary<string, ActiveTourismWave> activeWaves = [];
    private readonly GameStateQueryContext gsqContext = new(null, player, null, null, Random.Shared);

    // private int cacheDay = -1;
    // private bool cacheActiveState = false;

    // internal bool WaveActiveToday()
    // {
    //     if (Game1.Date.TotalDays == cacheDay)
    //     {
    //         return cacheActiveState;
    //     }
    //     cacheDay = Game1.Date.TotalDays;
    //     cacheActiveState = GameStateQuery.CheckConditions(Condition, new());
    //     return cacheActiveState;
    // }
}
