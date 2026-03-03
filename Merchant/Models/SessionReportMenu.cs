using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace Merchant.Models;

public sealed class SessionReportMenu : IClickableMenu
{
    private const int CELL_WIDTH = 300;
    private const int CELL_HEIGHT = 80;
    private const int ROWS = 8;
    private const int COLS = 4;

    public sealed record SoldRecordDisplay(
        SoldRecord Record,
        Item SoldItem,
        string CharacterName,
        Texture2D Sprite,
        Rectangle MugshotSourceRect
    )
    {
        internal readonly Rectangle ShopBgRect = new(384, 396, 15, 15);
        internal readonly string PriceText = string.Concat(Record.Price, "$");

        public void DrawToolTip(SpriteBatch b)
        {
            drawToolTip(
                b,
                I18n.Report_Hover_BoughtBy(CharacterName),
                SoldItem.DisplayName,
                SoldItem,
                moneyAmountToShowAtBottom: (int)Record.Price
            );
        }
    }

    public sealed class SoldRecordComponent(Rectangle bounds, string name) : ClickableComponent(bounds, name)
    {
        private const int ICON_YOFFSET = (CELL_HEIGHT - 64) / 2;
        private const int TEXT_YOFFSET = (CELL_HEIGHT - 60) / 2;

        public void Draw(SpriteBatch b, SoldRecordDisplay displ)
        {
            item = displ.SoldItem;
            drawTextureBox(
                b,
                Game1.mouseCursors,
                displ.ShopBgRect,
                bounds.X,
                bounds.Y,
                CELL_WIDTH,
                CELL_HEIGHT,
                Color.White,
                scale: 4,
                drawShadow: false
            );
            int xOffset = bounds.X + 10;
            int y = bounds.Y;
            b.Draw(
                displ.Sprite,
                new Rectangle(
                    xOffset,
                    y + CELL_HEIGHT - displ.MugshotSourceRect.Height * 4,
                    displ.MugshotSourceRect.Width * 4,
                    displ.MugshotSourceRect.Height * 4
                ),
                displ.MugshotSourceRect,
                Color.White
            );
            xOffset += displ.MugshotSourceRect.Width * 4 + 4;
            displ.SoldItem.drawInMenu(b, new(xOffset, y + ICON_YOFFSET), 1f);
            xOffset += 64 + 4;
            SpriteText.drawString(b, displ.PriceText, xOffset, y + TEXT_YOFFSET);
        }
    }

    private readonly int scrollIdx = 0;
    private readonly List<SoldRecordDisplay> soldRecordDisplays = [];
    private readonly List<SoldRecordComponent> soldRecordCC = [];
    private SoldRecordDisplay? hoveredDisplay = null;

    public static SessionReportMenu Make(ShopkeepSessionLog sessionLog)
    {
        return new(sessionLog);
    }

    private readonly string totalEarningsString;

    public SessionReportMenu(ShopkeepSessionLog sessionLog)
        : base(0, 0, CELL_WIDTH * COLS, CELL_HEIGHT * ROWS, true)
    {
        this.totalEarningsString = I18n.Report_TotalEarnings(sessionLog.Earnings);

        int maxY = 0;
        for (int row = 1; row < ROWS; row++)
        {
            for (int col = 0; col < COLS; col++)
            {
                int idx = col + row * COLS;
                if (idx >= sessionLog.Sales.Count)
                    break;

                int x = xPositionOnScreen + col * CELL_WIDTH;
                int y = yPositionOnScreen + row * CELL_HEIGHT;
                maxY = Math.Max(maxY, row * CELL_HEIGHT);

                int myID = 100 + idx;

                soldRecordCC.Add(
                    new(new(x, y, CELL_WIDTH, CELL_HEIGHT), $"{row}x{col}")
                    {
                        myID = myID,
                        upNeighborID = row > 0 ? myID - COLS : ClickableComponent.ID_ignore,
                        upNeighborImmutable = true,
                        leftNeighborID = col > 0 ? myID - 1 : ClickableComponent.ID_ignore,
                        rightNeighborID = col < COLS - 1 ? myID + 1 : ClickableComponent.ID_ignore,
                        downNeighborID = row < COLS ? myID + COLS : ClickableComponent.ID_ignore,
                        downNeighborImmutable = true,
                    }
                );
            }
        }

        foreach (SoldRecord record in sessionLog.Sales)
        {
            Item soldItem = record.CreateReprItem();
            string characterName = record.Buyer;
            Texture2D sprite;
            Rectangle mugshotSourceRect;
            if (ModEntry.FriendEntries.TryGetByName(record.Buyer, out NPC? npc))
            {
                characterName = npc.displayName;
                sprite = npc.Sprite.Texture;
                mugshotSourceRect = npc.getMugShotSourceRect();
            }
            else
            {
                sprite = Game1.content.Load<Texture2D>("Characters/Monsters/Skeleton");
                mugshotSourceRect = new(0, 0, 16, 24);
            }
            soldRecordDisplays.Add(new(record, soldItem, characterName, sprite, mugshotSourceRect));
        }

        Recenter();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
        {
            populateClickableComponentList();
            snapToDefaultClickableComponent();
        }
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents = [];
        allClickableComponents.AddRange(soldRecordCC);
        if (upperRightCloseButton != null)
        {
            allClickableComponents.Add(upperRightCloseButton);
        }
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = getComponentWithID(100);
        snapCursorToCurrentSnappedComponent();
    }

    private void Recenter()
    {
        Vector2 topLeftPositionForCenteringOnScreen = Utility.getTopLeftPositionForCenteringOnScreen(
            CELL_WIDTH * COLS,
            CELL_HEIGHT * ROWS
        );
        xPositionOnScreen = (int)topLeftPositionForCenteringOnScreen.X;
        yPositionOnScreen = (int)topLeftPositionForCenteringOnScreen.Y;
        base.initialize(xPositionOnScreen, yPositionOnScreen, width, height, true);
        for (int i = 0; i < soldRecordCC.Count; i++)
        {
            SoldRecordComponent comp = soldRecordCC[i];
            comp.bounds.X = xPositionOnScreen + i % COLS * CELL_WIDTH;
            comp.bounds.Y = yPositionOnScreen + (i / COLS + 1) * CELL_HEIGHT;
        }
    }

    internal IEnumerable<(SoldRecordComponent, SoldRecordDisplay)> IterateVisibleSoldRecord()
    {
        for (int i = 0; i < Math.Min(soldRecordCC.Count, soldRecordDisplays.Count - scrollIdx); i++)
        {
            SoldRecordComponent comp = soldRecordCC[i];
            SoldRecordDisplay displ = soldRecordDisplays[scrollIdx + i];
            yield return (comp, displ);
        }
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        foreach ((SoldRecordComponent comp, SoldRecordDisplay displ) in IterateVisibleSoldRecord())
        {
            if (comp.containsPoint(x, y))
            {
                hoveredDisplay = displ;
                return;
            }
        }
        hoveredDisplay = null;
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(b, xPositionOnScreen - 20, yPositionOnScreen - 20, width + 40, height + 40, Color.White);
        SpriteText.drawString(b, totalEarningsString, xPositionOnScreen, yPositionOnScreen);
        foreach ((SoldRecordComponent comp, SoldRecordDisplay displ) in IterateVisibleSoldRecord())
        {
            comp.Draw(b, displ);
        }
        hoveredDisplay?.DrawToolTip(b);
        base.draw(b);
        drawMouse(b);
    }
}
