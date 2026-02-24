## FAQ/Author's Guide

There's a few things you can customize for by adding an entry to `mushymato.Merchant/Customers` with your NPC's internal id.

### Haggle Dialogue

There are 5 dialogue fields on the asset:
- `Haggle_Ask`: When haggle begins.
- `Haggle_Compromise`: When your price is somewhat above the NPC's target price but they are willing to negotiate.
- `Haggle_Overpriced`: When your price is far above the NPC's target price, and they do not want to negotiate.
- `Haggle_Fail`: When you fail to sell the item.
- `Haggle_Success`: When you successfully sold the item.

You can `{0}` for item name and `{1}` for the current price.

Tokenized text is also supported and resolves before substitutions.

### Condition

By default, only social NPCs that the player has met may come and buy things from a player's shop.

You can change when exactly the NPCs come and go by using the `Condition` field.
