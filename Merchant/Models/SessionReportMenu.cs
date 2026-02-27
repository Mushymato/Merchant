using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace Merchant.Models;

public sealed record SoldRecordDisplay(
    SoldRecord Record,
    ParsedItemData ItemData,
    Texture2D Sprite,
    Rectangle MugshotSourceRect
);

public sealed class SessionReportMenu : IClickableMenu
{
    private const int WIDTH = 1200;
    private const int HEIGHT = 640;
    private readonly ShopkeepSessionLog sessionLog;

    public readonly List<SoldRecordDisplay> soldRecordDisplays = [];

    public static SessionReportMenu Make(ShopkeepSessionLog sessionLog)
    {
        Vector2 topLeftPositionForCenteringOnScreen = Utility.getTopLeftPositionForCenteringOnScreen(WIDTH, HEIGHT);
        return new(sessionLog, (int)topLeftPositionForCenteringOnScreen.X, (int)topLeftPositionForCenteringOnScreen.Y);
    }

    public SessionReportMenu(ShopkeepSessionLog sessionLog, int x, int y)
        : base(x, y, WIDTH, HEIGHT, true)
    {
        this.sessionLog = sessionLog;
        foreach (SoldRecord record in sessionLog.Sales)
        {
            ParsedItemData ItemData = ItemRegistry.GetDataOrErrorItem(record.ItemId);
            Texture2D Sprite;
            Rectangle MugshotSourceRect;
            if (ModEntry.FriendEntries.TryGetByName(record.Buyer, out NPC? npc))
            {
                Sprite = npc.Sprite.Texture;
                MugshotSourceRect = npc.getMugShotSourceRect();
            }
            else
            {
                Sprite = Game1.content.Load<Texture2D>("Characters/Monsters/Skeleton");
                MugshotSourceRect = new(0, 0, 16, 24);
            }
            soldRecordDisplays.Add(new(record, ItemData, Sprite, MugshotSourceRect));
        }
    }

    public override void draw(SpriteBatch b)
    {
        base.draw(b);
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
    }
}
