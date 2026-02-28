using Merchant.ModIntegration;
using StardewModdingAPI;

namespace Merchant.Models;

public sealed class ModConfig
{
    public bool EnableAutoRestock { get; set; } = true;
    public int HaggleSpeed { get; set; } = 1500;

    private void Reset()
    {
        EnableAutoRestock = true;
        HaggleSpeed = 1500;
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
        gmcm.AddNumberOption(
            mod,
            () => HaggleSpeed,
            (value) => HaggleSpeed = value,
            I18n.Config_HaggleSpeed_Name,
            I18n.Config_HaggleSpeed_Desc,
            1000,
            3000,
            250,
            (value) => $"{value / 1000f:0.00}"
        );
    }
}
