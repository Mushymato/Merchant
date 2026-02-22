using Microsoft.Xna.Framework;
using StardewValley;

namespace Merchant.Management;

public sealed class ShopkeepBrowsing(GameLocation location)
{
    internal CustomerActor? MakeCustomerActor(string npcName)
    {
        if (Game1.getCharacterFromName(npcName) is not NPC sourceNPC)
        {
            return null;
        }
        return new(
            new AnimatedSprite(sourceNPC.Sprite.textureName.Value),
            new Vector2(0, 0),
            location.Name,
            sourceNPC.FacingDirection,
            sourceNPC.Name,
            sourceNPC.Portrait,
            eventActor: true
        );
    }
}
