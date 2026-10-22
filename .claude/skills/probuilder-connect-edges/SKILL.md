---
name: probuilder-connect-edges
description: Insert new edges connecting the midpoints of selected edges within faces of a `ProBuilderMesh` — adds edge loops and extra geometry detail. Supply either `edges` (explicit list) or `faceDirection` (semantic selection); exactly one is required.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Connect edges in a ProBuilder mesh

Insert new edges connecting the midpoints of selected edges within faces of a `ProBuilderMesh`. When a face has more than two edges to connect, a center vertex is added. Useful for creating new edge loops and adding geometry detail.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `edges` — explicit list of edges to connect, each as `[vertexA, vertexB]`. Use 'probuilder-get-mesh-info' to discover valid indices.
- `faceDirection` — semantic alternative: connects all edges of faces pointing this direction (`Up`, `Down`, `Left`, `Right`, `Forward`, `Back`). Exactly one of `edges` / `faceDirection` is required.

## Examples

- Connect opposite edges of the top face: `faceDirection="up"`.
- Connect specific edges: `edges=[[0,1], [2,3]]`.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-connect-edges --input '{
  "gameObjectRef": "string_value",
  "edges": [],
  "faceDirection": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-connect-edges --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-connect-edges --input-file - <<'EOF'
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
| `edges` | `array` | No | Array of edge definitions. Each edge is [vertexA, vertexB]. Use ProBuilder_GetMeshInfo to get vertex indices. |
| `faceDirection` | `string` | No | Semantic face selection - connect edges of faces facing this direction. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "type": "string"
    },
    "edges": {
      "type": "array",
      "items": {
        "type": "array",
        "items": {
          "type": "integer"
        }
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

