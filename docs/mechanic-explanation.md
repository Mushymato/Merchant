## Mechanics in Detail

The happenings of 1 shopkeeping session depends on a variety of factors. This describes the maths behind everything and if you don't want to be spoiled, don't read this.

### Decor Bonus Calculation

Decor bonus is internally a number between 0.0 and 1.0 and the sum of these two values:
- Standing decor adds up to 0.7. It is calculated as number of non-table decor / number of tables and full bonus is earned by having at least one decor per table.
- Rug/floor adds up to 0.3. It is calculated as total tiles covered by something / (2/3 times number of reachable tiles).

The base haggle minimum multiplier is 0.5x, add 1/2 of decor bonus to get the final multiplier (i.e. up to 1.0x).
The base haggle maximum multiplier is 0.5x, add decor bonus to get the final multiplier (i.e. up to 2.5x).

### Customers
The standard kind of customers is a sociable NPC (i.e. appears on the social tab with hearts, can accept gifts) that the farmer has met.

Initially you get a maximum of 4 customers in each shopkeeping session, and every shopkeeping session you play add 1 to this max, up to 32

 You will not get more customers in 1 session than number of items you have for sale.

Choice of which customers will come happen in 2 steps:
1. We try to pick 1/3 of the required customers from the "best friends" pool, which are NPCs that you have maximum hearts with (8 for dateable NPC, 10 for others).
2. After that, we choose from the whole pool until the target customer count is reached.

A customer will not come to a shop if everything on sale is a hated gift.

### Purchase Decisions

Once in the shop, a customer will rank everything that is for sale using their gift tastes, and walk towards the item.

They then decide whether they will buy the item. Base chance of purchase is 20%~50% (40%~70% for loved items), and it increases by 10% for every item browsed.

Customers will never buy items they hate. If all remaining items in the shop are hated , they will leave without purchasing.

### Haggling 

What price you actually get is a combination of several factors:

1. Minimum and maximum multiplier (i.e. the start and end of the haggle bar):
    - Entirely decided by your decor bonus, you can reach up to **1.00x~2.50x** with a fully decorated shop.
2. Starting target price (i.e. where the customer's sprite is at on the bar):
    - Base: 0%~15%
    - Friendship with customer: up to 20%
    - Gift taste (Loved: +30%, Liked: +15%, Neutral: 0%, Disliked: -15%, Hated: won't buy)
3. Willingness to Haggle (i.e. the barrier above which negotiation does not work):
    - Base: 15%~30%
    - Friendship with customer: up to 20%
4. Themed Bonuses:
    - These apply on a per building and per item basis, and increases the minimum multiplier.
5. Where you clicked during the minigame:
    - Customer will only accept a price below their target.
    - Going above the target but below the haggle line means they will consider raising prices
    - Going above the haggle line means you lose a chance entirely.
