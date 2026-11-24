# Bathhouse

Build and run a bathhouse. Design: [Bathhouse mod design](https://claude.ai/artifact/2crHg6Fv1FBEdgfAkrdvC8); tracking issue #1. Targets the
Meitou 0.22.2 nightly (`gameVersion` `>=0.22.2`): its data phase and status effects are not in 0.22.1.

## Records

Added in the data phase (`BathhouseMod.EditData`), so there is no generator and no game content in the repo: each record copies a game record
for its look (a stand-in until the mod's models land) and links game items by id. Ids are in `Records/Ids.cs`; never reuse a number.

| Kind | Records | Copies (look) |
| --- | --- | --- |
| Items | Towel, Dirty Towel, Soap, Greenfruit Tea, Teaware, Tea Set, Used Tea Set, Dirty Robe | Fabrics, Fabrics, Foodcube, Greenfruit, Cup, Ceramic Bowl, Ceramic Bowl, Fabrics |
| Armour | Bathhouse Robe (`slot` 5, body: it replaces body armour; the Monk Robe sits in the belt slot, 14, under body armour; `part coverage` 20 on each arm and the stomach, 27 on the chest, so its total coverage is 100 and the clothing bench makes it from one Fabrics) | Monk Robe |
| Production (function 4) | Towel Loom (Fabrics to 2 Towels), Soap Press (Hemp to Soap), Tea Dryer (Greenfruit to Greenfruit Tea), Pottery Kiln (Raw Stone to Teaware) | Fabric Loom (hemp), Hemp Processor, Biofuel Distillery (Hemp), Cooking Stove |
| Crafting (function 10) | Tea Bench: Tea Set from Teaware + Greenfruit Tea + Water (the Tea Set's `ingredients`; only while the building is hot, Tea works); Washing Machine: a queue of Towel (Dirty Towel + 1/4 Soap) and Bathhouse Robe (Dirty Robe + 1/4 Soap) washes, 1 h each (the items' `ingredients` and `production time`; output holds 10 towels or 1 robe) | Cooking Stove, Sake Still |
| Power | Steam Boiler: draws 10, functionality the game's Lights (a consumer with no operator, so the Power switch shows) | Boiler Tank |
| Water | Bathhouse Water Tank: Resource storage limited to Water, as the game's Water Tower; Water Cistern (Water cistern) | Small Water Tank, Old Water Tower |
| Tubs (Resource storage that takes nothing: `itemtype limit` BUILDING, so only a Shift + right click uses it) | Small Tub (1 seat), Medium Tub (2), Large Bath (4), Oil Bath, Blood Bath (+10 Raw Meat to build) | own model (below), Large Barrels, Booze Barrels, Booze Barrel, Cannibal Butcher Table |
| Service | Massage Bed (bed), Massage Station (Towels only, 5), Check-in Counter (Towels and Soap only, so haulers stock it; grid 12 x 10), Lockers (locks; haulers store only Robes: `itemtype limit` ARMOUR and `limit inventory` Robe and Dirty Robe, two items so the grid stays 8 x 8 instead of one robe's footprint), Laundry Hamper (Dirty Towels only), Robe Hamper (Dirty Robes only), Towel Rack (Towels only), Soap Shelf (Soap only), Tea Table (one Tea Set or Used Tea Set: both listed, grid 2 x 2, one set's footprint), Tea Shelf (Greenfruit Tea only), Teaware Shelf (Teaware only), Tea Set Cabinet (Tea Sets only), Used Tea Rack (Used Tea Sets only), Review Board | Bed, Small Table, Shop Counter, Crossbow Locker, Small Barrel, Small Barrel, Shelf, Shelf, Small Table, Shelf, Shelf, Shelf, Shelf, banner_traders |
| Decorations | Zen Garden, Wall Art, Tapestry, Statue, Towel Hanger, Tea Set Display | Carpet: Rusty Rug, banner_empire, banner_holy, Emperor Statue, Wall Shelf 1, Wall Shelf 2 |

### Values

Matthew's rule: an item is worth its ingredients plus 2% per hour worked. Worth is the game's trade value (`ItemValue`, value x the category
mult: 0.6, greenfruit tea 0.45); an item with `ingredients` is worth theirs times 1 + `profitability`, so a crafted item's profitability is
0.02 x its `production time`. Base game (**Verified**, `ItemValue` over the load order): Hemp 17, Fabrics 56, Greenfruit 27, Raw Stone 36.
Machine hours an item: 24 / (`output rate` x `production mult`).

| Item | From | Hours | Worth |
| --- | --- | --- | --- |
| Soap | 1 Hemp (soap press) | 4 | 18 |
| Towel | 1/2 Fabrics (towel loom) | 0.5 | 27 (as washed: Dirty Towel + 1/4 Soap, 1 h) |
| Dirty Towel | a Towel less its wash | | 22 |
| Greenfruit Tea | 1 Greenfruit (tea dryer) | 0.4 | 27 |
| Teaware | 1 Raw Stone (kiln), or rinsed from a Used Tea Set (Tea works) | 0.5 | 37 |
| Tea Set | Teaware + Greenfruit Tea + Water (tea bench) | 4 | 100 |
| Used Tea Set | Teaware less its rinse | | 32 |
| Bathhouse Robe | 1 Fabrics (clothing bench) | 3 at quality 50 | 60 at quality 50 (`relative price mult` 0.11; 5 to 237 by quality) |
| Dirty Robe | a robe less its wash | | 54 |

The items and the robe show the mod's own icons, not the copied item's: `tools/make-icons.py` fits each `concept-art/<name>-inventory.png`
(transparent background) into the item's inventory footprint at 50 px a cell as `Bathhouse/items/icons/bathhouse-<name>.png`, and the
record names it in `icon` with `auto icon image` false (the game then takes the file by its bare name from its Icons group, which a mod's
`items/icons` folder joins, as Rebirth's `.\mods\Basic\items\icons\Foodcube.png`). Play copies `Bathhouse/items/` into the deployed mod folder (`PlayDeployAssets`). Rerun it when an inventory picture or a footprint changes.
Item models: `Bathhouse/ItemModels/<model>.glb` (textures at 1024; out of `Structures`, which would make them buildable furniture). The
import makes each a mesh `bathhouse_<model>.mesh` and a MATERIAL_SPECS_CLOTHING `<950000 + 100 x block>-Bathhouse.mod`, block from
`ItemModels/ids.json` (docs/structures.md "Item models"); when the deployed data has that material, the item's `mesh`, `ground mesh` and
`material` point at them (`ItemRecords.WearOwnModel`), else it keeps its stand-in's look. The robe keeps the Monk Robe's: a worn model needs
the body skeleton.

Every building is in the build menu's own `BATH HOUSE` category (`RecordKit.BuildCategory`); each shows there once its research is done. Each
building's description says how to use it (what it needs, which staff job its panel offers).

Own models: `Bathhouse/Structures/<piece>.glb` with a `.json` (`research` `none`, so only the piece shows in the build menu); Play's structure
import turns each into a building (docs/structures.md) and the piece copies its look when the deployed data has it (`FurnitureRecords.OwnLook`,
ids in `Ids` from `Structures/ids.json`), else the stand-in. Each model is scaled evenly to the `height` in its `.json` (mesh collision): the art prompts' size (docs/art-prompts.csv)
times 1.2, since pieces at the prompt's size looked small in game. Off-size exports are first rescaled to the prompt's size; `statue-1` leaves
room for more statues. A machine on its own look keeps its stand-in's build cost and power draw
(`MachineRecords.KeepCost`). The massage bed keeps its stand-in's functionality, inventory and `output rate` (`KeepWork`). A copied model carries no use
nodes (NODE_USE 1183-gamedata.base, node type 4): a bed without one is lain in at its origin facing its way, a machine without one is worked
from anywhere within its functionality's `use range` of its origin. The game's Bed lies along local X with a node beside each long side
(z ±12.2, the second turned 180°) and a sleeper lies along X (**Observed**, Matthew's game: on the model turned along Z the customer lay
across it); the two nodes face opposite ways (node yaw - 90), so the side a sleeper comes from decides where its head goes. So the massage bed's model lies along
local X (`yaw` 0, head rest at -X) and keeps only the Bed's -Z node (`KeepUseNodes`; Forgotten Buildings' Bed with Pillow keeps the same one), so every sleeper lies the same way. The node sits 0.5 m above the Bed's
(`LieRaise`): at the Bed's height the customer lay sunk into the higher mattress (**Observed**, Matthew's game). 
- **Work spots** (`WorkSpots`): every tub, machine and service store on its own model gets one use node in front of it (local +Z, the model's
  front), 6 units (0.6 m) past its front face as measured on the model at its finished height, turned -90° as the game's own front nodes are (the
  Shop Counter's and Cooking Stove's at +Z), so its marker (local -X) points back at the piece: the build ghost's green arrow. The machines'
  functionalities have `use range` 10, so operators walk up to the node instead of stopping anywhere near the origin (the washer's operator
  stood inside it). Staff take and put goods, and bathers step in and out of a tub, at the same spot.
Model textures are kept at 2048 (base colour and metal/roughness as JPEG quality 92, normal map as lossless PNG): the import bakes 2048 DDS
by default (`textureSize`), so a 4096 source only grows the repo.
The Tea Set Cabinet model came from a 1.6 million triangle export, cut to 130,000 triangles keeping its UV seams (meshoptimizer's
`gltfpack -si 0.08 -noq -kn`; collapsing across seams smears the textures), then its textures were shrunk to 2048; at 55,000 triangles its
cups and pots turned faceted. The original exports stay in Git history (`git show 9e9dcf7:Bathhouse/Structures/teaset-cabinet.glb`). The Teaware Shelf
(29,000 triangles) and Used Tea Rack (9,400) are the generator's own remeshes, textures shrunk to 2048; cutting them ourselves melted
their small pots and cups. The Used Tea Rack is sized by its width (art prompt 1.2 m x 1.2), so its height was 0.73 m; Matthew asked for it 20% bigger (0.88 m). He also asked for the Tea Table 15% (0.55 m), Tea Bench 30%
(1.56 m) and Washing Machine 10% (2.11 m) bigger; their work-spot faces scale with them.

Tubs have no functionality so that no game or mod AI (a town bar's, Fiefdom's residents) sits in them; bathing is the mod's code.

The goals are `AI_TASK`s "Taking a bath" (`enum` 4401) and "Visiting the bathhouse" (4402), classification 3 urgent, targeting 1, added with
weight 100 to the `AI Goals` of every RACE and of the player's faction (204 Nameless), the lists the chooser weighs for NPCs and the player's
characters. Bathhouse keeps task type numbers 4401 to 4409.

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
  When the chooser starts the goal the bather walks to the tub's work spot (in front, local +Z, Work spots above), is placed in its seat (one at the
  centre, two side by side, four on a square, 6 units out) and rests there (`CharacterTask.Rest`, sitting on the ground) on row 4401. Warm
  hours count while it sits; it stands up in front of the tub when the soak's length is reached or the water goes cold. A fight or another goal
  gets it out and the goal starts again later (one still in its seat, or whose rest something else ended while the goal held, rests again where it
  sits, without standing up first); an order, leaving the world, a gone tub, being knocked down or not getting in within 3 hours
  (plus the hours soaked) calls it off with nothing earned. Soaks are saved by lasting id; a loaded bather walks back in.
- **After a soak** (full or cut short by cold): the Soothed buff (`bathhouse.soothed`, Good, Toughness +5, Athletics +5, refreshed by a new
  soak) for the buff days, every registered Bad status effect removed except those switched off, and a towel used.
- **Towels** (`Towels`): from the bather's inventory, else the building's first store holding one. A used towel comes back dirty at the
  towels-kept chance (`Rng.Hash` of the world seed, the character and the tick), else it is ruined; a dirty one goes into the building's first
  hamper with room, else on the floor. A player's character right-clicks a towel to use it the same way (the dirty one back in its inventory).
- **Robes** (`Robes`): the first time a bather sits down it takes a Bathhouse Robe from a locker of the tub's building. Only the robe's
  equipment slot changes: what is worn there goes into its main grid, else its bag; with no room for it, or with it locked on, or no robe in a
  locker, it soaks as it is. When the soak ends (any end) it puts that piece back on and drops the robe as a Dirty Robe in the building's first
  robe hamper with room, else on the floor. One already wearing a robe keeps it. The look is rebuilt from the worn kit as the engine does
  (`SavedKit.LoadoutOf`, `IAppearanceSource.Create`); the engine has no public re-dress call, so the fighter's armour cover catches up at its
  next refresh. Saved with the soak (sidecar version 5 on).
- **Panels** (`BathhouseUi`): under its Condition bar each piece that works under conditions or with other pieces shows bars (`PanelBars`,
  worked out in the check): a tub Clean or Dirty (`TubCare`), Water (hot full, lukewarm half, cold empty; seats taken over seats) and its
  scrubber; a boiler the hours its building's Water lasts the lit boilers (Water x 24 / (lit x Water per day)) and the seats they heat; a tea
  bench and a used tea rack Steam boiler (lit, none, switched off or no power) and Water, which they need (Tea works), the rack also its rinse
  and whether a teaware shelf has room; a massage bed its customer's massage hours left, its station's attendant (Dexterity as the bar) and
  towels; a massage station its bed, attendant and towels; a tea table its set and tea server; a water cistern its Water stored and carrier.
  The bars come from a Harmony postfix on the engine's `BuildingSelection.BuildingRows` (`ConditionBars`, reached by name; `ConditionBars.Supported` is tested); in a game without it a
  tub's water and seats and a tea piece's hot water stay a line among the buttons. A tub also shows the bath button; a boiler its building's
  heat and one switch per Bad status effect of any mod (the first four) for whether soaks clear it. The switches are saved with the game.
- **Debug row** "Bathhouse: tubs and bathers": each building's heat and each soak.

## Steam

`EffectRecords`, `items/particles/bathhouse_steam.pu`. Each tub and the boiler carry an EFFECT instance (type NONE, copied from the game's
`small- smoke3_small`) naming one of the mod's ParticleUniverse systems: a faint steam rising from a box the size of the tub's water, just under
its rim, and a plume from the boiler's top. The game reads every `.pu` in a mod's folders, so the script ships in `items/particles` (deployed
with the item icons). The systems use the base game's `kenshi_smoke1` material (alpha-blended, not cut inside buildings) tinted white. Meitou
runs a player building's effects whenever it is complete (engine `docs/formats/lights.md` "Effects on buildings"), so a cold tub steams too:
following the water's heat needs an engine hook.

## Tea works

`Bathing/TeaWorks`, stepped in the slow check right after the heat. Both pieces work only in a hot or lukewarm building (Heating), like the tubs.

- **Used tea rack**: rinses one Used Tea Set an hour into Teaware on a teaware shelf in the same building with room; no shelf with room, it waits.
- **Tea bench**: its first queued craft does not advance while the building is cold (its progress is held at the last check's value). The
  engine draws a bench's ingredient sections as its operator works, so a cold bench also gets each section's items and level back to the last
  check's (`TeaWorks.GiveBack`; a section loaded since keeps what it has). Without that, a bench in a building with no boiler ate its
  ingredients and made nothing (**Observed**, Matthew's Settlement Test, issue #124).
- Neither the rinse hours, the held progress nor the held ingredients are saved: a load restarts the current rinse hour.

## Customers

`Customers/Clientele`, stepped in the same check after the soaks.

- **Kinds** (`CustomerTypes`): key, squad template, faction, price tolerance, rowdy, reputation needed. Drifters (3950-gamedata.base, Drifters,
  1.0), traders (1161, Traders Guild, 1.0), bounty hunters (5337, 1.0, reputation 2), Shek (51336-rebirth.mod wandering duelists, 1.0, rowdy,
  reputation 2). Skeletons (1.2), crab raiders (rowdy), nobles (2.0) and cannibals (1.5, rowdy) have lines but no source yet. Reputation is 1
  until milestone 5, so drifters and traders come. Demand (the least quality a kind leaves without complaining) is tolerance / 4.
- **Groups** (`Spawner`): while the bathhouse is open, every hours-between-customers setting, from 6:00 to 22:00, at most 2 groups at once, when
  a finished counter stands in a building with a warm tub. A kind whose faction is not hostile to the player's either way is picked with
  `Rng.Hash`, sized (below), spawned with `SpawnPlatoon` 800 to 1500 units from the counter (homed at the nearest town), tagged `Platoon.ModTag`
  `bathhouse.customers.<n>` (saved by the game: a group finds its platoon again by it), held and sent to the counter's front (20 units along its
  local +Z). Once the platoon loads its living people get visits. A group is let go (hold off, `ModSpawned` off, tag cleared, so the game's
  travel takes it on and may despawn it) when every visit is done, its counter is gone, or after 24 game hours.
- **Group size** (`GroupSize`, `SquadRecords`): 1 to a cap, rolled with `Rng.Hash`. The cap is the most-bathers-per-group setting (3) plus 2 a
  reputation level past 1, at most the tub seats in the counter's building plus 2 and at most 12. Each spawnable kind's game template has a copy
  per size 1 to 12 (ids 1000 + 100 x kind + size): its leader, AI packages, faction and dialogue, plus size - 1 of its first `squad` member (the
  leader's record for the Shek duelist, which has none), no animals or other lists, and `dont multiply`, so the world's Squad size setting does
  not change it (engine `SquadFactory.Count`).
  A group whose platoon disappears is posted and the next may set out at once. A held stand-in takes part in Meitou's off-screen fights
  (Settings > Advanced > Fights far away, on by default), so a group can be wiped out on the road; kept on purpose (README).
- **Order of a visit**: arrive at the counter, tea (optional), check in and bathe, massage (optional), pay. Counters, tea tables and massage
  beds each have an open switch on their panel (`OpenPieceCommand`, saved): a closed counter gets no groups and sends its arrivals away (one
  who had tea pays for it), a closed table or bed is not offered.
- **Check-in**: the visit goal (desire 0.7) walks the customer to the counter's front and stands it there; on its first arrival there it is
  offered tea (below), and comes back to the counter after. It checks in when one of the player's
  people stands within 80 units of the counter and a tub in the building is warm with a free seat. It gets a Towel and a Soap when each is found
  in the counter, else in a store in its building (`Stock`; they may come from different stores), and bathes without one that is not. It takes
  the price (below: the bath plus the towel and soap it gets), a Soap is used up and a Towel goes into its inventory (it dries with it), and it
  is sent to soak. After waiting 2 hours it leaves with a complaint; closing the bathhouse sends arriving customers away.
- **Prices** (`Pricing`): bath (small 45, medium 60, large 75, oil 90, blood 120 cats), towel 27, soap 18 (the items' worth, [Values](#values)), times the reputation factor (1 until
  milestone 5) for the suggested price. The counter's panel sets each service to 50, 75, 100, 125, 150 or 200% of it. A customer takes a visit
  price at or under tolerance x suggested; above it with chance e^(-8 (ratio / tolerance - 1)), 13% at a quarter over, else it leaves.
- **Quality and payment** (`Quality`): 1 less 0.5 x the lukewarm share of the soak, 0.15 a game hour waited (at most 0.3), 0.3 for a soak cut
  short by cold, 0.2 for a dirty tub, 0.1 without a towel, 0.1 without soap, 0.1 without a robe and 0.15 for late tea. At or above 0.8 the customer owes the full price, below it price x quality / 0.8. Check-in prefers a
  clean tub. After a good enough soak it may take a massage, then pays what it owes, the tea included (added to the player's money and posted).
- **Tea**: before check-in, the nearest open tea table of the player's within 3000 units of the counter that no other customer sits at, holding a
  Tea Set, else one a Tea server holds the job at, is offered; with the tea price taken (the same chance rule, ratio = percent / 100) the visit
  goal walks the customer to the table's work spot, where it sits on the floor facing the table (`CharacterTask.Rest` on row 4402, as in a tub;
  drawn cross-legged, see Animations) and waits for a Tea Set, then drinks for 0.5 h. Then the set becomes a Used Tea Set on the table, the
  tea's price (100 cats suggested) is owed, and it goes back to the counter to check in. Waiting more than 2 h for the set makes it late tea
  (quality -0.15, posted once); after 4 h it goes on without. 15% of drinks (seeded per customer and tick) breaks the set instead: no Used Tea
  Set is left, a message names the customer, and it offers to pay 37 cats for it (added to what it owes). Not sitting there within 1 h, the
  table gone or closed, or no tea and no server, it goes on without tea.
- **Massage** (`Massage`): each massage bed is paired with the nearest massage station in its building within 50 units (5 m) of it. With a free
  bed (one whose bed fixture no other visit holds, and whose station somebody holds the Massage attendant job at) and the massage price taken, the customer walks to the bed (visit goal) and lies in it
  (`EnterBed`). While the station's Massage attendant (Staff) stands within 40 units of it and it holds a Towel, each hour pays 300 cats suggested x the attendant's pay
  factor: 0.1 at Dexterity 0 rising evenly to 1 at 100, x 1.2 in a Bathhouse Robe, never past 1. Each hour worked with a customer
  on the bed trains the attendant's Dexterity by 0.5 on the game's curve (about a third of a level an hour at 20). After 1 hour massaged, or 1 hour lying with nobody at the station (or waiting for the bed), it gets up and pays
  the soak, the tea and the massage so far; a massage worked at all uses one Towel from the station, which goes to a hamper as a Dirty Towel.
  The station is a towel store of 5, not a machine: the game's operate job ran any machine with towels in it, and nothing lets a mod hold a
  machine idle (`ProductionSystem.HasWork` asks power, health and supply only).
- **Limited storage** (`FurnitureRecords.Limits`): the hampers, racks, shelves, cabinet and tea table are the game's Resource storage with a
  `limit inventory`, so haulers (who fill the nearest store that `Accepts` an item) never put other goods in them. The game makes a store limited to
  one item one cell of that item's footprint (engine `MachineData.StorageGrid`), and a limited store's stack starts from 1 times its `stackable
  bonus mult` (engine `Inventory.LimitOf`), so the mult is the capacity (`FurnitureRecords.Capacity`: hamper, towel rack and tea shelf 50, soap
  shelf 100, robe hamper and the teaware, tea set and used tea stores 20), as the game's own (Storage: Hemp 250, Water Tank 100).
- **Private beds** (`PrivateBeds`): only customers and the player's orders use a massage bed. Every game bed search (sleeping, "Sleep when
  injured", rescuing or putting someone in the nearest bed, NPC sleep, rented beds) passes over a bed `FixtureRegistry.Reachable` refuses for
  the searcher's faction, so each massage bed is marked unreachable for every faction (`MarkUnreachable`, until the end of the next 24-hour
  period, renewed each period and after a load). A use-bed or put-in-bed order naming the bed, and the customers' `EnterBed`, do not ask.
  Not covered: the engine's `SquadAutonomy.HomeBed`, which gives a code-mod-autonomous player squad the beds of its assigned home in creation
  order without asking. The bed heals as the game's Bed (its stand-in's `output rate`, 8); while someone works its station (`BedHealing`) the mod adds 3 times what
  the game healed of each part since the last check, out of the part's treated damage, so it heals 4 times as fast. No chair
  of the mod has the Chair function (17) or a chair spot, so "Sit when idle" never picks one.
- **Complaints** (`Complaints`): a quality under the kind's demand (or a long wait) pays nothing; the customer's faction loses relation per
  complaint towards the player's (`PlayerRelations.Add`), at most the daily cap per faction. With the fight chance (twice for rowdy kinds) it
  picks a fight: its squad gets a TEMP_ENEMY tag for each of the player's people within 300 units, never the faction.
- **Barks** (`Barks`): two lines per kind for getting into a tub, cold water, a complaint, leaving happy, the price and a fight. English only (the
  engine has no translation catalog for mods).
- **Panels**: the counter has its clerk button, a check-in line ("ready" with the towel and soap it has, or the first of clerk, warm tub, scrubbed tub it lacks;
  tooltip: the fix) and the ledger button; the counter, tea table and massage bed their open switch; the tea table and the massage station have their price line; the review board shows guests of the
  last three days (and their daily average), those ever, failed arrivals and the reviews button. The building panel holds about six lines, so
  the details are in the **ledger** window (`CounterLedger`, the HUD overlay, 900 pixels wide, fits a 1280 x 720 screen) with three tabs. The
  ledger button opens it on Counter, the reviews button on Reviews; either again, or Close, closes it.
  - Counter: check-in and its fix, the open switch with the groups and customers under way, each service's price (- and + step it through 50
    to 200% of suggested, with the price in cats) and the latest six payments and complaints (not saved).
  - Guests (`GuestList`): each group on its way (hours since it set out, hours before it gives up), then up to 14 guests: name, kind, what it
    does (walking to or waiting at the counter, going to or soaking in a tub, on a massage bed), hours left (of the wait, soak or massage),
    quality so far by the payment rule and what costs it (lukewarm water, wait, dirty tub, no towel, soap or robe).
  - Reviews: the last three days' totals and every finished visit, newest first, 16 at a time (Newer, Older).
- **Guest book** (`GuestBook`): every finished visit of the last 72 game hours: time, name, kind, outcome (paid, complained, refused the
  price, failed to arrive) with the cats, quality and reason. Failed: a group gave up on the way (24 h) or was lost, a guest was hurt on the
  way in, gave up before reaching the counter, found the bathhouse closed or its counter gone, waited too long or never got into its tub. It
  also counts guests who ever soaked (paid or complained). The daily average is over the days the record covers (one to three). Saved in the
  sidecar (version 6), not hashed.
- **Debug rows**: "Bathhouse: tubs and bathers" lists groups, visits, staff errands and dirty tubs; "Bathhouse: send customers (kind)" sends a
  group now.

## Staff

`Staffing/`. Six mod job types (`AddJobType`, 4403 to 4408), each in the character's job list on the piece it was given at: the piece's panel
button "Assign selected: <job>" (the panel's first mod line, as lines past the panel's edge are clipped) gives it to the selected characters (a character may hold a job type at several pieces, each job working its own piece, once per piece); the jobs panel shows and removes it and the game
saves it. Each job is also offered (`AddJobOffer`, `StaffRoles.Offer`): the piece's right-click menu shows "Work as: <job>" to selected people, and
other mods read the offers, so Fiefdom's Work tab can staff them. A Shift + right click on the counter, a tub or the massage station works too, with no engine hook: all are storages, so the game gives its Hauling to job
(124) there (at the cistern, an automatic machine, Operating automatic machinery, 152), and the system's serial step (`StaffRoles.TakeClickedJobs`) puts the clerk's, scrubber's or water carrier's job in its place in the list. `Staff` is the jobs' `IJobSource`: when the chooser asks, the job's plan (`Plans`) gives an errand of stops (walk there, work for a
time, then move goods, re-checking they are there); the errand runs on the job's row until done, then the chooser picks again. Errands are not
saved: a loaded staff member plans again.

- **Clerk** (counter): stands 15 units behind the counter for an hour at a time, so it is staffed.
- **Massage attendant** (massage station): stands at the station's work spot, 0.1 h at a time, while someone lies on a bed paired with it and it
  holds a Towel; otherwise it has no work and the next job in the list runs. Only the attendant running the job at that station counts for it
  (an attendant at the next station within reach does not).
- **Tub scrubber** (a tub): scrubs the tub its job is at once it is dirty and nobody soaks in it, 0.5 h (the blood bath 1.5 h). A tub gets dirty
  when a soak in it ends (`TubCare`, saved). Check-in seats customers in a clean tub first, else in a dirty tub nobody holds the scrubber job at
  (the visit is worse); a dirty tub with a scrubber is left to empty and be scrubbed (customers wait at the counter, which then reads
  "needs: a tub scrubbed"). The tub's Scrubber bar says what the scrubber is doing: scrubbing, next (scrubbing another tub), waits (bathers
  in it) or busy (another task). Like any job, it runs only
  when no job above it in the list has work: hauling jobs above it keep a hauler from scrubbing.
- **Laundry runner** (washing machine): loads what it carries that the current wash takes; puts carried clean goods away (towels to the first
  towel rack, else the counter, robes to the first lockers, with room) and dirty goods it cannot load back in their hamper; takes finished goods
  out of the washer to those places; fetches from a hamper only as many dirty goods (up to 10) as the queued washes of the current kind still
  need, since the washer drops what is left in its sections when the next wash takes another item; keeps 3 Soap in the washer from a store in
  the building (not a hamper or the counter). Queue the washes and set Repeat Queue at the washer; it also needs an operator.
- **Tea server** (tea table): puts carried Used Tea Sets in a used tea rack; brings a Tea Set (from a tea set cabinet first) to an empty table; takes a
  table's Used Tea Set to a used tea rack. The used tea rack rinses them back into Teaware (Tea works).
- **Water carrier** (water cistern): while the cistern has room, draws up to 10 Water from the nearest of the player's wells within 3000
  units that holds some (a machine whose output is Water; never a storage, so it does not undo the haulers filling tanks from the cistern) and
  pours it in.

## Water cistern

The game's haulers draw only from machine outputs, never from a storage (`ProductionSystem.HaulSource`), so a Water Tower or water tank never
feeds a Hauling to job. The Water Cistern is a machine instead: its functionality copies the game's well (`function` 4, max operators 0, so it
runs unattended) without the well's world resource, with `output rate` 0 so it makes nothing, and its output holds 200 Water. Haulers fill
water tanks and the tea bench from it as from a well (**Verified** in the headless harness on the Settlement Test save: a Water Tank hauler
drew 30 Water out of it). Its Water carrier keeps it full; its panel shows the Water stored and the carrier.

## Animations

`items/animations/bathhouse_male.skeleton` / `bathhouse_female.skeleton` hold the mod's clips on male_skeleton / female_skeleton bones. The data
phase adds an ANIMATION_FILE (`96-`) naming them and links it to every race whose `male mesh` is under `data/character/meshes/` (humans, shek,
hivers, skeletons, fishmen; not the Hive Queen), and a disabled COMBAT_TECHNIQUE per clip, so no fight picks it (`AnimationRecords`).
- `bathhouse_tea_sit` (`95-`, 2.5 s loop): a customer sitting at a tea table plays it as a mod combat move (`BathhouseMod.TeaSitMove`, from the
  sitters `Clientele` publishes each step), cross-legged facing the table. Meshy's Sit_Cross_Legged_on_Floor retargeted onto both skeletons
  with the Motion Lab (Meitou-Modding-Ecosystem/PZAKenshiMotionLab, `rig --reference`, then `motionlab.kenshi_export`), played in place.

## Research

All in the Crafting list, no cost: Bathhouse Basics (level 1; loom, press, washer, boiler, water tank, water cistern, small tub, counter, lockers, hamper,
rack, robe hamper, soap shelf, robe), then Hot Baths (medium and large tubs, review board, decorations), Massage, Tea Service (dryer, kiln, bench, tea table, tea shelf, teaware shelf, tea set cabinet, used tea rack, display)
and Oil Baths (level 2), and Blood Baths after Hot Baths (level 3).

## Settings

`BathhouseSettings`, on the Settings screen's Mods page: seats per boiler (4), Water per boiler a day (2), soak length by tub (small 2 h,
medium 3, large 4, oil and blood 3), Soothed buff (3 days), soaks clear debuffs (on), towels kept (80%), hours between customers (6), most bathers per group (3),
relation lost per complaint (1), most relation lost a day (5), fight chance (5%). Prices and the open switch are on the counter, saved with the
game (sidecar version 8, which adds the closed pieces and tea before the bath; 7 added the visits' tea; 6 added the guest book; version 2 reads with tea and massage at 100%, before 4 visits had a towel and soap).

## Tests

`Bathhouse.Tests/RecordTests` applies the records to the installed game (`KENSHI_PATH`; skipped without one) and checks links, machine data,
research, the goals' lists, the customer kinds' squads and factions and that each bather template makes its size at any Squad size.
`BathingTests` checks the heat rule, seats, the towel roll's split, which effects a soak clears, soak lengths and the sidecar round trip.
`GuestBookTests` checks the record's pruning, summary and round trip; `LedgerTests` the ledger's tabs, pages and fit.
`CustomerTests` checks price acceptance, the group size cap, the price steps, quality and pay, the complaints' cap, demand and the barks.
`StaffTests` checks dirty tubs, the massage skill, the dirty tub's quality part, tea and massage prices and the staff roles. `BathingTests`
also reads a version 2 sidecar.
