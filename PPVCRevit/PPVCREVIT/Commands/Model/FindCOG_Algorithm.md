# Thuật Toán Tính Trọng Tâm (COG) Trong Revit

## 1. Tổng Quan

**Mục tiêu:** Tính trọng tâm tổng hợp (Center of Gravity) của nhiều cấu kiện được chọn trong Revit, có xét đến **khối lượng riêng** của từng vật liệu.

**Công thức cốt lõi — Trọng tâm có trọng số (Weighted Centroid):**

```
           Σ (Cᵢ × Wᵢ)
COG = ─────────────────
              Σ Wᵢ

Trong đó:
  Cᵢ = Trọng tâm của phần tử thứ i (XYZ)
  Wᵢ = Khối lượng = Vᵢ × ρᵢ
  Vᵢ = Thể tích
  ρᵢ = Khối lượng riêng (Density)
```

---

## 2. Luồng Xử Lý Chính

```
Người dùng chọn cấu kiện (PickObjects)
          │
          ▼
┌─────────────────────────┐
│  Phân loại từng Element  │
└────────┬───────┬────────┘
         │       │
    Rebar?      Solid?
         │       │
         ▼       ▼
  Tính COG    Tính COG
  theo        theo
  Centerline  Geometry
         │       │
         ▼       ▼
   Cộng dồn có trọng số (Volume × Density)
          │
          ▼
   Chia tổng → COG tổng hợp
          │
          ▼
   Đặt Marker hình trụ (DirectShape)
```

---

## 3. Lấy Khối Lượng Riêng Từ Revit

### 3.1. Cho cấu kiện bê tông / thép hình (Solid Elements)

```
Element
  │
  ├─► get_Parameter(STRUCTURAL_MATERIAL_PARAM)  ──► MaterialId
  │   (ưu tiên)
  │
  ├─► GetMaterialIds(false)                      ──► MaterialId (fallback)
  │
  ▼
Material (doc.GetElement(matId))
  │
  ▼
material.StructuralAssetId
  │
  ▼
PropertySetElement (doc.GetElement(assetId))
  │
  ▼
StructuralAsset.Density  ──► Khối lượng riêng (internal units)
```

**Revit API Chain:**
```csharp
Parameter matParam = el.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
ElementId matId = matParam.AsElementId();
Material material = doc.GetElement(matId) as Material;
PropertySetElement pse = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
StructuralAsset asset = pse.GetStructuralAsset();
double density = asset.Density;  // Internal units, nhất quán với Volume
```

### 3.2. Cho thép (Rebar)

```
Rebar
  │
  ▼
RebarBarType (doc.GetElement(rebar.GetTypeId()))
  │
  ▼
get_Parameter(MATERIAL_ID_PARAM)  ──► MaterialId
  │
  ▼
Material → StructuralAsset.Density  (chuỗi tương tự mục 3.1)
```

### 3.3. Giá trị Fallback

| Vật liệu | Fallback (kg/m³) |
|-----------|:-----------------:|
| Thép      | 7 850             |
| Bê tông   | 2 400             |

> Fallback chỉ dùng khi cấu kiện không gán vật liệu hoặc vật liệu không có Structural Asset.

---

## 4. Tính Trọng Tâm Cho Từng Loại

### 4.1. Cấu kiện Solid (Bê tông, Thép hình...)

Duyệt **đệ quy** qua cây Geometry của Element:

```
GeometryElement
  ├── Solid           → ComputeCentroid() + Volume
  ├── Solid           → ComputeCentroid() + Volume
  └── GeometryInstance
        └── (Symbol Geometry — đệ quy tiếp)
```

**Xử lý Transform:**
- Dùng `GetSymbolGeometry()` (local space) + compose Transform
- `transform.Multiply(instance.Transform)` để xử lý đúng cấu kiện lồng nhau
- **Tránh** dùng `GetInstanceGeometry()` + `instance.Transform` vì sẽ bị **double-transform**

**Công thức cho mỗi Solid:**
```
Wᵢ = Volume × Density
Cᵢ = transform.OfPoint(solid.ComputeCentroid())
```

### 4.2. Rebar (Thép thanh)

Rebar không có Solid geometry trực tiếp → tính qua **đường tâm (Centerline)**:

```
Bước 1: Lấy thông số từ RebarBarType
         ├── BarModelDiameter → đường kính
         └── SectionArea = π × (d/2)²

Bước 2: Lấy số thanh
         └── rebar.Quantity → numberOfBars

Bước 3: Duyệt từng thanh (index i = 0..n-1)
         │
         ├── GetCenterlineCurves(false, false, false, ..., i)
         │   → Trả về IList<Curve> cho thanh thứ i
         │
         ├── Duyệt từng Curve (segment):
         │   ├── Line  → centroid = (P0 + P1) / 2
         │   ├── Arc   → centroid = công thức hình học (xem mục 4.3)
         │   └── Khác  → curve.Evaluate(0.5, true)
         │
         ├── Trọng tâm thanh = Σ(Csegment × Lsegment) / Σ Lsegment
         │
         └── Thể tích thanh = Σ Lsegment × SectionArea
```

**Kết quả trả về:** `(centroid_tổng_hợp, tổng_thể_tích)` cho toàn bộ bộ rải.

### 4.3. Trọng Tâm Cung Tròn (Arc Centroid)

Công thức chính xác cho dây mảnh hình cung:

```
                  sin(α)
d = R × ─────────
                    α

Trong đó:
  R     = Bán kính cung
  α     = Nửa góc ở tâm = (ArcLength / R) / 2
  d     = Khoảng cách từ tâm cung đến trọng tâm

Hướng: Từ Center → MidPoint (điểm giữa cung)

Centroid = Center + normalize(MidPoint - Center) × d
```

---

## 5. Cộng Dồn & Kết Quả

```
weightedCentroidSum = Σ (Cᵢ × Vᵢ × ρᵢ)
totalWeight         = Σ (Vᵢ × ρᵢ)

COG = weightedCentroidSum / totalWeight
```

> Vì `StructuralAsset.Density` và `Solid.Volume` đều dùng **internal units** của Revit, phép tính luôn nhất quán — **không cần convert đơn vị**.

---

## 6. Hiển Thị Marker

Sau khi tính được COG, tạo **DirectShape** hình trụ tại vị trí đó:

```
Bước 1: Tạo CurveLoop hình tròn (2 Arc ghép lại, Revit không hỗ trợ arc 360°)
Bước 2: Extrude theo trục Z → Solid hình trụ
Bước 3: Tạo DirectShape (Category: GenericModel)
         ├── ApplicationId  = "PPVCREVIT"
         ├── ApplicationDataId = "COG_MARKER"
         └── Name = "TRỌNG TÂM CẤU KIỆN"
```

---

## 7. Sơ Đồ Quan Hệ Các Hàm

```
Execute()
  │
  ├── PickObjects() ─────────────────── Chọn cấu kiện
  │
  ├── CalculateCentroidOfMultipleElements()
  │     │
  │     ├── [Rebar] GetAbsolutePreciseRebarCentroid()
  │     │             └── GetArcCentroid()
  │     │
  │     ├── [Rebar] GetRebarDensity()
  │     │             └── StructuralAsset.Density
  │     │
  │     ├── [Solid] ProcessGeometry() ── Đệ quy
  │     │             └── ComputeCentroid() + Volume
  │     │
  │     └── [Solid] GetMaterialDensity()
  │                   └── StructuralAsset.Density
  │
  └── CreateCylinderMarker() ────────── Đặt marker 3D
```
