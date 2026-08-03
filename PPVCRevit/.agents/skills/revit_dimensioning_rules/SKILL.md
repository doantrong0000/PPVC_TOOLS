---
name: Revit PPVC Dimensioning and Tagging Rules
description: Guidelines and rules for placing tags and dimension lines for PPVC modules, walls, beams, and slabs in Revit view drawings.
---

# Revit PPVC Dimensioning and Tagging Rules

Use this skill when implementing automated tagging and dimensioning scripts or commands for PPVC (Precast Prefabricated Volumetric Construction) modules, slab drawings, or wall layouts in Revit.

## 1. Dynamic Element Tagging
- **Leaderless Tags**: All tags (e.g., floor tags, beam tags, wall tags) should generally have leader lines disabled (`HasLeader = false`) unless explicitly requested otherwise.
- **Components Overlap Prevention**: Do not place tags directly at the geometric center of linear structural elements to avoid overlaps. Use a dynamic offset of `1.4` feet from the center:
  - **Slab/Floor Views**: Place tags pushing outward (e.g. beam tags pushed up if in the top half, down if in the bottom half).
  - **Wall Layout Views**: Because side dimensions are placed on the exterior, tags must be pushed **inward** toward the layout center to prevent overlaps with dimension lines:
    - **Horizontal Walls (parallel to X-axis)**: Offset along the Y-axis. Push it down if in the top half, and up if in the bottom half.
    - **Vertical Walls (parallel to Y-axis)**: Offset along the X-axis. Push it left if in the right half, and right if in the left half.

## 2. Layout-Based Side Dimensions (Dim các tường cùng phía)
- Walls or structural components on the same side of the module (Left, Right, Top, Bottom) must share a single dimension line rather than having separate lines for each element.
- **Detailed Dimensions**: Sort the faces by coordinate and group those that are collinear within a tolerance (e.g., 0.01 feet / 3mm). Measure them in a single chain.
- **Side Allocations**:
  - **Left Side**: Typically contains an inner detailed dimension chain and an outer overall length dimension line.
  - **Right Side**: Typically contains an inner detailed dimension chain.
  - **Top Side**: Typically contains an outer overall width dimension line.

## 3. Shearkey (Void) Dimensions
- Voids (like shearkeys) inside walls should be dimensioned on an **individual wall basis** (one dimension line per wall containing voids).
- **Dimension Chain**: The dimension line measures the distance: `Wall End Face 1 -> Center Plane of Void 1 -> Center Plane of Void 2 -> ... -> Wall End Face 2`.
- **Directional Offset (Pull-to-Side)**: To prevent dimension lines from cluttering the inside of the module layout, pull the shearkey dimension lines to the **outer sides** of the wall layout:
  - **Vertical Wall on Left half (X < midX)**: Offset shearkey dim line to the **Left** (-1.5 feet).
  - **Vertical Wall on Right half (X >= midX)**: Offset shearkey dim line to the **Right** (+1.5 feet).
  - **Horizontal Wall on Bottom half (Y < midY)**: Offset shearkey dim line **Down** (-1.5 feet).
  - **Horizontal Wall on Top half (Y >= midY)**: Offset shearkey dim line **Up** (+1.5 feet).

## 4. Void Center Reference Resolution
- When querying reference planes of a void `FamilyInstance`, use the hand or facing orientation vectors to identify the correct center plane perpendicular to the wall direction:
  - If `Math.Abs(fi.HandOrientation.DotProduct(wallDirection)) > 0.9`, use `FamilyInstanceReferenceType.CenterLeftRight`.
  - If `Math.Abs(fi.FacingOrientation.DotProduct(wallDirection)) > 0.9`, use `FamilyInstanceReferenceType.CenterFrontBack`.
