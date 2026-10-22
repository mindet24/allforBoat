---
name: probuilder-subdivide-edges
description: Insert new vertices on selected edges of a `ProBuilderMesh`, splitting each edge into smaller segments. Supply either `edges` (explicit list) or `faceDirection` (subdivides all edges of faces facing that direction); exactly one is required.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Subdivide edges in a ProBuilder mesh

Insert new vertices on selected edges of a `ProBuilderMesh`, subdividing each into smaller segments. Useful for adding detail to specific edges for further manipulation (extrude, bevel, etc.).

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `edges` — explicit list of edges to subdivide, each as `[vertexA, vertexB]`. Use 'probuilder-get-mesh-info' to discover valid edges.
- `faceDirection` — semantic alternative: subdivides all edges of faces pointing this direction (`Up`, `Down`, `Left`, `Right`, `Forward`, `Back`). Exactly one of `edges` / `faceDirection` is required.
- `subdivisions` — number of subdivisions per edge. `1` splits each edge in half, `2` into thirds, and so on. Default `1`.

## Examples

- Subdivide all edges of the top face: `faceDirection="up"`, `subdivisions=2`.
- Subdivide specific edges: `edges=[[0,1], [2,3]]`, `subdivisions=1`.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-subdivide-edges --input '{
  "gameObjectRef": "string_value",
  "edges": [],
  "faceDirection": "string_value",
  "subdivisions": 0
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-subdivide-edges --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-subdivide-edges --input-file - <<'EOF'
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
| `faceDirection` | `string` | No | Semantic face selection - subdivide all edges of faces facing this direction. |
| `subdivisions` | `integer` | No | Number of subdivisions per edge. 1 = splits edge in half, 2 = splits into thirds, etc. Default is 1. |

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
    },
    "subdivisions": {
      "type": "integer"
    }
  },
  "required": [
    "gameObjectRef"
  ]
}
```

## Output

This tool does not return structured output.

