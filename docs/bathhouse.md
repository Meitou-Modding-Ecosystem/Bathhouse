# Bathhouse

Build and run a bathhouse. Design: [Bathhouse mod design](https://claude.ai/artifact/2crHg6Fv1FBEdgfAkrdvC8); tracking issue #1. Targets the
Meitou 0.22.2 nightly (`gameVersion` `>=0.22.2`): its data phase and status effects are not in 0.22.1.

## Records

Added in the data phase (`BathhouseMod.EditData`), so there is no generator and no game content in the repo: each record copies a game record
for its look (a stand-in until the mod's models land) and links game items by id. Ids are in `Records/Ids.cs`; never reuse a number.

| Kind | Records | Copies (look) |
| --- | --- | --- |
| Items | Towel, Dirty Towel, Soap, Greenfruit Tea, Teaware, Tea Set, Used Tea Set | Fabrics, Fabrics, Foodcube, Greenfruit, Cup, Ceramic Bowl, Ceramic Bowl |
| Armour | Bathhouse Robe | Monk Robe |
| Production (function 4) | Towel Loom (Fabrics to 2 Towels), Soap Press (2 Hemp to Soap), Washing Machine (Dirty Towel + 1/4 Soap to Towel), Tea Dryer (Greenfruit to Greenfruit Tea), Pottery Kiln (Raw Stone to Teaware) | Fabric Loom (hemp), Hemp Processor, Sake Still, Biofuel Distillery (Hemp), Cooking Stove |
| Crafting (function 10) | Tea Bench: Tea Set from Teaware + Greenfruit Tea (the Tea Set's `ingredients`) | Cooking Stove |
| Power | Steam Boiler: draws 10, functionality the game's Lights (a consumer with no operator, so the Power switch shows) | Boiler Tank |
| Water | Bathhouse Water Tank: Resource storage limited to Water, as the game's Water Tower | Small Water Tank |
| Tubs (no functionality) | Small Tub (1 seat), Medium Tub (2), Large Bath (4), Oil Bath, Blood Bath (+10 Raw Meat to build) | own model (below), Large Barrels, Booze Barrels, Booze Barrel, Cannibal Butcher Table |
| Service | Massage Bed (bed), Massage Station (production: Towel to Dirty Towel, 1 operator), Check-in Counter (shop counter), Lockers (locked storage), Laundry Hamper, Towel Rack, Tea Table (storage), Review Board | Bed, Small Table, Shop Counter, Crossbow Locker, Small Barrel, Shelf, Small Table, banner_traders |
| Decorations | Zen Garden, Wall Art, Tapestry, Statue, Towel Hanger, Tea Set Display | Carpet: Rusty Rug, banner_empire, banner_holy, Emperor Statue, Wall Shelf 1, Wall Shelf 2 |

The items and the robe show the mod's own icons, not the copied item's: `tools/make-icons.py` fits each `concept-art/<name>-inventory.png`
(transparent background) into the item's inventory footprint at 50 px a cell as `Bathhouse/items/icons/bathhouse-<name>.png`, and the
record names it in `icon` with `auto icon image` false (the game then takes the file by its bare name from its Icons group, which a mod's
`items/icons` folder joins, as Rebirth's `.\mods\Basic\items\icons\Foodcube.png`). Rerun it when an inventory picture or a footprint changes.
Item models (`Bathhouse/ItemModels/*.glb`, textures at 1024) are kept out of `Structures`, which would make them buildable furniture; the
structure import makes buildings only, so the items still look like their stand-ins in the world.

Own models: `Bathhouse/Structures/<piece>.glb` with a `.json` (`research` `none`, so only the piece shows in the build menu); Play's structure
import turns each into a building (docs/structures.md) and the piece copies its look when the deployed data has it (`FurnitureRecords.OwnLook`,
ids in `Ids` from `Structures/ids.json`), else the stand-in. Each model is scaled evenly so its length matches the art prompts' size, mesh collision:
small tub 1.2 m long (0.62 m tall), medium 2 m (0.96), large 3.5 m (0.5), oil 1.8 m (1.03), blood 2.5 m (1.53), steam boiler 2.2 m tall,
massage bed 2 m long (0.72 m tall), massage station 1 m tall (1.07 m long), check-in counter 2.5 m long (0.95 m tall), laundry hamper
0.8 m tall. Later exports come in meters at the prompt's height and keep it: soap press 1.8 m, tea bench 1 m, tea table 0.4 m, towel loom
1.8 m, washing machine 1.6 m, tea dryer 1.6 m, pottery kiln 2 m, towel rack 1.6 m, locker bank 1.7 m, review board 1.8 m. A machine on its own look keeps its stand-in's build cost and power draw
(`MachineRecords.KeepCost`). The massage bed and the counter keep their stand-in's functionality, inventory and `output rate` (`KeepWork`; the copy carries no use nodes, so a
customer lies at the bed's origin facing its way, as on the stand-in); the station's operator stands within the
functionality's use range of its origin (a building with fewer than two use nodes is reached at its position marker).
Model textures are kept at 2048 (base colour and metal/roughness as JPEG quality 92, normal map as lossless PNG): the import bakes 2048 DDS
by default (`textureSize`), so a 4096 source only grows the repo.

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
- **Panels** (`BathhouseUi`): a tub shows its water and seats taken and the bath button; a boiler shows its building's heat (tooltip: boilers lit,
  seats heated, Water) and one switch per Bad status effect of any mod (the first four) for whether soaks clear it. The switches are saved with
  the game.
- **Debug row** "Bathhouse: tubs and bathers": each building's heat and each soak.

## Customers

`Customers/Clientele`, stepped in the same check after the soaks.

- **Kinds** (`CustomerTypes`): key, squad template, faction, price tolerance, rowdy, reputation needed. Drifters (3950-gamedata.base, Drifters,
  1.0), traders (1161, Traders Guild, 1.0), bounty hunters (5337, 1.0, reputation 2), Shek (51336-rebirth.mod wandering duelists, 1.0, rowdy,
  reputation 2). Skeletons (1.2), crab raiders (rowdy), nobles (2.0) and cannibals (1.5, rowdy) have lines but no source yet. Reputation is 1
  until milestone 5, so drifters and traders come. Demand (the least quality a kind leaves without complaining) is tolerance / 4.
- **Groups** (`Spawner`): while the bathhouse is open, every hours-between-customers setting, from 6:00 to 22:00, at most 2 groups at once, when
  a finished counter stands in a building with a warm tub. A kind whose faction is not hostile to the player's either way is picked with
  `Rng.Hash`, spawned with `SpawnPlatoon` 800 to 1500 units from the counter (homed at the nearest town), tagged `Platoon.ModTag`
  `bathhouse.customers.<n>` (saved by the game: a group finds its platoon again by it), held and sent to the counter's front (20 units along its
  local +Z). Once the platoon loads its living people get visits. A group is let go (hold off, `ModSpawned` off, tag cleared, so the game's
  travel takes it on and may despawn it) when every visit is done, its counter is gone, or after 24 game hours.
  A group whose platoon disappears is posted and the next may set out at once. A held stand-in takes part in Meitou's off-screen fights
  (Settings > Advanced > Fights far away, on by default), so a group can be wiped out on the road; kept on purpose (README).
- **Check-in**: the visit goal (desire 0.7) walks the customer to the counter's front and stands it there. It checks in when one of the player's
  people stands within 80 units of the counter, the counter (else a store in its building) holds a Towel and a Soap, and a tub in the building
  is warm with a free seat. It takes the price (below), a Soap is used up and a Towel goes into its inventory (it dries with it), and it is sent
  to soak. After waiting 2 hours it leaves with a complaint; closing the bathhouse sends arriving customers away.
- **Prices** (`Pricing`): bath (small 30, medium 40, large 50, oil 60, blood 80 cats), towel 15, soap 10, times the reputation factor (1 until
  milestone 5) for the suggested price. The counter's panel sets each service to 50, 75, 100, 125, 150 or 200% of it. A customer takes a visit
  price at or under tolerance x suggested; above it with chance e^(-8 (ratio / tolerance - 1)), 13% at a quarter over, else it leaves.
- **Quality and payment** (`Quality`): 1 less 0.5 x the lukewarm share of the soak, 0.15 a game hour waited (at most 0.3), 0.3 for a soak cut
  short by cold and 0.2 for a dirty tub. At or above 0.8 the customer owes the full price, below it price x quality / 0.8. Check-in prefers a
  clean tub. After a good enough soak it buys tea, may take a massage, then pays what it owes (added to the player's money and posted).
- **Tea**: when a tea table in the tub's building holds a Tea Set and the customer takes the tea price (the same chance rule, ratio = percent /
  100), the set becomes a Used Tea Set on the table and the tea's price (20 cats suggested) is owed.
- **Massage** (`Massage`): each massage bed is paired with the nearest massage station in its building. With a free bed (one whose bed fixture
  no other visit holds) and the massage price taken, the customer walks to the bed (visit goal) and lies in it (`EnterBed`). While one of the
  player's people operates the station (a vanilla operate-machinery job, within 40 units), each hour pays 30 cats suggested x skill (0.5 + 0.5 x
  Dexterity / 50, 0.5 to 1.5). After 1 hour massaged, or 1 hour lying with nobody at the station (or waiting for the bed), it gets up and pays
  the soak, the tea and the massage so far.
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
- **Panels**: the counter has an open switch (tooltip: groups and customers under way, the latest payment or complaint) and the bath, towel and
  soap price lines; the tea table and the massage station have their price line.
- **Debug rows**: "Bathhouse: tubs and bathers" lists groups, visits, staff errands and dirty tubs; "Bathhouse: send customers (kind)" sends a
  group now.

## Staff

`Staffing/`. Four mod job types (`AddJobType`, 4403 to 4406), each in the character's job list on the piece it was given at: the piece's panel
button "Assign selected: <job>" gives it to the selected characters (one job of a type each); the jobs panel shows and removes it and the game
saves it. `Staff` is the jobs' `IJobSource`: when the chooser asks, the job's plan (`Plans`) gives an errand of stops (walk there, work for a
time, then move goods, re-checking they are there); the errand runs on the job's row until done, then the chooser picks again. Errands are not
saved: a loaded staff member plans again.

- **Clerk** (counter): stands 15 units behind the counter for an hour at a time, so it is staffed.
- **Tub scrubber** (any tub): scrubs the building's first dirty tub nobody soaks in, 0.5 h (the blood bath 1.5 h). A tub gets dirty when a soak
  in it ends (`TubCare`, saved).
- **Laundry runner** (washing machine): delivers what it carries; takes up to 10 clean towels from the washer to the first towel rack, else the
  counter, with room; fetches up to 10 Dirty Towels from a hamper; keeps 3 Soap in the washer from a store in the building.
- **Tea server** (tea table): brings a Tea Set from a store to a table without one; takes a table's Used Tea Sets to a store, where each is rinsed into Teaware.

## Research

All in the Crafting list, no cost: Bathhouse Basics (level 1; loom, press, washer, boiler, water tank, small tub, counter, lockers, hamper,
rack, robe), then Hot Baths (medium and large tubs, review board, decorations), Massage, Tea Service (dryer, kiln, bench, tea table, display)
and Oil Baths (level 2), and Blood Baths after Hot Baths (level 3).

## Settings

`BathhouseSettings`, on the Settings screen's Mods page: seats per boiler (4), Water per boiler a day (2), soak length by tub (small 2 h,
medium 3, large 4, oil and blood 3), Soothed buff (3 days), soaks clear debuffs (on), towels kept (80%), hours between customers (6),
relation lost per complaint (1), most relation lost a day (5), fight chance (5%). Prices and the open switch are on the counter, saved with the
game (sidecar version 3; version 2 reads with tea and massage at 100%).

## Tests

`Bathhouse.Tests/RecordTests` applies the records to the installed game (`KENSHI_PATH`; skipped without one) and checks links, machine data,
research, the goals' lists and the customer kinds' squads and factions.
`BathingTests` checks the heat rule, seats, the towel roll's split, which effects a soak clears, soak lengths and the sidecar round trip.
`CustomerTests` checks price acceptance, the price steps, quality and pay, the complaints' cap, demand and the barks.
`StaffTests` checks dirty tubs, the massage skill, the dirty tub's quality part, tea and massage prices and the staff roles. `BathingTests`
also reads a version 2 sidecar.
