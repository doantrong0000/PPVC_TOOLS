using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
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
    }
}
