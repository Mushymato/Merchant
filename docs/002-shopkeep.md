## `mushymato.Merchant/ShopkeepThemeBoosts`

You can open a shop in (most) farm buildings, and some farm buildings can have bonuses for certain categories of items.

### Structure
```json
{"mushymato.Merchant_Flowers": {
  // Description shown in bonus menu
  "Description": "[LocalizedText mushymato.Merchant.i18n:Theme_Flowers]",
  // Context tags for which the boost is applicable to
  "ContextTags": [
    "flower_item", // can use "tag1,tag2" for AND
    // can specify additional tags for OR
  ],
  // Boost value
  "Value": 0.2 // maximum 0.5
}}
```

To link a boost to a building, you need to edit the Metadata.

### Building Metadata Keys

| Key | Description |
| --- | ----------- |
| `"mushymato.Merchant/ShopkeepThemeBoosts"` | List of comma separated `mushymato.Merchant/ShopkeepThemeBoosts` keys for boosts associated with this building. |
| `"mushymato.Merchant/ShopkeepCondition"` | A game state query that controls whether the building can be a shop. |
| `"mushymato.Merchant/ShopkeepNotAllowedMessage"` | If this building cannot be a shop, this message is displayed if set. |

### Custom Shop Locations

Besides farm buildings, mods can make a location valid for shopkeeping too.
This can be done by setting `mushymato.Merchant/ShopkeepCondition` to `TRUE` on the custom fields of a location.

The location should has to be decoratable by the player (i.e. have tables) and customers will use the first warp into the location as the entry tile.
