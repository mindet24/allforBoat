---
name: probuilder-merge-objects
description: Combine multiple `ProBuilderMesh` GameObjects into one merged mesh. The first GameObject in the list becomes the merge target. Source GameObjects are deleted by default. Useful for optimizing draw calls and unifying composite props.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Merge multiple ProBuilder meshes into one

Combine multiple `ProBuilderMesh` GameObjects into a single merged mesh. The first GameObject in `gameObjectRefs` becomes the merge target — subsequent meshes are absorbed into it. Useful for optimizing draw calls or creating a unified object from parts.

## Inputs

- `gameObjectRefs` — array of GameObject references (≥2). Each must carry a `ProBuilderMesh` component. The first reference is the merge target.
- `deleteSourceObjects` — when `true` (default), delete the non-target source GameObjects after merging. Set `false` to keep them in the scene.

## Example

Merge a table assembled from four leg meshes and a top into a single GameObject for shipping.

## Behavior

All meshes are rebuilt (`ToMesh` → `Refresh`), the resulting GameObject is marked dirty, and the Editor repaints. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-merge-objects --input '{
  "gameObjectRefs": "string_value",
  "deleteSourceObjects": false
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-merge-objects --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-merge-objects --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRefs` | `string` | Yes | Array of GameObject references with ProBuilderMesh components to merge. First object becomes the merge target. RQL: [GameObjectRef] |
| `deleteSourceObjects` | `boolean` | No | If true, delete the source GameObjects after merging (except the target). Default is true. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRefs": {
      "type": "string"
    },
    "deleteSourceObjects": {
      "type": "boolean"
    }
  },
  "required": [
    "gameObjectRefs"
  ]
}
```

## Output

This tool does not return structured output.

