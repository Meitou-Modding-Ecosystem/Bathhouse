# Bathhouse

A [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) code mod: build and run a bathhouse with towels, soap, tea,
heated tubs and paying bathers. GPL-3.0-or-later. What it does: [docs/bathhouse.md](docs/bathhouse.md); tracking issue #1.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Agent rules for the engine pin, versions, CI and releases: [CLAUDE.md](CLAUDE.md) (same as `AGENTS.md`); step-by-step procedures:
  `.claude/skills/meitou-engine-release`.
- Checking a built mod against the newest Meitou release (CI does it on every PR): [docs/refcheck.md](docs/refcheck.md).
- Publishing the mod to the Steam Workshop for the first time, then on every release: [docs/steam-workshop.md](docs/steam-workshop.md).
- Turning `.glb` models (Meshy, Hi3D, Blender) into buildable structures and items, no FCS needed: [docs/structures.md](docs/structures.md);
  getting a generated model's polycount, size and textures right first: [docs/model-prep.md](docs/model-prep.md).

## Customers can be attacked on the road

Customer groups travel to your bathhouse as ordinary squads. While they are away from your squads, Meitou's **Fights far away** setting lets
them meet hostile squads and fight it out off screen, and a group that loses never arrives (the bathhouse posts that it never made it, and
the next group may set out at once). A bathhouse in dangerous country gets fewer customers. To turn it off: **Settings > Advanced > Fights
far away** (it affects every squad in the world, not only customers).

## Numbers

Task types and dialogue codes are shared by every loaded mod; Bathhouse claims only these, inside its block
([mod number blocks](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/numbers.md)). `NumbersTests` checks the list.

Block 4400-4499

| Number | Pool | Use |
| --- | --- | --- |
| 4401 | Task type | Goal: taking a bath |
| 4402 | Task type | Goal: visiting the bathhouse |
| 4403 | Task type | Job: Bathhouse clerk (offered on the Check-in Counter) |
| 4404 | Task type | Job: Tub scrubber (offered on the tubs) |
| 4405 | Task type | Job: Laundry runner (offered on the Washing Machine) |
| 4406 | Task type | Job: Tea server (offered on the Tea Table) |
| 4407 | Task type | Job: Massage attendant (offered on the Massage Station) |
| 4408 | Task type | Job: Water carrier (offered on the Water Cistern) |

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. `git pull` does not move it: after
  every pull run `powershell -ExecutionPolicy Bypass -File tools\Update-Local.ps1 -Clean -Build`
  ([keep clones current](docs/local-development.md#keep-clones-current)).
- `<Mod>/`: the mod's project and `meitou.json`. `<Mod>.Tests/`: its tests (synthetic worlds, no game install needed).
  `<Mod>.Data/` (optional): a generator that writes `<Mod>.mod` from the installed game. `<Mod>/Structures/` (optional): `.glb` models
  imported as buildable structures on deploy ([docs/structures.md](docs/structures.md)).
- `meitou.lock.json`: the framework releases the mods build against
  ([framework lock](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-lock.md)); the Framework update workflow moves it
  to new releases and merges when CI passes ([framework update](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-update.md)).
- `Play/`: one-click build, deploy and run.
- `tools/`: `Update-Local.ps1`, the local pull and engine-pin check ([keep clones current](docs/local-development.md#keep-clones-current));
  `Switch-Engine.ps1` and `Use-ForkEngine.ps1`, local engine switching ([play every mod on the fork](docs/local-development.md#play-every-mod-on-the-fork));
  `refcheck.sh` and `RefCheck/`, the engine-reference check ([docs/refcheck.md](docs/refcheck.md)); `StructureImport/`, the model import ([docs/structures.md](docs/structures.md)).
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`. Each merge into `release` tags it `<slug>-v<version>` and publishes the mod's zip (`.github/workflows/release.yml`) unless that
tag exists; `tools\Tag-Release.ps1` is the manual fallback.

- New-repository branch protections and permanent beta/main/release reviews: [docs/branch-policy.md](docs/branch-policy.md). Templates copy source; GutenStructure applies settings through reconciliation.
