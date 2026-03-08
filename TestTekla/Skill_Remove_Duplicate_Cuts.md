---
description: Skill dùng để cung cấp cho AI khi cần tái tạo mã nguồn tính năng "Tìm và xóa các đường cắt trùng lặp/giống y hệt nhau trên 1 cấu kiện chính" (Remove Duplicate Cuts) dựa trên chữ ký trong Tekla Open API.
---

# SKILL: Tính năng Xóa Cắt Trùng (Remove Duplicate Cuts) trong Tekla Structures

Tài liệu này hữu ích khi tạo hoặc bảo trì thuật toán loại bỏ bớt phần tử boolean trùng nhau trên một Host Part.

## 1. Yêu cầu hệ thống và MVVM
- Nền tảng: .NET 4.8 WinForms.
- Tương tự các app độc lập ngoài khác (`AppDomain.AssemblyResolve` dll).

## 2. Cách thức sử dụng
- Pick đối tượng trong mô hình 3D (Cấu kiện).
- Hiển thị kết quả (MessageBox) số lượng vết cắt cũ, số nét trùng đã xóa, số nét cắt còn giữ lại.

## 3. Core Logic & Thuật Toán Chữ Ký
- Lấy đối tượng Picker: `ModelObject pickedObject = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART);`
- Khi lấy danh sách vết khuyết bằng hàm `GetBooleans()`, ta lặp qua từng `BooleanPart`.
- Nhận diện vết cắt này bằng phần tử `OperativePart` (là Part tạo ra nét cắt): `Part cuttingPart = booleanCut.OperativePart;`
- Nhận diện trùng lặp (`Duplicate`) bằng kỹ thuật "Tạo chữ ký định danh" - *Signature*.
  + Yếu tố 1: Thể tích. `double volume = 0; cuttingPart.GetReportProperty("VOLUME", ref volume);`
  + Yếu tố 2: Điểm giữa theo 3 trục không gian (MidX, MidY, MidZ). Lấy `Solid` của `OperativePart`: `var solid = cuttingPart.GetSolid();`. Tính toán tạo cặp `MidX = (solid.MinimumPoint.X + solid.MaximumPoint.X) / 2.0`. Lưu ý: Nên là `Math.Round()` để hạn chế sai số float (Ví dụ `Math.Round(val, 1)` độ chính xác 1 số lẻ).
  + Ký hiệu ghép lại thành chuỗi: `string signature = $"{Math.Round(volume, 2)}_{midX}_{midY}_{midZ}";`
- Dùng cấu trúc `Dictionary<string, BooleanPart>` để đối chiếu. Khởi tạo một Dictionary mới mỗi khi thực thi. Nếu `ContainsKey(signature)` thì có nghĩa là phát hiện nét trùng (trùng thể tích và vị trí trọng tâm) -> `booleanCut.Delete()`. Nếu chưa có -> `Add(signature, booleanCut)`.
- Ghi đè vào Model: `model.CommitChanges()`.
