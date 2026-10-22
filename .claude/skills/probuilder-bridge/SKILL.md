---
name: probuilder-bridge
description: Create a single new face that bridges two existing edges of a `ProBuilderMesh`. Useful for connecting separate parts of geometry or filling gaps. Pair with 'probuilder-get-mesh-info' to discover valid edges.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Bridge two edges in a ProBuilder mesh

Create a single new face that bridges two existing edges of a `ProBuilderMesh`. Useful for connecting separate parts of geometry or filling gaps between disjoint sections. Pair with 'probuilder-get-mesh-info' to discover valid edges first.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `edgeA` — first edge as `[vertexA, vertexB]`.
- `edgeB` — second edge as `[vertexA, vertexB]`.
- `allowNonManifold` — when `true`, permits the bridge to share an edge with more than two faces. Defaults to `false`.

## Example

`edgeA=[0,1]`, `edgeB=[4,5]` creates a quad face between the two edges.

## Behavior

The mesh is rebuilt (`ToMesh` → `Refresh`), dirty-flagged, and the Editor repaints. Returns the index of the new face, before/after face counts, and post-op vertex/edge counts. Throws when the bridge cannot be formed (e.g., edges already connected or non-manifold check blocked it). The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-bridge --input '{
  "gameObjectRef": "string_value",
  "edgeA": [],
  "edgeB": [],
  "allowNonManifold": false
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-bridge --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-bridge --input-file - <<'EOF'
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
| `edgeA` | `array` | Yes | First edge as [vertexA, vertexB]. |
| `edgeB` | `array` | Yes | Second edge as [vertexA, vertexB]. |
| `allowNonManifold` | `boolean` | No | If true, allows creation of non-manifold geometry (edges shared by more than 2 faces). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "type": "string"
    },
    "edgeA": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "edgeB": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "allowNonManifold": {
      "type": "boolean"
    }
  },
  "required": [
    "gameObjectRef",
    "edgeA",
    "edgeB"
  ]
}
```

## Output

This tool does not return structured output.

