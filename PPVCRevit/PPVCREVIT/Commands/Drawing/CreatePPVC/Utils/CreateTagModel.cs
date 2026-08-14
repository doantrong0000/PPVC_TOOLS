using Autodesk.Revit.DB;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Models;
using PPVCREVIT.Utils.FamiliesUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Utils
{
    public static class CreateTagModel
    {
        /// <summary>
        /// Lấy FamilySymbol của Rebar Tag dựa trên cấu hình (có hỗ trợ các tầng fallback).
        /// </summary>
        public static FamilySymbol GetRebarTagSymbol(Document doc)
        {
            // Tìm hoặc load Family Symbol cho Rebar Tag (1 Family có nhiều Types)
            FamilySymbol rebarTagSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_RebarTags)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Family.Name.Equals(CreatePPVCConfig.RebarTag.FamilyName, StringComparison.OrdinalIgnoreCase)
                                      && fs.Name.Equals(CreatePPVCConfig.RebarTag.Type4, StringComparison.OrdinalIgnoreCase));

            if (rebarTagSymbol == null)
            {
                // Fallback 1: Lấy type đầu tiên của Family chỉ định
                rebarTagSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_RebarTags)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs => fs.Family.Name.Equals(CreatePPVCConfig.RebarTag.FamilyName, StringComparison.OrdinalIgnoreCase));
            }

            if (rebarTagSymbol == null)
            {
                // Fallback 2: Lấy tag đầu tiên của category OST_RebarTags
                rebarTagSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_RebarTags)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();
            }

            return rebarTagSymbol;
        }

        /// <summary>
        /// Lấy FamilySymbol của Rebar Tag dựa trên tên type chỉ định (ví dụ "Type 3", "Type3", hoặc tên cụ thể).
        /// </summary>
        public static FamilySymbol GetRebarTagSymbol(Document doc, string tagTypeName)
        {
            if (doc == null) return null;
            if (string.IsNullOrEmpty(tagTypeName)) return GetRebarTagSymbol(doc);

            // Xác định tên type thực tế từ config nếu có (ví dụ "Type 3" -> RebarTag.Type3)
            string actualTypeName = tagTypeName;
            if (tagTypeName.Equals("Type 3", StringComparison.OrdinalIgnoreCase) || tagTypeName.Equals("Type3", StringComparison.OrdinalIgnoreCase) || tagTypeName.Equals("3"))
            {
                actualTypeName = !string.IsNullOrEmpty(CreatePPVCConfig.RebarTag.Type3) ? CreatePPVCConfig.RebarTag.Type3 : tagTypeName;
            }
            else if (tagTypeName.Equals("Type 4", StringComparison.OrdinalIgnoreCase) || tagTypeName.Equals("Type4", StringComparison.OrdinalIgnoreCase) || tagTypeName.Equals("4"))
            {
                actualTypeName = !string.IsNullOrEmpty(CreatePPVCConfig.RebarTag.Type4) ? CreatePPVCConfig.RebarTag.Type4 : tagTypeName;
            }

            // 1. Thử khớp chính xác FamilyName + actualTypeName
            FamilySymbol symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_RebarTags)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Family.Name.Equals(CreatePPVCConfig.RebarTag.FamilyName, StringComparison.OrdinalIgnoreCase)
                                      && fs.Name.Equals(actualTypeName, StringComparison.OrdinalIgnoreCase));

            // 2. Tìm theo từ khóa trong Family WH_RebarTag_v26
            if (symbol == null)
            {
                Family family = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .FirstOrDefault(f => f.Name.Equals(CreatePPVCConfig.RebarTag.FamilyName, StringComparison.OrdinalIgnoreCase));

                if (family != null)
                {
                    foreach (ElementId id in family.GetFamilySymbolIds())
                    {
                        var sym = doc.GetElement(id) as FamilySymbol;
                        if (sym != null && (sym.Name.Equals(actualTypeName, StringComparison.OrdinalIgnoreCase) ||
                                            sym.Name.IndexOf(tagTypeName, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            symbol = sym;
                            break;
                        }
                    }
                }
            }

            // 3. Fallback dùng GetRebarTagSymbol mặc định
            if (symbol == null)
            {
                symbol = GetRebarTagSymbol(doc);
            }

            return symbol;
        }

        /// <summary>
        /// Lấy FamilySymbol cho Slab Tag (WH_SlabTag_v26) theo tên Type mong muốn (ví dụ "SFL + THK" cho floor plan hoặc "THK" cho roof plan).
        /// </summary>
        public static FamilySymbol GetSlabTagSymbol(Document doc, string preferredTypeName)
        {
            if (doc == null || string.IsNullOrEmpty(preferredTypeName)) return null;

            // 1. Thử lấy chính xác theo Family Name "WH_SlabTag_v26" và Type Name
            FamilySymbol slabTagSymbol = LoadFamilyUtils.GetFamilySymbol(doc, "WH_SlabTag_v26", preferredTypeName);

            // 2. Tìm theo từ khóa trong Family "WH_SlabTag_v26"
            if (slabTagSymbol == null)
            {
                Family family = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .FirstOrDefault(f => f.Name.Equals("WH_SlabTag_v26", StringComparison.OrdinalIgnoreCase));

                if (family != null)
                {
                    foreach (ElementId id in family.GetFamilySymbolIds())
                    {
                        var sym = doc.GetElement(id) as FamilySymbol;
                        if (sym != null)
                        {
                            if (preferredTypeName.Contains("SFL"))
                            {
                                if (sym.Name.IndexOf("SFL", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    slabTagSymbol = sym;
                                    break;
                                }
                            }
                            else if (preferredTypeName.Equals("THK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (sym.Name.IndexOf("THK", StringComparison.OrdinalIgnoreCase) >= 0 && sym.Name.IndexOf("SFL", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    slabTagSymbol = sym;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Fallback: Lấy symbol đầu tiên của family WH_SlabTag_v26
            if (slabTagSymbol == null)
            {
                slabTagSymbol = LoadFamilyUtils.GetFamilySymbol(doc, "WH_SlabTag_v26");
            }

            // 4. Fallback cuối: Lấy tag bất kỳ thuộc category OST_FloorTags
            if (slabTagSymbol == null)
            {
                var floorTags = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_FloorTags)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .ToList();

                if (preferredTypeName.Contains("SFL"))
                {
                    slabTagSymbol = floorTags.FirstOrDefault(fs => fs.Name.IndexOf("SFL", StringComparison.OrdinalIgnoreCase) >= 0) ?? floorTags.FirstOrDefault();
                }
                else
                {
                    slabTagSymbol = floorTags.FirstOrDefault(fs => fs.Name.IndexOf("THK", StringComparison.OrdinalIgnoreCase) >= 0 && fs.Name.IndexOf("SFL", StringComparison.OrdinalIgnoreCase) < 0) ?? floorTags.FirstOrDefault();
                }
            }

            return slabTagSymbol;
        }
    }
}
