using Merchant.ModIntegration;
using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace Merchant.Models;

public sealed class ModConfig
{
    public bool EnableAutoRestock { get; set; } = true;
    public bool AllowUnmetCustomers { get; set; } = false;
    public bool HaggleAutoClick { get; set; } = false;
    public int HaggleSpeed { get; set; } = 1500;
    public Point HaggleUIOffset { get; set; } = Point.Zero;

    private void Reset()
    {
        EnableAutoRestock = true;
        AllowUnmetCustomers = false;
        HaggleAutoClick = false;
        HaggleSpeed = 1500;
        HaggleUIOffset = Point.Zero;
    }

    private void Save()
    {
        ModEntry.help.WriteConfig(this);
    }

    public void Register(IManifest mod, IGenericModConfigMenuApi gmcm)
    {
        gmcm.Register(mod, Reset, Save);
        gmcm.AddBoolOption(
            mod,
            () => EnableAutoRestock,
            (value) => EnableAutoRestock = value,
            I18n.Config_AutoRestock_Name,
            I18n.Config_AutoRestock_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => AllowUnmetCustomers,
            (value) =>
            {
                AllowUnmetCustomers = value;
                ModEntry.FriendEntries.ResetFriends();
            },
            I18n.Config_UnmetNpc_Name,
            I18n.Config_UnmetNpc_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => HaggleAutoClick,
            (value) => HaggleAutoClick = value,
            I18n.Config_HaggleAutoclick_Name,
            I18n.Config_HaggleAutoclick_Desc
        );
        gmcm.AddNumberOption(
            mod,
            () => HaggleSpeed,
            (value) => HaggleSpeed = value,
            I18n.Config_HaggleSpeed_Name,
            I18n.Config_HaggleSpeed_Desc,
            1000,
            3000,
            250,
            (value) => $"{value / 1000f:0.00}s"
        );
        gmcm.AddTextOption(
            mod,
            () => $"{HaggleUIOffset.X},{HaggleUIOffset.Y}",
            (value) =>
            {
                string[] parts = value.Split(',');
                if (parts.Length < 2)
                    return;
                if (int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int y))
                {
                    HaggleUIOffset = new(x, y);
                }
                else
                {
                    HaggleUIOffset = Point.Zero;
                }
            },
            I18n.Config_HaggleUiOffset_Name,
            I18n.Config_HaggleUiOffset_Desc
        );

        // gmcm.AddNumberOption(
        //     mod,
        //     () => Game1.player.difficultyModifier,
        //     (value) => Game1.player.difficultyModifier = value,
        //     () => Game1.content.LoadString("Strings\\UI:Character_Difficulty"),
        //     () => Game1.content.LoadString("Strings\\UI:AGO_ProfitMargin_Tooltip"),
        //     min: 0f,
        //     max: 1f,
        //     interval: 0.01f
        // );
    }
}
