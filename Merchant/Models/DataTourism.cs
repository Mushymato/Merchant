using Merchant.Misc;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Merchant.Models;

public sealed class TouristData : BaseCustomerData
{
    public override bool IsTourist() => true;

    public List<string> AppearsDuring { get; set; } = [AssetManager.Default_TourismWave];
    public List<string>? DesiredContextTags { get; set; } = null;

    public string? NPC { get; set; } = null;

    public string? DisplayName { get; set; } = null;
    public string? Portrait { get; set; } = null;
    public string? Sprite { get; set; } = null;
    public Point Size { get; set; } = new Point(16, 32);
    public Rectangle? MugShotSourceRect { get; set; } = null;
}

public sealed class TourismWaveData
{
    public string? Condition { get; set; } = null;
    public string? DisplayName { get; set; } = null;
    public List<string>? DesiredContextTags { get; set; } = null;
    public int TouristMinCount { get; set; } = 4;
    public int TouristMaxCount { get; set; } = 8;

    private int cacheDay = -1;
    private bool cacheActiveState = false;

    internal bool WaveActiveToday()
    {
        if (Game1.Date.TotalDays == cacheDay)
        {
            return cacheActiveState;
        }
        cacheDay = Game1.Date.TotalDays;
        cacheActiveState = GameStateQuery.CheckConditions(Condition, new());
        return cacheActiveState;
    }
}
