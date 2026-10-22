---
name: probuilder-get-mesh-info
description: "Inspect a `ProBuilderMesh` — face/vertex/edge counts plus optional face-by-face detail. Use `detail=\"summary\"` for a token-efficient overview, `detail=\"full\"` for detailed face data. Often skippable when other tools accept `faceDirection` semantic selection."
metadata:
  generated-by: mcp-plugin-dotnet
---

# Get ProBuilder mesh information

Inspect a `ProBuilderMesh` — totals (face/vertex/edge counts) plus optional face-by-face detail (per-face vertex positions, edges, semantic direction). The other ProBuilder tools accept `faceDirection` (semantic selection like `"up"`, `"down"`) — when that suffices, you can skip this call entirely.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `detail` — `Summary` (default, cheap overview with face directions) or `Full` (per-face data).
- `includeVertexPositions` — `Full` only. Include each face's vertex positions in the response.
- `includeEdges` — `Full` only. Include each face's edges. Default `true`.
- `maxFacesToShow` — `Full` only. Cap on face-detail entries (default `20`; pass `-1` for all). Keeps the response small for high-face meshes.

## Tip

With semantic face selection (`faceDirection`) available on Extrude / DeleteFaces / SetFaceMaterial and friends, you often don't need this tool — just pass `faceDirection="up"` etc. directly.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-get-mesh-info --input '{
  "gameObjectRef": "string_value",
  "detail": "string_value",
  "includeVertexPositions": false,
  "includeEdges": false,
  "maxFacesToShow": 0
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-get-mesh-info --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-get-mesh-info --input-file - <<'EOF'
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
| `detail` | `string` | No | Detail level for output. |
| `includeVertexPositions` | `boolean` | No | If true, includes detailed vertex positions for each face (only with detail='full'). |
| `includeEdges` | `boolean` | No | If true, includes edge information for each face (only with detail='full'). |
| `maxFacesToShow` | `integer` | No | Maximum number of faces to include in detail (only with detail='full'). Use -1 for all faces. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "type": "string"
    },
    "detail": {
      "type": "string",
      "enum": [
        "Summary",
        "Full"
      ]
    },
    "includeVertexPositions": {
      "type": "boolean"
    },
    "includeEdges": {
      "type": "boolean"
    },
    "maxFacesToShow": {
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

