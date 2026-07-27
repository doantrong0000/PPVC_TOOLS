using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace PPVCREVIT.Utils.FamiliesUtils
{
    public class FamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }

    public static class LoadFamilyUtils
    {
        /// <summary>
        /// Loads a family from the given file path.
        /// </summary>
        public static bool LoadFamily(Document doc, string familyPath, out Family family)
        {
            family = null;
            if (string.IsNullOrEmpty(familyPath) || !File.Exists(familyPath))
            {
                return false;
            }

            try
            {
                return doc.LoadFamily(familyPath, new FamilyLoadOptions(), out family);
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// Finds a family symbol by family name and symbol name. Activates it if found.
        /// If the family is not found in the project, it tries to load it from the Family directory.
        /// </summary>
        public static FamilySymbol GetFamilySymbol(Document doc, string familyName, string symbolName = null)
        {
            Family family = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));
            if (family == null)
            {
                return null;
            }

            FamilySymbol symbol = null;
            var symbolIds = family.GetFamilySymbolIds();

            if (string.IsNullOrEmpty(symbolName))
            {
                // If no symbol name is specified, take the first one
                var symbolId = symbolIds.FirstOrDefault();
                if (symbolId != null && symbolId != ElementId.InvalidElementId)
                {
                    symbol = doc.GetElement(symbolId) as FamilySymbol;
                }
            }
            else
            {
                // Match by name
                foreach (var id in symbolIds)
                {
                    var sym = doc.GetElement(id) as FamilySymbol;
                    if (sym != null && sym.Name.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                    {
                        symbol = sym;
                        break;
                    }
                }
            }

            // Activate symbol in a transaction if not active
            if (symbol != null && !symbol.IsActive)
            {
                using (Transaction trans = new Transaction(doc, $"Activate {symbol.Name}"))
                {
                    trans.Start();
                    symbol.Activate();
                    trans.Commit();
                }
            }

            return symbol;
        }
    }
}

