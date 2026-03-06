## Misc Features

### Trigger `mushymato.Merchant_Sold`

This trigger is raised whenever the player sells something. It happens in the middle of the minigame.

- The target item is the item which was just sold
- It will have mod data about the buyer 

Note: If BETAS is installed, the trigger `Spiderbuttons.BETAS_ItemShipped` is also raised.

There are 2 GSQs you can use with this trigger:

- `mushymato.Merchant_SOLD_BUYER <buyer>` checks if the item is sold to a particular buyer.
- `mushymato.Merchant_SOLD_PRICE <minPrice> [maxPrice]` checks if the item's sold price is between min and max.

### Interact Method `Merchant.Models.GameDelegates, Merchant: InteractCashRegister`

This is the interact method used to show the merchant minigame menu.
To turn a custom big craftable into a cash register, you need to edit `Data/Machines` with entry like this:

```json
{"(BC)You.YourMod_YourCraftable": {
  "InteractMethod": "Merchant.Models.GameDelegates, Merchant: InteractCashRegister"
}}
```

### Tile Action `mushymato.Merchant_CashRegister`

This tile action works just like interacting with the cash register big craftable.
You can create custom cash register items using a framework that attaches tile action to furniture, such as spacecore.
