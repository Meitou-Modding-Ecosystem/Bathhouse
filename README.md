# Bathhouse

A [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) code mod: build and run a bathhouse with towels, soap, tea,
heated tubs and paying bathers. GPL-3.0-or-later. What it does: [docs/bathhouse.md](docs/bathhouse.md); tracking issue #1.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Checking a built mod against the newest Meitou release (CI does it on every PR): [docs/refcheck.md](docs/refcheck.md).
- Publishing the mod to the Steam Workshop for the first time, then on every release: [docs/steam-workshop.md](docs/steam-workshop.md).
- Turning `.glb` models (Meshy, Hi3D, Blender) into buildable structures, no FCS needed: [docs/structures.md](docs/structures.md).

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. After a pull that moves it:
  `git submodule update --init`.
- `<Mod>/`: the mod's project and `meitou.json`. `<Mod>.Tests/`: its tests (synthetic worlds, no game install needed).
  `<Mod>.Data/` (optional): a generator that writes `<Mod>.mod` from the installed game. `<Mod>/Structures/` (optional): `.glb` models
  imported as buildable structures on deploy ([docs/structures.md](docs/structures.md)).
- `meitou.lock.json`: the framework releases the mods build against
  ([framework lock](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-lock.md)); the Framework update workflow moves it
  to new releases and merges when CI passes ([framework update](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/framework-update.md)).
- `Play/`: one-click build, deploy and run.
- `tools/`: `refcheck.sh` and `RefCheck/`, the engine-reference check ([docs/refcheck.md](docs/refcheck.md)); `StructureImport/`, the
  model import ([docs/structures.md](docs/structures.md)).
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`. A tag `<slug>-v<version>` on `release` publishes the mod's zip (`.github/workflows/release.yml`).

- New-repository branch protections and permanent beta/main/release reviews: [docs/branch-policy.md](docs/branch-policy.md). Templates copy source; GutenStructure applies settings through reconciliation.
