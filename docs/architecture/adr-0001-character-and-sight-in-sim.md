---
status: accepted
date: 2026-10-23
---

# Character position and Sight live in the sim, not the view

The player character's room, walking path, action queue and arrival times, and the set of rooms the player can see, are state inside `Trouble.Sim`. They are counted in integer ticks and have no `UnityEngine` reference. The view only animates a sprite along the path the sim reports. We did this because Sight decides when a socket is revealed, and walking time decides whether a request beats its deadline. Both have to be deterministic so that the reference solutions, scenarios and the Level Editor's Run Sim give the same result every run, at any frame rate.

## Considered Options

- **Character as a normal Unity object** (Transform, NavMesh or tweening, Sight from colliders or raycasts). Rejected: walk time would depend on frame rate and physics, Run Sim could not run in the Editor without a scene, and replay tests could not cover walking or reveals.

## Consequences

- Room geometry (`floor`, `x`, `width`) and doors are level data that the sim reads. The 3D house the view builds has to match them; it must not invent its own distances.
- Changing walk speed or Sight rules means editing `rules.json` and running Run Sim. It never means editing view code.
