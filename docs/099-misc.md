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

### Sounds

You can alter sounds used in the minigame by adding specific sound cues to [Data/AudioChanges](https://stardewvalleywiki.com/Modding:Audio).

- `mushymato.Merchant_trackGameplay`: music during merchant game, default `event2`
- `mushymato.Merchant_trackReport`: music during the report, default `harveys_theme_jazz`
- `mushymato.Merchant_hagglePing`: sfx when pointer is about to hit target, default `junimoKart_coin`
- `mushymato.Merchant_haggleSlide`: sfx played at different pitch while the pointer star moves across the bar, default `flute`
- `mushymato.Merchant_haggleNegotiate`: sfx when renegotiating the price, default `smallSelect`
- `mushymato.Merchant_haggleSuccess`: sfx when haggle is successful, default `reward`
- `mushymato.Merchant_haggleFailure`: sfx when haggle has failed, default `fishEscape`

Additionally, this cue is already a custom sound:
- `mushymato.Merchant_doorbell`: sfx when a new customer arrives, default `mushymato.Merchant_doorbell`
