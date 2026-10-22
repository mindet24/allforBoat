---
name: probuilder-set-face-material
description: Assign a material to specific faces of a `ProBuilderMesh`, enabling multi-material meshes (e.g., grass on top, dirt on sides). Supply either `faceIndices` (explicit) or `faceDirection` (semantic); exactly one is required.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Set material on ProBuilder faces

Assign a material to specific faces of a `ProBuilderMesh`, enabling multi-material meshes where different faces have different materials (e.g., grass on top, dirt on the sides).

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `materialPath` — project path to the material asset (e.g., `Assets/Materials/Grass.mat`) or a bare material name. Required.
- `faceIndices` — explicit array of face indices.
- `faceDirection` — semantic alternative (`Up`, `Down`, `Left`, `Right`, `Forward`, `Back`). Exactly one of `faceIndices` / `faceDirection` is required.

## Examples

- Set material on the top face: `faceDirection="up"`.
- Set material on specific faces: `faceIndices=[0, 2, 4]`.

## Behavior

The mesh is rebuilt (`ToMesh` → `Refresh`), dirty-flagged, and the Editor repaints. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-set-face-material --input '{
  "gameObjectRef": "string_value",
  "materialPath": "string_value",
  "faceIndices": [],
  "faceDirection": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-set-face-material --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-set-face-material --input-file - <<'EOF'
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
| `materialPath` | `string` | Yes | Path to the material asset (e.g., 'Assets/Materials/MyMaterial.mat') or material name. |
| `faceIndices` | `array` | No | Array of face indices to apply the material to. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices. |
| `faceDirection` | `string` | No | Semantic face selection by direction. Use this OR faceIndices, not both. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "type": "string"
    },
    "materialPath": {
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
    "gameObjectRef",
    "materialPath"
  ]
}
```

## Output

This tool does not return structured output.

