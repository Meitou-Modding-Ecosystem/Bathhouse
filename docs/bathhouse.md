# Bathhouse

Build and run a bathhouse. Design: [Bathhouse mod design](https://claude.ai/artifact/2crHg6Fv1FBEdgfAkrdvC8); tracking issue #1. Targets the
Meitou 0.22.2 nightly (`gameVersion` `>=0.22.2`): its data phase and status effects are not in 0.22.1.

## Records

Added in the data phase (`BathhouseMod.EditData`), so there is no generator and no game content in the repo: each record copies a game record
for its look (a stand-in until the mod's models land) and links game items by id. Ids are in `Records/Ids.cs`; never reuse a number.

| Kind | Records | Copies (look) |
| --- | --- | --- |
| Items | Towel, Dirty Towel, Soap, Greenfruit Tea, Teaware, Tea Set | Fabrics, Fabrics, Foodcube, Greenfruit, Cup, Ceramic Bowl |
| Armour | Bathhouse Robe | Monk Robe |
| Production (function 4) | Towel Loom (Fabrics to 2 Towels), Soap Press (2 Hemp to Soap), Washing Machine (Dirty Towel + 1/4 Soap to Towel), Tea Dryer (Greenfruit to Greenfruit Tea), Pottery Kiln (Raw Stone to Teaware) | Fabric Loom (hemp), Hemp Processor, Sake Still, Biofuel Distillery (Hemp), Cooking Stove |
| Crafting (function 10) | Tea Bench: Tea Set from Teaware + Greenfruit Tea (the Tea Set's `ingredients`) | Cooking Stove |
| Power | Steam Boiler: draws 10, functionality the game's Lights (a consumer with no operator, so the Power switch shows) | Boiler Tank |
| Tubs (no functionality) | Small Tub (1 seat), Medium Tub (2), Large Bath (4), Oil Bath, Blood Bath (+10 Raw Meat to build) | Wooden Barrel, Large Barrels, Booze Barrels, Booze Barrel, Cannibal Butcher Table |
| Service | Massage Bed (bed), Massage Station (production: Towel to Dirty Towel, 1 operator), Check-in Counter (shop counter), Lockers (locked storage), Laundry Hamper, Towel Rack, Tea Table (storage), Review Board | Bed, Small Table, Shop Counter, Crossbow Locker, Small Barrel, Shelf, Small Table, banner_traders |
| Decorations | Zen Garden, Wall Art, Tapestry, Statue, Towel Hanger, Tea Set Display | Carpet: Rusty Rug, banner_empire, banner_holy, Emperor Statue, Wall Shelf 1, Wall Shelf 2 |

Tubs have no functionality so that no game or mod AI (a town bar's, Fiefdom's residents) sits in them; bathing is the mod's code.

## Research

All in the Crafting list, no cost: Bathhouse Basics (level 1; loom, press, washer, boiler, small tub, counter, lockers, hamper, rack, robe),
then Hot Baths (medium and large tubs, review board, decorations), Massage, Tea Service (dryer, kiln, bench, tea table, display) and Oil Baths
(level 2), and Blood Baths after Hot Baths (level 3).

## Settings

`BathhouseSettings`, on the Settings screen's Mods page: seats per boiler (4), Water per boiler a day (2), soak length (3 h), Soothed buff
(3 days), soaks clear debuffs (on), towels kept (80%), hours between customers (6), relation lost per complaint (1), most relation lost a day
(5), fight chance (5%).

## Tests

`Bathhouse.Tests/RecordTests` applies the records to the installed game (`KENSHI_PATH`; skipped without one) and checks links, machine data
and research.
