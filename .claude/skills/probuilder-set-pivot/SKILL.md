---
name: probuilder-set-pivot
description: Move the pivot (origin) of a `ProBuilderMesh` without shifting the visible geometry. Choose `Center` (mesh bounds), `FirstVertex`, or `Custom` (world-space position). The mesh data is rebaked so the visual position stays fixed.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Set the pivot point of a ProBuilder mesh

Move the pivot (origin) of a `ProBuilderMesh` without shifting the visible geometry. The mesh data is rebaked so the visual position stays fixed while the GameObject's transform origin moves to the new pivot.

## Inputs

- `gameObjectRef` — the GameObject hosting the `ProBuilderMesh` component.
- `pivotLocation` — `MeshPivotLocation` enum: `Center` (mesh bounds center), `FirstVertex`, or `Custom`.
- `customPosition` — required when `pivotLocation = Custom`. World-space pivot position.

## Examples

- Center the pivot: `pivotLocation=Center`.
- Set pivot to first vertex: `pivotLocation=FirstVertex`.
- Set custom pivot: `pivotLocation=Custom`, `customPosition=(0, 0, 0)`.

## Behavior

The mesh is rebuilt (`ToMesh` → `Refresh`), dirty-flagged, and the Editor repaints. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-set-pivot --input '{
  "gameObjectRef": "string_value",
  "pivotLocation": "string_value",
  "customPosition": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-set-pivot --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-set-pivot --input-file - <<'EOF'
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
| `pivotLocation` | `string` | No | Where to place the pivot. |
| `customPosition` | `string` | No | Custom world position for pivot (only used when pivotLocation=Custom). RQL: Vector3 |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "type": "string"
    },
    "pivotLocation": {
      "type": "string",
      "enum": [
        "Center",
        "FirstVertex",
        "Custom"
      ]
    },
    "customPosition": {
      "type": "string"
    }
  },
  "required": [
    "gameObjectRef"
  ]
}
```

## Output

This tool does not return structured output.

