---
description: Skill dùng để cung cấp cho AI khi cần tái tạo mã nguồn tính năng "Gộp Assembly - Nhập nhiều cấu kiện phụ vào 1 cấu kiện chính" trong Tekla Structures.
---

# SKILL: Tính năng Gộp Assembly (Add to Assembly) trong Tekla Structures

Tài liệu này dùng để thiết lập lại tính năng "Gộp cụm Assembly" cho model 3D Tekla Structures bằng Tekla API.

## 1. Yêu cầu hệ thống
- Nền tảng: .NET Framework 4.8 (WinForms).
- Mô hình: MVVM. Cần load linh động dll của Tekla (`AssemblyResolve`) để có thể run file .EXE trực tiếp.
- API: `Tekla.Structures.dll`, `Tekla.Structures.Model.dll`.

## 2. Cách thức thao tác
- Bước 1: API yêu cầu người dùng chọn 1 cấu kiện để làm "Main Part" (Cấu kiện chính của cụm).
- Bước 2: API yêu cầu dùng chuột quét chọn nhiều cấu kiện ("Secondary Parts"). Người dùng ấn phím cuộn chuột (Middle Mouse) để kết thúc quá trình quét.
- Bước 3: Các thành phần phụ sẽ trở thành Secondary part của Assembly có chứa Main Part đó.

## 3. Core Logic qua Tekla API
- Pick Cấu kiện chính: `ModelObject mainObj = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Chọn cấu kiện chính");`
- Pick Cấu kiện phụ (nhiều tính năng): `ModelObjectEnumerator secondaryObjects = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS, "Quét cấu kiện phụ, click chuột giữa để hoàn tất");`
- Lấy đối tượng kết cấu tổ hợp (Assembly): `Tekla.Structures.Model.Assembly assembly = mainPart.GetAssembly();`
- Duyệt qua `secondaryObjects.MoveNext()`, thêm vào cụm bằng lệnh `assembly.Add(secPart)`.
- Chú ý: Cần kiểm tra ID của secPart khác với mainPart để tránh lỗi tự add chính nó: `secPart.Identifier.ID != mainPart.Identifier.ID`.
- Áp dụng thay đổi: `assembly.Modify();` và sau cùng đẩy vào model bằng `model.CommitChanges();`.
- Kết quả được báo ra màn hình thông qua MessageBox.
