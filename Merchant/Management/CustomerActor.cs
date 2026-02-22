using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Merchant.Management;

public sealed class CustomerActor : NPC
{
    public CustomerActor(
        AnimatedSprite sprite,
        Vector2 position,
        string defaultMap,
        int facingDir,
        string name,
        Texture2D portrait,
        bool eventActor
    )
        : base(sprite, position, defaultMap, facingDir, name, portrait, eventActor)
    {
        forceOneTileWide.Value = true;
    }
}
