---
name: probuilder-create-shape
description: Create a new editable `ProBuilderMesh` GameObject in the active scene from a `ShapeType` primitive (Cube, Cylinder, Sphere, Plane, Prism, Cone, Stair, etc.). Optionally set name, parent, transform, size, and world/local space.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Create a ProBuilder shape

Create a new editable `ProBuilderMesh` GameObject in the active scene. ProBuilder shapes are editable 3D meshes that can be further modified with the rest of the ProBuilder tools (extrude, bevel, subdivide, etc.).

## Inputs

- `shapeType` — `ShapeType` enum (Cube, Cylinder, Sphere, Plane, Prism, Cone, Stair, Door, Pipe, Arch, Sprite, Torus, etc.).
- `name` — optional GameObject name.
- `parentGameObjectRef` — optional parent; root of the scene when omitted.
- `position`, `rotation`, `scale` — optional `Vector3` transform values. Rotation is euler degrees. Defaults: zero / zero / one.
- `size` — `Vector3` width/height/depth of the generated shape. Default `(1, 1, 1)`.
- `isLocalSpace` — when `true`, position/rotation/scale are interpreted in the parent's local space; otherwise world space.

## Behavior

Uses `ShapeGenerator.CreateShape` with `PivotLocation.Center`, applies the transform values, rebuilds the mesh, marks dirty, and repaints the Editor. The whole call runs on the Unity main thread.

## How to Call

```bash
unity-mcp-cli run-tool probuilder-create-shape --input '{
  "shapeType": "string_value",
  "name": "string_value",
  "parentGameObjectRef": "string_value",
  "position": "string_value",
  "rotation": "string_value",
  "scale": "string_value",
  "size": "string_value",
  "isLocalSpace": false
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool probuilder-create-shape --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool probuilder-create-shape --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `shapeType` | `string` | Yes | The type of shape to create. |
| `name` | `string` | No | Name of the new GameObject. |
| `parentGameObjectRef` | `string` | No | Parent GameObject reference. If not provided, the shape will be created at the root of the scene. RQL: GameObjectRef |
| `position` | `string` | No | Position of the shape in world or local space. RQL: Vector3 |
| `rotation` | `string` | No | Rotation of the shape in euler angles (degrees). RQL: Vector3 |
| `scale` | `string` | No | Scale of the shape. RQL: Vector3 |
| `size` | `string` | No | Size of the shape (width, height, depth). Default is (1, 1, 1). RQL: Vector3 |
| `isLocalSpace` | `boolean` | No | If true, position/rotation/scale are in local space relative to parent. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "shapeType": {
      "type": "string",
      "enum": [
        "Cube",
        "Stair",
        "CurvedStair",
        "Prism",
        "Cylinder",
        "Plane",
        "Door",
        "Pipe",
        "Cone",
        "Sprite",
        "Arch",
        "Sphere",
        "Torus"
      ]
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
    "scale": {
      "type": "string"
    },
    "size": {
      "type": "string"
    },
    "isLocalSpace": {
      "type": "boolean"
    }
  },
  "required": [
    "shapeType"
  ]
}
```

## Output

This tool does not return structured output.

