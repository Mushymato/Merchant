using System.Text;
using Merchant.Management;

namespace Merchant.Models;

public sealed record ShopBonusStats(
    int StandingDecorCount,
    int TableCount,
    int FloorDecorCount,
    int MapTileCount,
    int UnreachableTableCount
)
{
    private const float FLOOR_COVERAGE_TARGET = 1 / 3f;
    public readonly float StandingDecorBonus = Math.Min(1f, StandingDecorCount / (float)TableCount);
    public readonly float FloorCoverageBonusRaw = Math.Min(
        FLOOR_COVERAGE_TARGET,
        FloorDecorCount / (float)MapTileCount
    );
    public float TotalBonus => StandingDecorBonus * 0.7f + FloorCoverageBonusRaw * 0.3f;

    public string FormatSummary()
    {
        StringBuilder sb = new();
        sb.Append(I18n.Bonus_Title());
        sb.Append("  ^");
        sb.Append("----------------------------------------");
        sb.Append("  ^");
        sb.Append(I18n.Bonus_Decor());
        sb.Append("  ^  ");
        sb.Append(
            I18n.Bonus_Decor_Values(
                StandingDecorCount,
                TableCount,
                $"{StandingDecorBonus:P2}",
                StandingDecorBonus >= 1f ? I18n.Bonus_Capped() : ""
            )
        );
        if (UnreachableTableCount > 0)
        {
            sb.Append("  ^");
            sb.Append(I18n.Bonus_UnreachableTable(UnreachableTableCount));
            sb.Append("  ^  ");
        }
        sb.Append("  ^");
        sb.Append(I18n.Bonus_RugFloor());
        sb.Append("  ^  ");
        sb.Append(
            I18n.Bonus_RugFloor_Values(
                FloorDecorCount,
                MapTileCount,
                $"{FloorCoverageBonusRaw * 3:P2}",
                FloorCoverageBonusRaw >= FLOOR_COVERAGE_TARGET ? I18n.Bonus_Capped() : ""
            )
        );
        sb.Append("  ^");
        float totalBonus = TotalBonus;
        sb.Append(
            I18n.Bonus_Total(
                $"{totalBonus + ShopkeepHaggle.MIN_MULT:P2}",
                $"{totalBonus + ShopkeepHaggle.MIN_MULT + ShopkeepHaggle.MAX_MULT_DELTA:P2}"
            )
        );
        return sb.ToString();
    }
}
