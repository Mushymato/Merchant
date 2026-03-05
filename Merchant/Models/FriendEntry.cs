using Merchant.Management;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace Merchant.Models;

public abstract record BaseFriendEntry(BaseCustomerData? BaseCxData, Friendship? Fren, int MaxHeartCount)
{
    public const int OneHeart = 250;
    public readonly bool IsTourist = BaseCxData?.IsTourist() ?? false;

    public readonly int FrenPoints = Fren?.Points ?? -1;
    public readonly float FrenPercent = (Fren?.Points ?? 0) / (float)(OneHeart * MaxHeartCount);
    public readonly bool IsMaxedHeart = (Fren?.Points ?? -1) >= OneHeart * MaxHeartCount;

    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string SpriteAssetName { get; }
    public abstract AnimatedSprite Sprite { get; }
    public abstract Rectangle MugShotSourceRect { get; }
    public abstract bool ShowShadow { get; }

    public abstract float GetHaggleBaseTargetPointer(ForSaleTarget forSale);

    public abstract float GetHaggleTargetOverRange(ForSaleTarget forSale);

    public abstract int GetGiftTasteForSaleItem(ForSaleTarget forSale);

    public abstract void ApplyChangesToActor(CustomerActor actor);
}

public sealed record FriendEntry(NPC Npc, CustomerData? CxData, Friendship? Fren, int MaxHeartCount)
    : BaseFriendEntry(CxData, Fren, MaxHeartCount)
{
    public override string Name => Npc.Name;
    public override string DisplayName => Npc.displayName;
    public override string SpriteAssetName
    {
        get
        {
            if (
                CxData?.OverrideAppearanceId is string apprId
                && Npc.GetData().Appearance?.FirstOrDefault(appear => appear.Id == apprId)
                    is CharacterAppearanceData overrideAppearance
            )
            {
                return overrideAppearance.Sprite ?? Npc.Sprite.textureName.Value;
            }
            return Npc.Sprite.textureName.Value;
        }
    }
    public override AnimatedSprite Sprite =>
        new(Game1.content, Npc.Sprite.textureName.Value, 0, Npc.Sprite.SpriteWidth, Npc.Sprite.SpriteHeight);
    public override Rectangle MugShotSourceRect => Npc.getMugShotSourceRect();
    private readonly bool showShadow = Npc.GetData()?.Shadow?.Visible ?? true;
    public override bool ShowShadow => showShadow;

    public override float GetHaggleBaseTargetPointer(ForSaleTarget forSale)
    {
        float haggleBaseTarget = FrenPercent * 0.2f + 0.15f * Random.Shared.NextSingle();
        int giftTaste = GetGiftTasteForSaleItem(forSale);
        switch (giftTaste)
        {
            case NPC.gift_taste_stardroptea:
            case NPC.gift_taste_love:
                haggleBaseTarget += 0.3f;
                break;
            case NPC.gift_taste_like:
                haggleBaseTarget += 0.15f;
                break;
            case NPC.gift_taste_dislike:
                haggleBaseTarget -= 0.15f;
                break;
        }
        ModEntry.Log($"haggleBaseTarget: {haggleBaseTarget}");
        return Math.Max(0f, haggleBaseTarget);
    }

    public override float GetHaggleTargetOverRange(ForSaleTarget forSale)
    {
        return 0.15f + FrenPercent * 0.2f + 0.15f * Random.Shared.NextSingle();
    }

    private readonly Dictionary<ForSaleTarget, int> cachedGiftTastes = [];

    public override int GetGiftTasteForSaleItem(ForSaleTarget forSale)
    {
        if (!cachedGiftTastes.TryGetValue(forSale, out int giftTaste))
        {
            giftTaste = Npc.getGiftTasteForThisItem(forSale.Thing);
            cachedGiftTastes[forSale] = giftTaste;
        }
        return giftTaste;
    }

    public override void ApplyChangesToActor(CustomerActor actor)
    {
        actor.displayName = Npc.displayName;
        actor.Portrait = Npc.Portrait;
        if (
            CxData?.OverrideAppearanceId is string apprId
            && actor.GetData().Appearance?.FirstOrDefault(appear => appear.Id == apprId)
                is CharacterAppearanceData overrideAppearance
        )
        {
            if (
                !string.IsNullOrEmpty(overrideAppearance.Sprite)
                && !actor.TryLoadSprites(overrideAppearance.Sprite, out string error, Game1.temporaryContent)
            )
            {
                ModEntry.Log(
                    $"Failed to load sprite from [{actor.Name}].OverrideAppearanceId='{apprId}'",
                    LogLevel.Error
                );
            }
            if (
                !string.IsNullOrEmpty(overrideAppearance.Portrait)
                && !actor.TryLoadPortraits(overrideAppearance.Portrait, out error, Game1.temporaryContent)
            )
            {
                ModEntry.Log(
                    $"Failed to load portrait from [{actor.Name}].OverrideAppearanceId='{apprId}'",
                    LogLevel.Error
                );
            }
        }
    }
}

public sealed record TouristEntry(string TrstId, TouristData TrstData, TourismWaveData WaveData)
    : BaseFriendEntry(TrstData, null, -2)
{
    private readonly FriendEntry? friendEntry =
        TrstData.NPC != null && ModEntry.FriendEntries.TryGetFriendByName(TrstData.NPC, out FriendEntry? friend, true)
            ? friend
            : null;

    public override string Name => TrstId;
    public override string DisplayName =>
        friendEntry?.DisplayName ?? TokenParser.ParseText(TrstData.DisplayName) ?? Name;
    public override string SpriteAssetName =>
        friendEntry?.SpriteAssetName ?? TrstData.Sprite ?? "Characters/Monsters/Skeleton";
    public override AnimatedSprite Sprite =>
        friendEntry?.Sprite ?? new(Game1.content, SpriteAssetName, 0, TrstData.Size.X, TrstData.Size.Y);
    public override Rectangle MugShotSourceRect =>
        friendEntry?.MugShotSourceRect ?? TrstData.MugShotSourceRect ?? new(0, 0, 16, 24);
    public override bool ShowShadow => TrstData.ShowShadow;

    public override float GetHaggleBaseTargetPointer(ForSaleTarget forSale) => 0.6f;

    public override float GetHaggleTargetOverRange(ForSaleTarget forSale) => 0.2f + 0.2f * Random.Shared.NextSingle();

    public override int GetGiftTasteForSaleItem(ForSaleTarget forSale)
    {
        if (WaveData.SplitContextTags.CheckContextTags(forSale.Thing))
            return NPC.gift_taste_love;
        if (TrstData.SplitContextTags.CheckContextTags(forSale.Thing))
            return NPC.gift_taste_love;
        if (!TrstData.UseNPCGiftTastes && friendEntry != null)
            return friendEntry.GetGiftTasteForSaleItem(forSale);
        return NPC.gift_taste_hate;
    }

    public override void ApplyChangesToActor(CustomerActor actor)
    {
        if (friendEntry != null)
        {
            actor.Sprite.textureName.Value = actor.Sprite.textureName.Value;
            friendEntry.ApplyChangesToActor(actor);
        }
        else
        {
            actor.displayName = DisplayName;
            if (!string.IsNullOrEmpty(TrstData.Portrait) && Game1.content.DoesAssetExist<Texture2D>(TrstData.Portrait))
            {
                actor.Portrait = Game1.content.Load<Texture2D>(TrstData.Portrait);
            }
        }
    }
}
