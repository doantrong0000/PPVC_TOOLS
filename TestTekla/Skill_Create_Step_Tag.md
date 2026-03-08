---
description: Skill dùng để cung cấp cho AI khi cần tái tạo mã nguồn tính năng "Tự động tạo chữ Ký hiệu Giật cấp trong bản vẽ (Drawing Step Elevation Tag)" thông qua việc chọn cấu kiện, tính toán sự khác biệt hệ số Z từ mô hình 3D, và vẽ đồ hoạ (Polygon, Line, Text).
---

# SKILL: Tính năng Ký hiệu giật cấp bản vẽ (Step Tag) trong Tekla Structures

Tài liệu này chứa thuật toán chi tiết để thực thi tính năng tạo biểu đồ "ký hiệu Giật Cấp sàn" ngay trên bản vẽ Tekla 2D (Sử dụng `Tekla.Structures.Drawing.dll`).

## 1. Yêu cầu hệ thống và Core MVVM
- .NET 4.8 WinForms, `AssemblyResolve` như cũ.
- Đặc biệt, sử dụng thêm các Namespace không gian hình học 3D để mapping:
  `using Tekla.Structures.Drawing;`
  `using Tekla.Structures.Drawing.UI;`
  `using Tekla.Structures.Model;`
  `using Tekla.Structures.Geometry3d;`

## 2. Cách dùng
1. Yêu cầu người dùng đang mở 1 file bản vẽ. (Xác minh bằng `dh.GetActiveDrawing() != null`).
2. API Picker (của `DrawingHandler`) chờ Pick 2 vật thể trong mặt bằng bản vẽ: Cấu kiện 1 và Cấu kiện 2. `var pick1 = picker.PickObject("Sàn 1"); DrawingObject dPart1 = pick1.Item1;`
3. Click điểm thứ 1 tại mặt cắt để đặt điểm gốc (chính giữa). `var ptPick1 = picker.PickPoint(); Point pStart = ptPick1.Item1;`
4. Click điểm thứ 2 bám theo đường chéo để báo cho Tekla biết hướng song song chéo của mặt sàn (hướng rải góc nghiêng của View / Part).

## 3. Core Logic & Geometry API Mở Rộng
### Chuyển đối tượng 2D sang 3D Model:
- Từ `DrawingObject` chuyển ModelIdentifier sang Model Object tương ứng ở môi trường 3D.
  `Model model = new Model();`
  `Model.Part mPart1 = model.SelectModelObject(dp1.ModelIdentifier) as Model.Part;`

### Tính toán chênh lệch cao độ (`ΔZ`):
- Lấy đỉnh cao nhất (Top Level) của bề mặt sàn 3D thay vì ReportProperty để tránh phụ thuộc Numbering:
  `Solid solid1 = mPart1.GetSolid();`
  `double z1 = solid1.MaximumPoint.Z;`
- `ΔZ` (đơn vị nguyên) = `Math.Round(Math.Abs(z1 - z2))`. Nếu dưới `0.1`, hủy bỏ lệnh.

### Thuật toán xác định sàn nào nằm bên trên/dưới để dựng Z-bar:
- Trong bản vẽ có thể bị nghiêng góc view, ta cần chuyển `Center` của `Solid 1` qua Coordinate của `View 2D` để so sánh với điểm `pStart` người dùng Pick.
  - Sử dụng `MatrixFactory.ToCoordinateSystem(view.DisplayCoordinateSystem);` để lấy ma trận dịch ngược. Transform tâm `Center 1` (3D global) theo phương XY sang tọa độ không gian tờ giấy. Đầu ra là một `Vector` giữa mặt Sàn số 1 tới Mép Giao (`pStart`).
  - Lấy hướng mép giao: `Vector vY = Normalized(pEnd - pStart)` (đây là chiều dọc của mép).
  - Trục ngang (`vX`) vuông góc đường pick: `Vector vX = new Vector(vY.Y, -vY.X, 0)`.
  - Dùng tích vô hướng (`Dot Product`) giữa vX và vị trí tâm khối để quyết định `vHighX` (hướng từ trọng tâm ra cho Sàn cao) và `vLowX` cho Sàn thấp.

### Dựng hình (Polygons & Lines):
- Điều chỉnh hằng số `L = 3.0 * scale` (scale lấy từ View).
- **Phần vuông tô đậm (Giật cấp)**: Sử dụng các lớp 2D Point. Gọi `Tekla.Structures.Drawing.Polygon(view, pointsList)`.
  - Phủ Hatch (gạch sọc ANSI31 màu xanh lá - `DrawingHatchColors.Green`).
  - Phủ đường viền ẩn (`DrawingColors.Green`) cho nó trùng màu. Insert 2 Poly (1 cho High và 1 cho Low) đầu tiên để "Chìm xuống dưới Base".
- **Phần Lines**: Dùng `Tekla.Structures.Drawing.Line` kéo thành 3 đoạn hình chữ Z nối mép ngang cao - dọc theo vY - mép ngang thấp. Insert thứ hai.
- **Phần Text**: Tạo `Text(view, position, valueZ)`. 
  - Đặt nó ở trọng tâm `vHighX`, nghiêng góc song song vY (`Angle = Math.Atan2(vY.Y, vY.X) * 180 / PI`).
  - Khắc phục chữ lộn ngược nếu nhỏ hơn/hoặc lớn hơn 90 độ, thay góc `+180`.

- Commit Thay đổi bản vẽ: `dh.GetActiveDrawing().CommitChanges();`
