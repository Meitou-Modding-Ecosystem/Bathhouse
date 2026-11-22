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
| Water | Bathhouse Water Tank: Resource storage limited to Water, as the game's Water Tower | Small Water Tank |
| Tubs (no functionality) | Small Tub (1 seat), Medium Tub (2), Large Bath (4), Oil Bath, Blood Bath (+10 Raw Meat to build) | Wooden Barrel, Large Barrels, Booze Barrels, Booze Barrel, Cannibal Butcher Table |
| Service | Massage Bed (bed), Massage Station (production: Towel to Dirty Towel, 1 operator), Check-in Counter (shop counter), Lockers (locked storage), Laundry Hamper, Towel Rack, Tea Table (storage), Review Board | Bed, Small Table, Shop Counter, Crossbow Locker, Small Barrel, Shelf, Small Table, banner_traders |
| Decorations | Zen Garden, Wall Art, Tapestry, Statue, Towel Hanger, Tea Set Display | Carpet: Rusty Rug, banner_empire, banner_holy, Emperor Statue, Wall Shelf 1, Wall Shelf 2 |

Tubs have no functionality so that no game or mod AI (a town bar's, Fiefdom's residents) sits in them; bathing is the mod's code.

The bath goal is an `AI_TASK` "Taking a bath" (`enum` 4401, classification 3 urgent, targeting 1), added with weight 100 to the `AI Goals` of
every RACE and of the player's faction (204 Nameless), the lists the chooser weighs for NPCs and the player's characters. Bathhouse keeps task
type numbers 4401 to 4409.

## Heated tubs

The system (`Bathing/BathhouseSystem`, at the end of the tick) checks once a game second:

- **Heating** (`Heating`): per building (a piece's parent site), boilers counted as built, switched on (`Machine.PowerOn`, not broken) and lit
  (also `HasPower` and `Active`). The rule, in order: no boiler, all off, none lit, no Water in the building's stores (when Water per day is
  above 0), more tub seats than lit boilers x seats per boiler (lukewarm), else hot. Hot and lukewarm are warm; a tub outdoors is cold. Lit
  boilers drink Water per day each, taken unit by unit from the building's storage pieces (the water tank first in site order); what is owed under
  one unit is saved.
- **Player bathers**: a tub's panel has a "Take a bath" button (`BatheCommand`) that sends the player's selected characters (inspecting a
  building keeps the selection). The next check turns each into a soak; one that cannot go (cold tub, full tub) barks why.
- **Soaks** (`Soaks`): a sent bather holds the first free seat and gets the bath goal's desire (0.75, urgent tier; 0 for everyone else).
  When the chooser starts the goal the bather walks to the tub's approach point (15 units in front, local +Z), is placed in its seat (one at the
  centre, two side by side, four on a square, 6 units out) and rests there (`CharacterTask.Rest`, sitting on the ground) on row 4401. Warm
  hours count while it sits; it stands up in front of the tub when the soak's length is reached or the water goes cold. A fight or another goal
  gets it out and the goal starts again later; an order, leaving the world, a gone tub, being knocked down or not getting in within 3 hours
  (plus the hours soaked) calls it off with nothing earned. Soaks are saved by lasting id; a loaded bather walks back in.
- **After a soak** (full or cut short by cold): the Soothed buff (`bathhouse.soothed`, Good, Toughness +5, Athletics +5, refreshed by a new
  soak) for the buff days, every registered Bad status effect removed except those switched off, and a towel used.
- **Towels** (`Towels`): from the bather's inventory, else the building's first store holding one. A used towel comes back dirty at the
  towels-kept chance (`Rng.Hash` of the world seed, the character and the tick), else it is ruined; a dirty one goes into the building's first
  hamper with room, else on the floor. A player's character right-clicks a towel to use it the same way (the dirty one back in its inventory).
- **Panels** (`BathhouseUi`): a tub shows its water and seats taken and the bath button; a boiler shows its building's heat (tooltip: boilers lit, seats heated,
  Water) and one switch per Bad status effect of any mod (the first four) for whether soaks clear it. The switches are saved with the game.
- **Debug row** "Bathhouse: tubs and bathers": each building's heat and each soak.

## Research

All in the Crafting list, no cost: Bathhouse Basics (level 1; loom, press, washer, boiler, water tank, small tub, counter, lockers, hamper,
rack, robe), then Hot Baths (medium and large tubs, review board, decorations), Massage, Tea Service (dryer, kiln, bench, tea table, display)
and Oil Baths (level 2), and Blood Baths after Hot Baths (level 3).

## Settings

`BathhouseSettings`, on the Settings screen's Mods page: seats per boiler (4), Water per boiler a day (2), soak length by tub (small 2 h,
medium 3, large 4, oil and blood 3), Soothed buff (3 days), soaks clear debuffs (on), towels kept (80%), hours between customers (6),
relation lost per complaint (1), most relation lost a day (5), fight chance (5%).

## Tests

`Bathhouse.Tests/RecordTests` applies the records to the installed game (`KENSHI_PATH`; skipped without one) and checks links, machine data,
research and the bath goal's lists. `BathingTests` checks the heat rule, seats, the towel roll's split, which effects a soak clears, soak lengths
and the sidecar round trip.
