---
description: Skill dùng để cung cấp cho AI khi cần tái tạo mã nguồn tính năng "Đọc thông số User Defined Attributes (UDA) của cấu kiện" trong phần mềm tương tác với Tekla Structures bằng Tekla Open API.
---

# SKILL: Tính năng Đọc Thông Số Cấu Kiện (Read Parameters) trong Tekla Structures

Tài liệu này dùng để làm Prompt / Skill cho AI khi phát triển hoặc phục hồi tính năng đọc thông số cấu kiện bằng Tekla API.

## 1. Yêu cầu hệ thống và Kiến trúc
- **Nền tảng**: .NET Framework 4.8 (WinForms).
- **Mô hình lập trình**: MVVM (Model - View - ViewModel).
- **Thư viện Tekla**: `Tekla.Structures.dll`, `Tekla.Structures.Model.dll`.
- **Luồng khởi chạy**: Để tránh lỗi *Could not load file or assembly* do thiếu DLL của Tekla khi mở app (EXE không nằm trong thư mục cài Tekla), phải cấu hình thư viện động bằng cách thêm event `AppDomain.CurrentDomain.AssemblyResolve` trong `Program.cs` trước khi nạp bất kì `Form` nào.

## 2. Cách thức hoạt động
1. Người dùng nhấn nút trên giao diện.
2. Chương trình yêu cầu người dùng chọn 1 cấu kiện trên mô hình 3D (Môi trường Model).
3. Sau khi chọn, hộp thoại riêng lẻ bật lên hiển thị thông số.

## 3. Core Logic & API
- Dùng `Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();`
- Chọn 1 đối tượng: `ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_OBJECT, "Vui lòng chọn đối tượng");`
- Ép kiểu: `if (pickedObject is Part part) { ... }`
- Đọc UDA: Khai báo chuỗi rỗng và dùng `part.GetUserProperty(string propertyName, ref string value)`.
- Các thuộc tính cần lấy:
  * `USER_FIELD_1`, `USER_FIELD_2`, `USER_FIELD_3`, `USER_FIELD_4`.
  * `comment`, `PRELIM_MARK`.
- Hiển thị kết quả ra TextBox Multiline trên cửa sổ giao diện phụ (không dùng MessageBox).

## 4. Cấu trúc MVVM 
- **View**: `ParameterForm.cs` (Cửa sổ hiển thị TextBox). Nút kích hoạt lệnh ở `MainForm.cs` sẽ `new ParameterForm().ShowDialog()`. Form này tạm ẩn `this.Hide()` trong khi người dùng thao tác Pick trên màn hình Tekla.
- **ViewModel**: `ParameterViewModel.cs` - chứa logic khởi tạo `Picker`, kiểm tra điều kiện kết nối với Tekla, truy xuất `GetUserProperty`, và trả về chuỗi (string) dữ liệu để View hiển thị.
- **Model**: Class `TeklaModelMng` quản lý instance của `Model` (`new Model()`) và kiểm tra `GetConnectionStatus()`.
