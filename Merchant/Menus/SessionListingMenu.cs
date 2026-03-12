using Merchant.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace Merchant.Menus;

public sealed record SessionDateDisplay(int Date, long Revenue) : ISimpleGridDisplay
{
    private readonly string dateStr = I18n.Report_Session_DateTotalRevenue(
        WorldDate.ForDaysPlayed(Date).Localize(),
        Revenue
    );
    private readonly Vector2 dateStrSize = Game1.dialogueFont.MeasureString(
        I18n.Report_Session_DateTotalRevenue(WorldDate.ForDaysPlayed(Date).Localize(), Revenue)
    );

    public void Draw(SpriteBatch b, ClickableComponent cc, bool isHovered)
    {
        int yOffset = (int)(cc.bounds.Y + SimpleGridMenu.CELL_HEIGHT - dateStrSize.Y);
        b.DrawString(Game1.dialogueFont, dateStr, new(cc.bounds.X + 10, yOffset), Game1.textColor);
        SpriteText.drawString(b, "$", (int)(cc.bounds.X + 16 + dateStrSize.X), yOffset);
    }

    public void DrawToolTip(SpriteBatch b) { }

    public void LeftClick(IClickableMenu parent) { }
}

public sealed record SessionLogDisplay(int Seq, ShopkeepSessionLog SessionLog, long Revenue) : ISimpleGridDisplay
{
    private const int TEXT_YOFFSET = (SimpleGridMenu.CELL_HEIGHT - 50) / 2;
    private readonly string sessionText = I18n.Report_Session_Seq(
        Seq,
        SessionLog.IsRoboShopkeep
            ? I18n.Report_Session_Roboshopkeep(SessionLog.Sales.Count)
            : I18n.Report_Session_Manual(SessionLog.Sales.Count),
        I18n.Report_Session_Revenue(Revenue)
    );
    private int? sessionTextWidth = null;

    public void Draw(SpriteBatch b, ClickableComponent cc, bool isHovered)
    {
        sessionTextWidth ??= SpriteText.getWidthOfString(sessionText);
        Rectangle bounds = cc.bounds;
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            SimpleGridMenu.ShopBgRect,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            isHovered ? Color.Wheat : Color.White,
            scale: 4,
            drawShadow: false
        );
        SpriteText.drawString(b, sessionText, bounds.X + 20, bounds.Y + TEXT_YOFFSET);
        SpriteText.drawString(b, "$", (int)(bounds.X + 20 + sessionTextWidth), bounds.Y + TEXT_YOFFSET);
    }

    public void DrawToolTip(SpriteBatch b) { }

    public void LeftClick(IClickableMenu parent)
    {
        parent.SetChildMenu(SessionReportMenu.Make(SessionLog));
    }
}

public sealed class SessionListingMenu : SimpleGridMenu
{
    public static bool TryShow(string shopName)
    {
        Dictionary<int, List<SessionLogDisplay>> groupedLogs = [];
        for (int i = 1; i <= ModEntry.ProgressData.Logs.Count; i++)
        {
            ShopkeepSessionLog log = ModEntry.ProgressData.Logs[i - 1];
            if (log.Shop != shopName)
                continue;
            groupedLogs.TryAdd(log.Date, []);
            groupedLogs[log.Date].Add(new SessionLogDisplay(i, log, log.Sales.Sum(sale => sale.Price)));
        }
        if (groupedLogs.Count == 0)
        {
            Game1.drawObjectDialogue(I18n.FailReason_NoReportsToShow());
            return false;
        }
        Game1.activeClickableMenu = new SessionListingMenu(groupedLogs);
        return true;
    }

    private SessionListingMenu(Dictionary<int, List<SessionLogDisplay>> groupedLogs)
        : base(1, 8, 1200, CELL_HEIGHT)
    {
        foreach ((int date, List<SessionLogDisplay> logsOnDate) in groupedLogs.OrderByDescending(kv => kv.Key))
        {
            gridDisplays.Add(new SessionDateDisplay(date, logsOnDate.Sum(log => log.Revenue)));
            logsOnDate.Reverse();
            gridDisplays.AddRange(logsOnDate);
        }

        InitializeGridCC(gridDisplays.Count);

        RepositionAndSnap();
    }
}
