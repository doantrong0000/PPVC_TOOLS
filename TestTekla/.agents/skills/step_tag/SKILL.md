---
name: step_tag
description: Generates a step elevation symbol (Z-bar) between two overlapping parts in a Tekla drawing based on their 3D Z-level difference.
---

# Feature: Drawing Step Elevation Tag (Z-Bar)

This feature automatically generates a standard symbol for floor/slab step elevations in Tekla Structures drawings. It calculates the height difference between parts directly from the 3D model.

## 1. Core Logic & Implementation

The logic implemented in `StepTagViewModel.cs` follows these steps:

### Selection & Validation
- **Requirement**: Must be in an active drawing (`DrawingHandler.GetActiveDrawing()`).
- **Input**: User selects multiple parts (Slabs, Beams, etc.) in the drawing.
- **Process**: The code iterates through selected objects and finds pairs of overlapping parts.

### 3D Coordinate Analysis
- **Model Mapping**: For each pair in the drawing, the tool retrieves the corresponding 3D Model Objects using `ModelIdentifier`.
- **Top Level Detection**: Retrieves the `Solid` of each part and finds the maximum Z-coordinate (`MaximumPoint.Z`).
- **Difference Calculation**: $\Delta Z = |z_1 - z_2|$. If the difference is negligible (< 0.1mm), the pair is ignored.

### Projection & Geometry (2D/3D Mapping)
- **View Transformation**: Projects the 3D solids into the 2D View's coordinate system using `MatrixFactory.ToCoordinateSystem(view.DisplayCoordinateSystem)`.
- **Overlap Detection**: Calculates the intersection area of the projected bounding boxes.
- **Orientation**: Determines if the joint is "Horizontal" or "Vertical" based on the aspect ratio of the overlap area.

### Symbol Generation
The symbol consists of:
1. **Z-Bar Lines**: Three lines connecting the higher level, the vertical/sloped step, and the lower level.
2. **Hatching**: 
   - **Line Mode**: Parallel lines drawn with specified spacing and length.
   - **Polygon Mode**: A solid-filled polygon (`ANSI31_13` or custom) representing the step depth.
3. **Height Text**: 
   - Displays the calculated $\Delta Z$.
   - **Font Style**: Configurable name, height, and color (`DrawingColors`).
   - **Auto-Rotation**: Text is automatically rotated to be parallel/perpendicular to the joint, with "right-side-up" logic.

## 2. API Reference & Parameters

### `CreateStepTag` Method Signature:
```csharp
public string CreateStepTag(
    double textHeight, 
    string fontName, 
    string textColor, 
    double surfLen, 
    double stepHeight, 
    double hatchSpc, 
    double hatchLen, 
    bool useRectFill = false, 
    string fillName = "ANSI31_13"
)
```

### Parameters Detail:
- **`textHeight`**: The font height in drawing units (mm).
- **`fontName`**: The name of the font (e.g., "Arial").
- **`textColor`**: The color name for the text (maps to `DrawingColors`).
- **`surfLen`**: The extension length of the horizontal/vertical lines of the Z-bar.
- **`stepHeight`**: The length of the offset line connecting the two levels.
- **`hatchSpc`**: Spacing between hatching lines.
- **`hatchLen`**: The depth of the hatching lines.
- **`useRectFill`**: If true, uses a filled polygon instead of lines.
- **`fillName`**: The hatch pattern name (e.g., "ANSI31_13").

## 3. Best Practices
- **Scale Awareness**: All geometry calculations multiply parameters by `view.Attributes.Scale` to ensure the symbol looks correct at any drawing scale.
- **Z-Ordering**: The hatching is inserted before lines and text so it appears in the background.
- **Commit**: Always call `dh.GetActiveDrawing().CommitChanges()` at the end of the operation to persist changes.
