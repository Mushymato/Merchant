using Microsoft.Xna.Framework;

namespace Merchant.Models;

public sealed class TouristData : BaseCustomerData
{
    public override bool IsTourist() => true;

    public List<string> AppearsDuring { get; set; } = [TourismWaveData.DefaultWave];
    public List<string>? ContextTags { get; set; } = null;

    public string? NPC { get; set; } = null;
    public bool UseNPCGiftTastes { get; set; } = true;

    public string? DisplayName { get; set; } = null;
    public string? Portrait { get; set; } = null;
    public string? Sprite { get; set; } = null;
    public Point Size { get; set; } = new Point(16, 32);
    public Rectangle? MugShotSourceRect { get; set; } = null;
    public bool ShowShadow { get; set; } = true;

    internal List<string[]> SplitContextTags => field ??= ContextTags.SplitContextTags();
}

public sealed class TourismWaveData
{
    internal const string DefaultWave = "Default";

    public string? Condition { get; set; } = null;
    public string? DisplayName { get; set; } = null;
    public string? Description { get; set; } = null;
    public List<string>? ContextTags { get; set; } = null;
    public int TouristMinCount { get; set; } = 4;
    public int TouristMaxCount { get; set; } = -1;

    internal List<string[]> SplitContextTags => field ??= ContextTags.SplitContextTags();
}
