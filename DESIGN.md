How to make a shop?
1. have a farm building with class Shed
2. place a furniture that can open up the shop stats UI
3. begin the shopkeeping minigame from there

Shopkeeping
- Each shop session costs 60 energy and 2hr in single player
- If the items being haggled have the same category they get a chain bonus
- Success gives flat chain mult of +0.05, lasting only this session
- The base rate of hagglings is 0.5x to 1.5x but it's very easy to increase with just some furniture
- Any extra customers will not initiate a haggle and buy at the average of haggle success mult

Haggling gameplay
- Potion Craft style
- Press key on slidey thing to pick a price point, then the customer will counter offer
- Failure to press for 2 whole cycle means you accept the price as is
- Pressing a too high number 1-3 times in a row will fail the haggle with no sale
- Pressing a number below the customer's price makes them accept immediately and ends the haggle with a sale

Shop stats
Influences how many customers you get in "open shop" session
- Friendship: higher friendship with an NPC makes them more likely to visit your shop
- Marketing: you can boost number of customers by investing in promotions, this decreases over time
Since haggling is capped to 6, every extra customer is one more "auto buy"

Influences your minimum and maximum haggling range
- Decor: a nebulous number, calculated from furniture that are not tables with held items in the shop location. Increases haggling range
- Haggling level: not a true skill, but the amount of sales made via manual haggling translate to some level between 1 and 10 amd gives a permanent boost up to 0.50

Other mods can also register bonuses

Robo shopkeep
- Sells stuff as if every customer is extra customer
- Can upgrade them for minor bonus but strictly worse than manual



Dev plan
1. haggle
2. shop visuals and pathfinding
3. minigame start and stop
4. shop stats number fiddling


Sounds
hagglefail: fishEscape
haggleSuccess: newRecord
