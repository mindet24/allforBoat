---
name: probuilder-flip-normals
description: Reverse the normal direction of selected faces in a `ProBuilderMesh`, flipping them inside-out. Useful for creating interior spaces (a room from the inside of a cube) or fixing inverted faces. Defaults to all faces when no selection is supplied.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Flip face normals in a ProBuilder mesh

Reverse the normal direction of selected faces in a `ProBuilderMesh`, flipping them inside-out. Useful for creating interior spaces (a room from the inside of a cube) or fixing inverted faces produced by other operations.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `faceIndices` — optional explicit list of face indices to flip.
- `faceDirection` — optional semantic alternative (`Up`, `Down`, `Left`, `Right`, `Forward`, `Back`).

When both are omitted, **every face** is flipped.

## Examples

- Flip all faces: leave both `faceIndices` and `faceDirection` empty.
- Flip top face only: `faceDirection=Up`.
- Flip specific faces: `faceIndices=[0, 2, 4]`.

## Behavior

The mesh is rebuilt (`ToMesh` → `Refresh`), dirty-flagged, and the Editor repaints. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-flip-normals --input '{
  "gameObjectRef": "string_value",
  "faceIndices": [],
  "faceDirection": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-flip-normals --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-flip-normals --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `string` | Yes | Reference to the GameObject with a ProBuilderMesh component. RQL: GameObjectRef |
| `faceIndices` | `array` | No | Array of face indices to flip. If empty and faceDirection is empty, flips all faces. |
| `faceDirection` | `string` | No | Semantic face selection by direction. If empty and faceIndices is empty, flips all faces. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "type": "string"
    },
    "faceIndices": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "faceDirection": {
      "type": "string",
      "enum": [
        "Up",
        "Down",
        "Left",
        "Right",
        "Forward",
        "Back"
      ]
    }
  },
  "required": [
    "gameObjectRef"
  ]
}
```

## Output

This tool does not return structured output.

