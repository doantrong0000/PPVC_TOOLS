using Autodesk.Revit.DB;
using System;
using System.Linq;

namespace PPVCREVIT.Services
{
    /// <summary>
    /// Service quản lý việc kiểm tra Family trong project Revit.
    /// </summary>
    public static class FamilyLoaderService
    {
        private const string CogMarkerFamilyName = "COG_Marker";

        /// <summary>
        /// Kiểm tra family COG_Marker đã có trong project chưa.
        /// Nếu có → trả về FamilySymbol đã activate.
        /// Nếu chưa → trả về null.
        /// </summary>
        public static FamilySymbol GetCogMarkerSymbol(Document doc)
        {
            return GetFamilySymbol(doc, CogMarkerFamilyName);
        }

        /// <summary>
        /// Tìm Family trong project theo tên và trả về FamilySymbol đầu tiên (đã activate).
        /// Trả về null nếu không tìm thấy.
        /// </summary>
        public static FamilySymbol GetFamilySymbol(Document doc, string familyName)
        {
            Family family = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

            if (family == null)
                return null;

            ElementId symbolId = family.GetFamilySymbolIds().FirstOrDefault();
            if (symbolId == null || symbolId == ElementId.InvalidElementId)
                return null;

            FamilySymbol symbol = doc.GetElement(symbolId) as FamilySymbol;

            if (symbol != null && !symbol.IsActive)
                symbol.Activate();

            return symbol;
        }
    }
}
