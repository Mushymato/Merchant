using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Merchant.Menus;

public interface ISimpleGridDisplay
{
    public void Draw(SpriteBatch b, ClickableComponent cc, bool isHovered);
    public void DrawToolTip(SpriteBatch b);
    public void LeftClick(IClickableMenu parent);
}

public class SimpleGridMenu(int cols, int rows, int cellW, int cellH)
    : IClickableMenu(0, 0, cols * cellW, rows * cellH, true)
{
    internal const int BASE_CC_ID = 100;
    internal static readonly Rectangle ShopBgRect = new(384, 396, 15, 15);
    internal const int CELL_HEIGHT = 80;
    internal const int ICON_YOFFSET = (CELL_HEIGHT - 64) / 2;

    public int scrollIdx = 0;
    public readonly List<ISimpleGridDisplay> gridDisplays = [];
    public readonly List<ClickableComponent> gridCC = [];
    public ISimpleGridDisplay? hoveredDisplay = default;

    public static void DrawCurrency(SpriteBatch b, Vector2 pos, int currency)
    {
        Utility.drawWithShadow(
            b,
            Game1.mouseCursors,
            pos,
            new Rectangle(193 + currency * 9, 373, 9, 10),
            Color.White,
            0f,
            Vector2.Zero
        );
    }

    public virtual void InitializeGridCC(int dispCount)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int idx = col + row * cols;
                if (idx >= dispCount)
                    break;

                int x = xPositionOnScreen + col * cellW;
                int y = yPositionOnScreen + row * cellH;

                int myID = BASE_CC_ID + idx;

                gridCC.Add(
                    new(new(x, y, cellW, cellH), $"{row}x{col}")
                    {
                        myID = myID,
                        upNeighborID = row > 0 ? myID - cols : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                        upNeighborImmutable = true,
                        leftNeighborID = col > 0 ? myID - 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                        rightNeighborID = col < cols - 1 ? myID + 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                        downNeighborID = row < rows - 1 ? myID + cols : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                        downNeighborImmutable = true,
                    }
                );
            }
        }
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents = [];
        allClickableComponents.AddRange(gridCC);
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

    public void RepositionAndSnap()
    {
        Recenter();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
        {
            populateClickableComponentList();
            snapToDefaultClickableComponent();
        }
    }

    public void Recenter()
    {
        Vector2 topLeftPositionForCenteringOnScreen = Utility.getTopLeftPositionForCenteringOnScreen(
            cellW * cols,
            cellH * rows
        );
        xPositionOnScreen = (int)topLeftPositionForCenteringOnScreen.X;
        yPositionOnScreen = (int)topLeftPositionForCenteringOnScreen.Y;
        base.initialize(xPositionOnScreen, yPositionOnScreen, width, height, true);
        for (int i = 0; i < gridCC.Count; i++)
        {
            ClickableComponent comp = gridCC[i];
            comp.bounds.X = xPositionOnScreen + i % cols * cellW;
            comp.bounds.Y = yPositionOnScreen + i / cols * cellH;
        }
    }

    public IEnumerable<(ClickableComponent, ISimpleGridDisplay)> IterateVisibleSoldRecord()
    {
        for (int i = 0; i < Math.Min(gridCC.Count, gridDisplays.Count - scrollIdx); i++)
        {
            ClickableComponent comp = gridCC[i];
            ISimpleGridDisplay displ = gridDisplays[scrollIdx + i];
            yield return (comp, displ);
        }
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        foreach ((ClickableComponent comp, ISimpleGridDisplay displ) in IterateVisibleSoldRecord())
        {
            if (comp.containsPoint(x, y))
            {
                hoveredDisplay = displ;
                return;
            }
        }
        hoveredDisplay = default;
    }

    public override void receiveScrollWheelAction(int direction)
    {
        ScrollGrid(direction);
        base.receiveScrollWheelAction(direction);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton?.containsPoint(x, y) ?? false)
        {
            base.receiveLeftClick(x, y, playSound);
            return;
        }
        if (Game1.activeClickableMenu != null)
            foreach ((ClickableComponent comp, ISimpleGridDisplay displ) in IterateVisibleSoldRecord())
            {
                if (comp.containsPoint(x, y))
                {
                    displ.LeftClick(this);
                    return;
                }
            }
    }

    protected override void customSnapBehavior(int direction, int oldRegion, int oldID)
    {
        if (oldID >= BASE_CC_ID && oldID < BASE_CC_ID + cols)
        {
            ScrollGrid(1);
        }
        else if (oldID >= BASE_CC_ID + cols * (rows - 1))
        {
            ScrollGrid(-1);
        }
    }

    public bool ScrollGrid(int direction)
    {
        bool scrolled = false;
        if (direction > 0 && scrollIdx >= cols)
        {
            scrollIdx -= cols;
            scrolled = true;
        }
        else if (direction < 0 && scrollIdx < Math.Max(0, gridDisplays.Count - gridCC.Count))
        {
            scrollIdx += cols;
            scrolled = true;
        }
        return scrolled;
    }

    public override void draw(SpriteBatch b)
    {
        if (_childMenu != null)
            return;
        drawTextureBox(b, xPositionOnScreen - 20, yPositionOnScreen - 20, width + 40, height + 40, Color.White);
        foreach ((ClickableComponent comp, ISimpleGridDisplay displ) in IterateVisibleSoldRecord())
        {
            displ.Draw(b, comp, displ == hoveredDisplay);
        }
        hoveredDisplay?.DrawToolTip(b);
        base.draw(b);
        drawMouse(b);
    }
}
