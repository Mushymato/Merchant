using Merchant.Misc;
using Merchant.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace Merchant.Menus;

public sealed record SoldRecordDisplay(
    SoldRecord Record,
    Item SoldItem,
    string TooltipDesc,
    Texture2D Sprite,
    Rectangle MugshotSourceRect
) : ISimpleGridDisplay
{
    private readonly string priceText = string.Concat(Record.Price, "$");
    internal const int TEXT_YOFFSET = (SimpleGridMenu.CELL_HEIGHT - 56) / 2;

    public void Draw(SpriteBatch b, ClickableComponent cc, bool isHovered)
    {
        cc.item = SoldItem;
        Rectangle bounds = cc.bounds;
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            SimpleGridMenu.ShopBgRect,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            Color.White,
            scale: 4,
            drawShadow: false
        );
        int xOffset = bounds.X + 10;
        int y = bounds.Y;
        b.Draw(
            Sprite,
            new Rectangle(
                xOffset,
                y + SimpleGridMenu.CELL_HEIGHT - MugshotSourceRect.Height * 4,
                MugshotSourceRect.Width * 4,
                MugshotSourceRect.Height * 4
            ),
            MugshotSourceRect,
            Color.White
        );
        xOffset += MugshotSourceRect.Width * 4 + 4;
        SoldItem.drawInMenu(b, new(xOffset, y + SimpleGridMenu.ICON_YOFFSET), 1f);
        xOffset += 64 + 4;
        SpriteText.drawString(b, priceText, xOffset, y + TEXT_YOFFSET);
    }

    public void DrawToolTip(SpriteBatch b)
    {
        IClickableMenu.drawToolTip(
            b,
            TooltipDesc,
            SoldItem.DisplayName,
            SoldItem,
            moneyAmountToShowAtBottom: (int)Record.Price
        );
    }

    public void LeftClick(IClickableMenu parent) { }
}

public sealed class SessionReportMenu : SimpleGridMenu
{
    public static SessionReportMenu Make(ShopkeepSessionLog sessionLog)
    {
        return new(sessionLog);
    }

    public SessionReportMenu(ShopkeepSessionLog sessionLog)
        : base(4, 8, 300, 80)
    {
        InitializeGridCC(sessionLog.Sales.Count);

        foreach (SoldRecord record in sessionLog.Sales)
        {
            Item soldItem = record.CreateReprItem();
            string characterName = record.Buyer;
            string? SpriteAssetName = null;
            Rectangle mugshotSourceRect;

            if (sessionLog.IsRoboShopkeep)
            {
                ParsedItemData roboShopkeep = ItemRegistry.GetDataOrErrorItem(AssetManager.RoboShopkeepQId);
                Texture2D roboShopkeepTx = roboShopkeep.GetTexture() ?? Game1.objectSpriteSheet;
                Rectangle roboShopkeepSourceRect = roboShopkeep.GetSourceRect();
                gridDisplays.Add(
                    new SoldRecordDisplay(
                        record,
                        soldItem,
                        I18n.Report_Hover_SoldByRobo(),
                        roboShopkeepTx,
                        new(roboShopkeepSourceRect.X, roboShopkeepSourceRect.Y, roboShopkeepSourceRect.Width, 16)
                    )
                );
                continue;
            }

            BaseFriendEntry? fren = null;

            if (record.IsTourist)
            {
                if (AssetManager.Tourists.Data.TryGetValue(record.Buyer, out TouristData? touristData))
                {
                    TouristEntry touristEntry = new(record.Buyer, touristData, null!);
                    fren = touristEntry;
                }
            }
            else
            {
                if (ModEntry.FriendEntries.TryGetFriendByName(record.Buyer, out FriendEntry? friendEntry))
                {
                    fren = friendEntry;
                }
            }

            if (fren != null)
            {
                characterName = fren.DisplayName;
                SpriteAssetName = fren.SpriteAssetName;
                mugshotSourceRect = fren.MugShotSourceRect;
            }
            else
            {
                mugshotSourceRect = new(0, 0, 16, 24);
            }

            Texture2D sprite;
            if (!string.IsNullOrEmpty(SpriteAssetName) && Game1.content.DoesAssetExist<Texture2D>(SpriteAssetName))
                sprite = Game1.content.Load<Texture2D>(SpriteAssetName);
            else
                sprite = Game1.content.Load<Texture2D>("Characters/Monsters/Skeleton");

            gridDisplays.Add(
                new SoldRecordDisplay(
                    record,
                    soldItem,
                    I18n.Report_Hover_BoughtBy(characterName),
                    sprite,
                    mugshotSourceRect
                )
            );
        }

        RepositionAndSnap();
    }
}
