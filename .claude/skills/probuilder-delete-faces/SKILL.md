---
name: probuilder-delete-faces
description: Delete selected faces from a `ProBuilderMesh`, creating holes or removing geometry. Supply either `faceIndices` (explicit list) or `faceDirection` (semantic selection); exactly one is required.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Delete ProBuilder faces

Delete selected faces from a `ProBuilderMesh`, creating holes or removing geometry entirely. Faces can be selected explicitly by index or semantically by direction.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `faceIndices` — explicit array of face indices to delete. Use 'probuilder-get-mesh-info' to discover valid indices.
- `faceDirection` — semantic alternative (`Up`, `Down`, `Left`, `Right`, `Forward`, `Back`). Exactly one of `faceIndices` / `faceDirection` is required.

## Examples

- Delete the bottom face: `faceDirection="down"`.
- Delete specific faces: `faceIndices=[0, 2, 4]`.

## Behavior

The mesh is rebuilt (`ToMesh` → `Refresh`), the `ProBuilderMesh` and GameObject are marked dirty, and Editor windows repaint. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-delete-faces --input '{
  "gameObjectRef": "string_value",
  "faceIndices": [],
  "faceDirection": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-delete-faces --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-delete-faces --input-file - <<'EOF'
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
| `faceIndices` | `array` | No | Array of face indices to delete. Use this OR faceDirection, not both. Use ProBuilder_GetMeshInfo to get valid face indices. |
| `faceDirection` | `string` | No | Semantic face selection by direction. Use this OR faceIndices, not both. |

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

