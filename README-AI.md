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
Any mesh hit fades to `occluderAlpha` (0.25) by swapping in transparent copies of its URP Lit materials. Meshes
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
- Read the file before editing. Another linked Claude session (`error-fork-2`) has also edited this tree, for
  example the camera orbit.

## State of the repo

- Branch `main`. Last commit: `73ed14c feat(view): tilted perspective camera and room lights tied to power`.
- **Large uncommitted working tree (90+ files).** It holds the day-mode rework, the jump and jetpack, the fader,
  smoothing, family/rig work, the camera orbit and more. Nothing has been committed since. Commit only when the user
  asks, using Conventional Commits (`feat:`, `fix:`...) with the Claude co-author line.
- Evidence screenshots for this work are in `production/qa/evidence/`: `jump-*.png`, `jetpack-*.png`,
  `see-through.png`, `walk-smooth.png`.

## Open items / ideas

- Playtest and tune the day: `walkSpeed`, `depthWalkSpeed`, `jumpTicks`, request timing in `level_demo.json`.
- Verify the see-through fade in a built player (see above).
- The jetpack flame is a placeholder orange rounded rect (`TrippedViewConfig.jetpackFlame*`).
- `RigView` / `RigAnimator` (cut-out rig) exist and are tested, but the character still draws as sprite frames.
- **Workshop** (floor 0, x 0..4, door to the laundry) is `Art/Models/Room_Workshop.fbx`, built in
  `Source~/TRIPPED_Rooms.blend` (collection `TRIPPED_Workshop`). Any room-model part named `*_Random*` is there on a
  given day with `TrippedViewConfig.randomPropChance`, rolled from the day seed (`RoomModelRules.ShowsRandomPart`).
- The menu "How to Play" key line (`g6_keys`) always shows the jump keys, even when the jetpack is on.

## Working with this user

- Follows the studio protocol in `CLAUDE.md`. Show what you will change for big multi-file work, and don't commit
  unasked.
- Short requests in Thai. When a request has a real design choice (for example "where can you jump"), ask with
  2–3 concrete options. Otherwise pick a sensible default, do it, and say what you chose.
- They value seeing it work: run the tests, play it, keep a screenshot.
