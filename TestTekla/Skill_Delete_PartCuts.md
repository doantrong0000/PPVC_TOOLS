---
description: Skill dùng để cung cấp cho AI khi cần tái tạo mã nguồn tính năng "Xóa toàn bộ các đường cắt (PartCut/BooleanPart) trên một cấu kiện" trong Tekla Structures thông qua Tekla Open API.
---

# SKILL: Tính năng Xóa PartCut trong Tekla Structures

Tài liệu này là Prompt / Skill dành cho AI để tái tạo hoặc phân tích tính năng Xóa nét cắt.

## 1. Yêu cầu hệ thống và Kiến trúc
- **Nền tảng**: .NET Framework 4.8 (WinForms).
- **Mô hình lập trình**: MVVM (Model - View - ViewModel).
- **Thư viện Tekla**: `Tekla.Structures.dll`, `Tekla.Structures.Model.dll`.
- **Luồng khởi chạy**: Load tự động Assembly Tekla trong `Program.cs` (`AssemblyResolve`).

## 2. Môi trường và Cách dùng
- Thao tác trên môi trường 3D Model.
- Người dùng nhấp một lệnh, API hiển thị hướng dẫn yêu cầu người dùng pick vào 1 cấu kiện cần làm sạch rác/vết cắt.
- Khi chọn xong, xóa toàn bộ các nét khoét trên nó và hiển thị số lượng nét cắt đã xóa.

## 3. Core Logic & Cơ chế Tekla API
- Chức năng Pick cấu kiện: `ModelObject pickedObject = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART);`
- Ép kiểu đối tượng gốc (Host Part): `if (pickedObject is Part hostPart)`
- Lấy các đối tượng Boolean phụ thuộc (vết cắt, đường hàn): `ModelObjectEnumerator cutEnumerator = hostPart.GetBooleans();`
- Vòng lặp xóa vết cắt: 
  Sử dụng vòng lặp `while (cutEnumerator.MoveNext())`, quét từng thành phần bên trong.
  Ép kiểu để đảm bảo nó là vết cắt: `if (cutEnumerator.Current is BooleanPart booleanCut)`
  Thực hiện lệnh xóa vật thể phụ trợ này: `booleanCut.Delete();`
- Ghi nhận thay đổi xuống Tekla: `new Model().CommitChanges();`

## 4. Xử lý UI
- Tính năng này hiển thị thông báo kết quả (Ví dụ: "Đã xóa 5 PartCut thành công") trực tiếp bằng `MessageBox.Show()`, không cần sinh Form mới.
