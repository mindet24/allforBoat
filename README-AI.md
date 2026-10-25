# TRIPPED! — handoff for the next AI

Read this first, then `CLAUDE.md` (studio rules) and `GLOSSARY.md` (the project's words). Design lives in
`Gameprompt/TRIPPED_GDD.md` (v1.0, Thai) and `Gameprompt/TRIPPED_GDD_v1.1_map_art_tools.md` (v1.1, Thai).
The user writes in Thai; code, identifiers, in-game text and docs are English.

## What the game is

Game-jam game, theme *"We R in Trouble"*. A family house where everyone wants power at once.

- **Day mode — the main game** (`Levels/level_demo.json`, "One Busy Day", level number 100). Real-time and played
  with the keyboard. Request cards pop up ("Mom: Kettle in the Kitchen"). You carry appliances to the room on the
  card and plug them in. If a room's load stays over its capacity for 3 s, the **whole house** blacks out, and you
  run to the breaker panel in the basement and press E. You have 3 hearts, and a missed urgent card costs one.
  The day lasts about 3 minutes.
- **Classic levels 1–4** (`level1..4.json`): the older click-to-plan puzzle mode, with circuits, stairs, MoveTo and
  cords. It is still in the code and tests and reachable from the menu, but it is not where current work happens.

Family: Mom, Dad, Daughter, Grandma. Unity **6000.3.8f1**, URP, Input System, uGUI + TextMeshPro. There is one
scene (`Assets/TRIPPED/Scenes/Main.unity`), and nearly all UI and world objects are built from code at runtime
(`GameRoot` → `LevelController` → `HouseView` / HUD).

## Layout

```
Assets/TRIPPED/
  Scripts/Sim/        Trouble.Sim   — pure C# simulation. NO UnityEngine reference (asmdef has no refs).
  Scripts/Game/       Trouble.Game  — everything Unity: views, HUD, camera, input, juice.
    Core/             GameRoot (boot), LevelController (input → Commands, tick loop), LevelLoader
    Logic/            pure, unit-tested helpers for the view (HouseLayout, DayInteraction, RigAnimator, ...)
    World/            HouseView (builds the house), HouseKitView (3D kit + room models), CharacterView,
                      ApplianceView, FamilyView, DayPromptView (E/F bubbles), OccluderFader (see-through)
    UI/ Screens/      HUD (DayHud, DayStatus, TrayView, PowerPanel...), menus; texts in UI/HudText.cs
    Juice/            sounds (SynthBank = procedural audio), shake, squash, puffs
    Camera/           HouseCamera (follow, overview on Tab, right-drag orbit, middle-click reset)
    Config/           TrippedViewConfig (view tunables), RigConfig
  Editor/             Tools/TRIPPED/Level Editor, Run Sim, Studio (Ctrl+Shift+T)
  Resources/Config/   rules.json, catalog.json, strings.json, TrippedViewConfig.asset, JuiceConfig.asset
  Resources/Levels/   level JSON
  Tests/EditMode/     NUnit EditMode tests (Trouble.Tests.EditMode)
production/qa/evidence/   retained screenshots (required evidence for visual/UI work, see CLAUDE.md)
docs/architecture/        ADRs
```

## Rules of the architecture (do not break)

1. **The sim is the truth.** `HouseSim` owns all game state. The view reads it every frame and never changes it.
   Every player action is a `Command` sent through `LevelController.Submit` → `HouseSim.Submit`.
2. **Deterministic integers.** Time is whole ticks (`rules.ticksPerSecond` = 10). Day-mode positions are whole
   **centimetres** (`CharacterState.XCm`, `ZCm`, `AirCm`). Convert with `Cm(metres)`. This keeps replays exact.
3. **Gameplay numbers are data**, not hardcoded: `rules.json` → `RulesConfig`, `catalog.json` → `ApplianceDef`,
   view numbers → `TrippedViewConfig`. A key missing from the JSON falls back to the C# default.
4. **Pure logic goes in `Game/Logic` or `Sim`** so it can be unit tested. MonoBehaviours stay thin.
5. Doc comments on public APIs. Match the surrounding comment style, which is plain, explanatory and explains why.

## Day-mode movement (current state)

Default keys below; every one can be rebound in `TrippedViewConfig` (see "Keys and the Studio tool").

| Key | Action |
|---|---|
| A/D | walk along the room (`walkSpeed` 3 m/s). At the front of the room (`stairsDepthMetres` 1 m) walking across the bottom step of a flight goes up it, across the top step goes down (`stairsSpeed` 2.2 m/s) |
| W/S | walk in / out, between the open front (z 0) and `walkDepthMetres` 2 m (`depthWalkSpeed` 3 m/s) |
| Space / Shift+Space | only with `"verticalMove": "jump"` / `"jetpack"`; with stairs (the default) Space pauses |
| E | pick up / put down (hold E while carrying to throw); at the panel during a blackout, reset it |
| F | plug in / unplug |
| P / Esc / Space | pause · Tab overview · R restart |

- **Stairs are walked** (`"verticalMove": "stairs"`, the default). Each stairs door between two floors gets a flight
  (`HouseSim.PlaceFlight`): `stairsRunMetres` long inside the x range both rooms share, climbing toward +x, unless the
  level's door gives `stairsBottomX` / `stairsRun`. On a flight `CharacterState.StairsDoorId` is set, the room switches
  halfway, and `AirCm` is the height over that room's floor; hands are busy (`CanUseHands`). Behind the flight (deeper
  than `stairsDepthMetres`) the character walks past. `HouseView.BuildDayStairs` draws one block per step under the kit,
  so `OccluderFader` fades them when the character is behind. Tests: `StairsTests`.
- **Jump** (`"verticalMove": "jump"`) is kept as an option.
- **Jetpack is kept but switched off too.** Set `"verticalMove": "jetpack"` in `rules.json` to get it back:
  hold Space to fly up through ceilings, release to fall and land on the floor below, S/Shift+Space to drop through
  one floor. Heavy appliances (catalog `weight`) reduce thrust. Both modes are tested. Each mode rejects the other
  mode's commands (`HouseSim.UsesJetpack`).
- **Reach is generous in depth.** `reachDepthWeight` 0.5 means depth counts half, so sockets at the back
  (z = 2 m) can be used from the front. Set it to 1.0 for the old behaviour.
- **Doors** only pass at the open front (`doorDepthMetres` 1 m). Walking into a door's wall deeper in slides the
  character toward the doorway automatically (`HouseSim.KeyboardTick`).
- **Smooth motion.** The sim moves every tick (0.1 s). `CharacterState.PrevXCm` / `PrevZCm` store the position at
  the start of the last tick. `HouseLayout.DayX` / `DayZ` / `DayCharacterPoint` interpolate with
  `LevelController.TickFraction`, which freezes while paused. `CharacterView` shows that point exactly. Don't put a
  follow-lag back on top of it.

## See-through house (`World/OccluderFader.cs`)

Every mesh under the house kit gets a trigger `BoxCollider` on the Ignore Raycast layer, so click picking never
sees it. Each frame, rays go from the camera to every visible sprite under the character, appliances and family.
Any mesh hit fades to `occluderAlpha` (0.5) by swapping in transparent copies of its URP Lit materials. Meshes
whose name contains `neverFadeParts` ("Floor", "Foundation") never fade, because a faded floor slab makes the
house look broken.
**Not checked yet:** a built player. Shader-variant stripping could remove URP Lit's transparent variant in a build.

## How to verify your work

The user expects real proof (CLAUDE.md: "a parse check is not a run"). The Unity Editor is usually open, so drive it
through the Unity MCP tools:

1. **Compile.** Call `Unity_RunCommand` with `AssetDatabase.Refresh(...)` and
   `CompilationPipeline.RequestScriptCompilation()`. Wait until `Library/ScriptAssemblies/Trouble.Tests.EditMode.dll`
   is newer than your edited files, then read `Unity_GetConsoleLogs` (errors). The Sentis shader warnings are noise.
2. **Tests.** Use `Unity_RunCommand` with a `TestRunnerApi` + `ICallbacks` script that writes pass/fail counts and
   failure messages to a file in your scratchpad, then poll for that file. **554 EditMode tests** pass right now.
3. **Play check.** `Unity_ManageEditor Play`, then `Unity_RunCommand`: find `LevelController`,
   `LoadLevel(LevelLoader.DemoLevel)`, `StartOrTogglePause()`, `Submit(Command...)`. Drive it from an
   `EditorApplication.update` callback and capture with `ScreenCapture.CaptureScreenshot` into
   `production/qa/evidence/`. Stop play afterwards.

Gotchas:
- **The user may be playing in the Game view at the same time.** A trace that shows walking you didn't command is
  the user. Don't call `LoadLevel` over their run without saying so.
- `EditorApplication.update` callbacks outlive your command. Always remove them (on a timeout too), or they keep
  pressing keys in later runs.
- `EditorApplication.update` runs more often than game frames. Sample once per `Time.frameCount`.
- Unity sometimes locks a file for a moment and a write fails with "Invalid argument". Check the file is intact and
  retry.
- Test helpers in `DayModeTests` / `DayInteractionTests` work like this: `WalkTo(x)` goes front → along → back
  (doors need the front, sockets the back), `WalkAlong(x)` keeps the current depth, and `WalkDepthTo(zCm)` walks in
  or out.
- `ScreenCapture` only writes when a frame renders, and the editor barely renders while it is unfocused. Pause, then
  `EditorApplication.Step()` in separate commands, then capture and `Step()` once more.
- Run tests with `runSynchronously = true` in `ExecutionSettings`; the async run sometimes never finishes.
- **One session at a time.** Earlier, several forks edited the same files at once and overwrote each other. Before
  editing, check `list_sessions` for other running sessions in this folder.

## Characters and the rig

- Art: `Resources/Art/Characters/player_idle.png` (the kid, the player), `family_{mom,dad,daughter,grandma}.png`,
  `Faces/face_<owner>.png` (request cards). All cut from the user's sheet; same pixel scale, so heights carry over.
- **Player = cut-out rig** (`World/RigView.cs`): `Art/Characters/Rig/player_{head,body,armL,armR,legL,legR}.png`
  on one 512 canvas plus `player_rig.json` (joints). Poses are pure maths in `Logic/RigAnimator.cs`: idle, walk,
  stairs run, jetpack flying, carrying overhead, and one-off Lift / PutDown / Throw / Plug. Every number is in
  `Resources/Config/RigConfig.asset`. `CharacterView` drives it, and `HouseView` forwards
  `LevelController.CommandAccepted` to `CharacterView.OnCommandAccepted` for the one-off moves. If the rig art is
  missing, it falls back to the sprite strips. The juice layer still leans and squashes the whole `Character` object.
- Family (`World/FamilyView.cs`, `Logic/FamilyRules.cs`): each person waits in the room of their earliest active
  request, otherwise in their first room. They fade out and back in when changing rooms, hop while waiting, and step
  aside from appliances. They are not rigged.

## Keys and the Studio tool

- **Every gameplay key is a field in `TrippedViewConfig`**: walk left/right/in/out (main + alt), start, pause,
  restart, down modifier, interact (E), plug (F), overview, follow, home. `LevelController` and `HouseCamera` read
  them through `KeyHeld` / `KeyDown`. Escape (menus/back) and digits 1–9 (debug level jump) are still fixed.
  The on-screen hint texts (`UI/HudText.cs`: `day_hint`, `g6_keys*`) are fixed strings and do not follow key changes.
- **Tools > TRIPPED > Studio (Ctrl+Shift+T)**, `Editor/StudioWindow.cs`: tabs Rules (`rules.json`), Level (any
  `Levels/*.json`, including the day randomisation: seed, room `capRandom`/`capMin`/`capMax`, request timing,
  urgencies, people), Catalog, Strings, View / UI / Juice / Rig (the config assets, live in Play mode), and Keys
  (every `InputSystem.Key` field of every config). There is a search box. JSON changes apply on the next Play or Restart
  level. It uses reflection, so new fields show up without editing the window. The older per-level tool is
  Tools > TRIPPED > Level Editor (Verify / Run Sim).

## Art pipeline (Blender)

- Room models: `Art/Models/Room_<Name>.fbx`. Each is an empty `Room_<Name>` with meshes as direct children, x 0..width,
  depth y 0..4.2 (back wall at y 4–4.2), z 0..3, floor slab −0.3..0. Source: `Art/Models/Source~/TRIPPED_Rooms.blend`,
  one collection per room. Export: selected root + children, FBX, Forward −Z, Up Y, no leaf bones. Copy an existing
  `Room_*.fbx.meta` for the import settings (no rig, materials via description) with a new GUID. Then register the room
  in `TrippedViewConfig.roomModels` (asset) and add the room to the level JSON.
- Parts named `*_Random*` appear on a given day with `randomPropChance` (`RoomModelRules.ShowsRandomPart`).
- Blender MCP works (addon protocol 13, slightly old). The `.blend` was left with unsaved changes; the Workshop
  collection was **not saved** into it. Save it from Blender if it matters.

## State of the repo

- Branch `main`, last commit `e6fd27b feat(day): stairs, workshop room, family, rig animation, E/F carry and
  Studio tool`. **554 EditMode tests pass.** Not pushed.
- Untracked on purpose: `Tests/EditMode/Fixtures/TmpArtCheck*` and `Source~/TRIPPED_HouseKit.blend.meta.unused`.
  Never commit them.
- Commit only when the user asks. Use Conventional Commits with the Claude co-author line.
- Evidence screenshots: `production/qa/evidence/` (`stairs-*`, `workshop-room`, `rig-carry`, `family-*`,
  `plug-*`, `see-through`, `walk-*`, `look-*`).

## Open items / ideas

- **No Windows build has been made or tested yet.** The jam deadline was 25 Oct 10:00. Also check that the
  see-through shader variant survives a build.
- Where stairs pass through the upper floor slab the slab is solid, so the head can dip into it near the top. The
  fader never fades "Floor" parts.
- Key hint texts do not follow rebinding (see Keys).
- The jetpack flame is a placeholder rounded rect.
- Family members are static sprites (no rig yet). Only the kid has rig parts.
- Playtest and tune: `walkSpeed`, `stairsSpeed`, `stairsDepthMetres`, request timing and capacities in
  `level_demo.json`.

## Working with this user

- Thai, short requests. **Keep replies and progress updates short.** Long sessions filled the context window. Avoid
  tool calls that dump big output.
- Follows the studio protocol in `CLAUDE.md`. For big multi-file work, say what you will change. Don't commit unasked.
- When a request has a real design choice, ask with 2–3 concrete options. Otherwise pick a sensible default and say
  what you chose.
- They value seeing it work: run the tests, play it, keep a screenshot.
