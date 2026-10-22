---
name: probuilder-bevel
description: Bevel (chamfer) selected edges of a `ProBuilderMesh`, replacing each sharp edge with an angled face. Identify edges by their `[vertexA, vertexB]` index pairs — use 'probuilder-get-mesh-info' to discover them. `amount` is clamped to (0, 1).
metadata:
  generated-by: mcp-plugin-dotnet
---

# Bevel ProBuilder edges

Bevel (chamfer) selected edges of a `ProBuilderMesh`, replacing each sharp edge with an angled face for a smoother appearance. Use 'probuilder-get-mesh-info' first to discover edges by their vertex pairs.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `edges` — array of edge definitions; each edge is `[vertexA, vertexB]`. Example: `[[0,1], [2,3]]` bevels two edges.
- `amount` — bevel strength in the range (0, 1). `0.001` is barely visible, `0.999` reaches face center. Recommended `0.05`–`0.2`. Internally clamped.

## Behavior

Vertex indices are validated against `vertexCount`. After `BevelEdges` runs, the mesh is rebuilt (`ToMesh` → `Refresh`), the `ProBuilderMesh` and GameObject are marked dirty, and Editor windows repaint. Returns the number of edges processed, the clamped amount, and post-op face/vertex/edge counts. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-bevel --input '{
  "gameObjectRef": "string_value",
  "edges": [],
  "amount": 0
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-bevel --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-bevel --input-file - <<'EOF'
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
| `edges` | `array` | Yes | Array of edge definitions. Each edge is defined by two vertex indices [vertexA, vertexB]. Example: [[0,1], [2,3]] bevels edges from vertex 0 to 1 and from vertex 2 to 3. |
| `amount` | `number` | No | Bevel amount from 0 (no bevel) to 1 (maximum bevel reaching face center). Recommended values: 0.05 to 0.2. |

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
    "amount": {
      "type": "number"
    }
  },
  "required": [
    "gameObjectRef",
    "edges"
  ]
}
```

## Output

This tool does not return structured output.

