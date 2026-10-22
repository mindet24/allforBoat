---
name: probuilder-create-poly-shape
description: Create a 3D `ProBuilderMesh` from a 2D polygon outline (x,z points) extruded upward by `height`. Perfect for floor plans, room layouts, terrain patches, walls, platforms — any shape definable by a 2D outline. Requires ≥3 points.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Create a ProBuilder shape from polygon points

Create a 3D `ProBuilderMesh` from a 2D polygon outline (x,z points) extruded upward by `height`. Perfect for:

- Floor plans and room layouts.
- Custom terrain patches.
- Architectural elements (walls, platforms).
- Any shape that can be defined by a 2D outline.

## Inputs

- `points` — array of 2D points as `[x, z]`. Minimum 3 points; should be in clockwise or counter-clockwise order.
- `height` — extrusion height upward (Y axis). Default `1`.
- `name` — optional GameObject name.
- `parentGameObjectRef` — optional parent; root of the scene when omitted.
- `position`, `rotation` — optional `Vector3` transform values (rotation in euler degrees).
- `flipNormals` — when `true`, flip the resulting face normals so the shape points inward.
- `isLocalSpace` — when `true`, position/rotation are interpreted in the parent's local space.

## Examples

- Rectangle: `points=[[0,0], [4,0], [4,3], [0,3]]`, `height=2.5`.
- L-shape: `points=[[0,0], [3,0], [3,2], [1,2], [1,3], [0,3]]`, `height=3`.
- Triangle: `points=[[0,0], [2,0], [1,1.7]]`, `height=1`.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-create-poly-shape --input '{
  "points": [],
  "height": 0,
  "name": "string_value",
  "parentGameObjectRef": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "flipNormals": false,
  "isLocalSpace": false
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-create-poly-shape --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-create-poly-shape --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `points` | `array` | Yes | 2D polygon points as [x,z] coordinates. Minimum 3 points. Points should be in clockwise or counter-clockwise order. Example: [[0,0], [4,0], [4,3], [0,3]] creates a 4x3 rectangle. |
| `height` | `number` | No | Height to extrude the polygon upward. Default is 1. |
| `name` | `string` | No | Name of the new GameObject. |
| `parentGameObjectRef` | `string` | No | Parent GameObject reference. If not provided, the shape will be created at the root of the scene. RQL: GameObjectRef |
| `position` | `string` | No | Position of the shape in world or local space. RQL: Vector3 |
| `rotation` | `string` | No | Rotation of the shape in euler angles (degrees). RQL: Vector3 |
| `flipNormals` | `boolean` | No | If true, flip the normals so the faces point inward instead of outward. |
| `isLocalSpace` | `boolean` | No | If true, position/rotation are in local space relative to parent. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "points": {
      "type": "array",
      "items": {
        "type": "array",
        "items": {
          "type": "number"
        }
      }
    },
    "height": {
      "type": "number"
    },
    "name": {
      "type": "string"
    },
    "parentGameObjectRef": {
      "type": "string"
    },
    "position": {
      "type": "string"
    },
    "rotation": {
      "type": "string"
    },
    "flipNormals": {
      "type": "boolean"
    },
    "isLocalSpace": {
      "type": "boolean"
    }
  },
  "required": [
    "points"
  ]
}
```

## Output

This tool does not return structured output.

