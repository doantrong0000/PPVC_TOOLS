using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Model.CheckRebar
{
    public enum CheckRebarAction
    {
        None,
        Select,
        SelectAndIsolate,
        ResetView
    }

    public class CheckRebarEventHandler : IExternalEventHandler
    {
        public CheckRebarAction CurrentAction { get; set; } = CheckRebarAction.None;
        public string SearchText { get; set; } = string.Empty;
        public Action<string, bool> StatusCallback { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument uidoc = app.ActiveUIDocument;
                if (uidoc == null) return;
                Document doc = uidoc.Document;
                View activeView = doc.ActiveView;
                if (activeView == null) return;

                if (CurrentAction == CheckRebarAction.ResetView)
                {
                    using (Transaction tr = new Transaction(doc, "Bỏ hiện riêng thanh thép"))
                    {
                        tr.Start();
                        if (activeView.IsTemporaryHideIsolateActive())
                        {
                            activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        }
                        tr.Commit();
                    }
                    StatusCallback?.Invoke("Đã khôi phục chế độ hiển thị mặc định của View.", true);
                    CurrentAction = CheckRebarAction.None;
                    return;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    StatusCallback?.Invoke("Vui lòng nhập số thanh thép cần tìm.", false);
                    CurrentAction = CheckRebarAction.None;
                    return;
                }

                // Split search string by space, comma, semicolon, tab, newlines
                var targetNumbers = SearchText
                    .Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (targetNumbers.Count == 0)
                {
                    StatusCallback?.Invoke("Không tìm thấy giá trị số thanh hợp lệ.", false);
                    CurrentAction = CheckRebarAction.None;
                    return;
                }

                // Collect rebars in active view
                FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
                var rebars = collector.OfCategory(BuiltInCategory.OST_Rebar)
                                      .WhereElementIsNotElementType()
                                      .ToList();

                List<ElementId> matchingIds = new List<ElementId>();
                HashSet<string> foundNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (Element elem in rebars)
                {
                    string rebarNum = GetRebarNumber(elem);
                    if (!string.IsNullOrEmpty(rebarNum) && targetNumbers.Contains(rebarNum))
                    {
                        matchingIds.Add(elem.Id);
                        foundNumbers.Add(rebarNum);
                    }
                }

                if (matchingIds.Count == 0)
                {
                    StatusCallback?.Invoke($"Không tìm thấy thanh thép nào có số: {string.Join(", ", targetNumbers)}", false);
                    CurrentAction = CheckRebarAction.None;
                    return;
                }

                // Select matching rebars in Revit UI
                uidoc.Selection.SetElementIds(matchingIds);

                if (CurrentAction == CheckRebarAction.SelectAndIsolate)
                {
                    // Find all other rebars in active view that do NOT match the search
                    HashSet<ElementId> matchingSet = new HashSet<ElementId>(matchingIds);
                    List<ElementId> otherRebarIds = rebars
                        .Where(r => !matchingSet.Contains(r.Id))
                        .Select(r => r.Id)
                        .ToList();

                    using (Transaction tr = new Transaction(doc, "Ẩn tạm các thanh thép khác"))
                    {
                        tr.Start();
                        if (activeView.IsTemporaryHideIsolateActive())
                        {
                            activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        }

                        if (otherRebarIds.Count > 0)
                        {
                            activeView.HideElementsTemporary(otherRebarIds);
                        }
                        tr.Commit();
                    }
                    StatusCallback?.Invoke($"Đã chọn {matchingIds.Count} thanh thép và ẩn tạm {otherRebarIds.Count} thanh thép khác.", true);
                }
                else if (CurrentAction == CheckRebarAction.Select)
                {
                    StatusCallback?.Invoke($"Đã chọn {matchingIds.Count} thanh thép (Số: {string.Join(", ", foundNumbers)}).", true);
                }
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Lỗi: {ex.Message}", false);
            }
            finally
            {
                CurrentAction = CheckRebarAction.None;
            }
        }

        private string GetRebarNumber(Element elem)
        {
            // Built-in parameter for Rebar Number
            Parameter p = elem.get_Parameter(BuiltInParameter.REBAR_NUMBER);
            if (p != null)
            {
                string val = p.AsString();
                if (string.IsNullOrEmpty(val)) val = p.AsValueString();
                if (!string.IsNullOrEmpty(val)) return val.Trim();
            }

            // Fallback parameters
            Parameter p2 = elem.LookupParameter("Rebar Number") ?? elem.LookupParameter("Số thanh");
            if (p2 != null)
            {
                string val = p2.AsString();
                if (string.IsNullOrEmpty(val)) val = p2.AsValueString();
                if (!string.IsNullOrEmpty(val)) return val.Trim();
            }

            return string.Empty;
        }

        public string GetName() => "CheckRebarEventHandler";
    }
}
