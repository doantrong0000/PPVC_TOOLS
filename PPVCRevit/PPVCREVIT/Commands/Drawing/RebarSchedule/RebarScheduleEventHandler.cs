using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using PPVCREVIT.Commands.Drawing.RebarSchedule.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.RebarSchedule
{
    public enum RebarActionType
    {
        Fetch,
        Select
    }

    public class RebarScheduleEventHandler : IExternalEventHandler
    {
        private readonly RebarScheduleWindow _window;

        public RebarActionType RequestType { get; set; } = RebarActionType.Fetch;
        public string SelectedRebarUniqueId { get; set; } = string.Empty;

        public RebarScheduleEventHandler(RebarScheduleWindow window)
        {
            _window = window;
        }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;
            if (activeView == null) return;

            try
            {
                if (RequestType == RebarActionType.Fetch)
                {
                    // 1. Collect all rebars visible in active view
                    FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
                    IList<Element> rebars = collector.OfCategory(BuiltInCategory.OST_Rebar)
                                                     .WhereElementIsNotElementType()
                                                     .ToElements();

                    // 2. Collect all IndependentTags in active view
                    FilteredElementCollector tagCollector = new FilteredElementCollector(doc, activeView.Id);
                    IList<IndependentTag> tags = tagCollector.OfClass(typeof(IndependentTag))
                                                             .Cast<IndependentTag>()
                                                             .ToList();

                    // Also collect MultiReferenceAnnotation elements (Multi-rebar tags) in active view
                    FilteredElementCollector mraCollector = new FilteredElementCollector(doc, activeView.Id);
                    IList<Element> mras = mraCollector.OfClass(typeof(MultiReferenceAnnotation)).ToElements();

                    // Map rebar unique ID to a set of unique tag/annotation IDs tagging it
                    Dictionary<string, HashSet<ElementId>> rebarTagElements = new Dictionary<string, HashSet<ElementId>>();

                    // Process normal independent tags
                    foreach (IndependentTag tag in tags)
                    {
                        try
                        {
                            ISet<ElementId> taggedIds = tag.GetTaggedLocalElementIds();
                            foreach (ElementId taggedId in taggedIds)
                            {
                                Element taggedElem = doc.GetElement(taggedId);
                                if (taggedElem != null)
                                {
                                    string uniqueId = taggedElem.UniqueId;
                                    if (!rebarTagElements.ContainsKey(uniqueId))
                                    {
                                        rebarTagElements[uniqueId] = new HashSet<ElementId>();
                                    }
                                    rebarTagElements[uniqueId].Add(tag.Id);
                                }
                            }
                        }
                        catch
                        {
                            // Safe catch
                        }
                    }

                    // Process multi-rebar annotations (MRAs)
                    foreach (Element mraElem in mras)
                    {
                        if (mraElem is MultiReferenceAnnotation mra)
                        {
                            ElementId dimId = mra.DimensionId;
                            if (dimId != ElementId.InvalidElementId)
                            {
                                Dimension? dim = doc.GetElement(dimId) as Dimension;
                                if (dim != null)
                                {
                                    try
                                    {
                                        // Register the associated tag ID (or MRA ID itself if tag ID is invalid)
                                        ElementId tagIdToRegister = mra.TagId != ElementId.InvalidElementId ? mra.TagId : mra.Id;
                                        foreach (Reference reference in dim.References)
                                        {
                                            ElementId referencedId = reference.ElementId;
                                            if (referencedId != ElementId.InvalidElementId)
                                            {
                                                Element taggedElem = doc.GetElement(referencedId);
                                                if (taggedElem != null)
                                                {
                                                    string uniqueId = taggedElem.UniqueId;
                                                    if (!rebarTagElements.ContainsKey(uniqueId))
                                                    {
                                                        rebarTagElements[uniqueId] = new HashSet<ElementId>();
                                                    }
                                                    rebarTagElements[uniqueId].Add(tagIdToRegister);
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // Safe catch
                                    }
                                }
                            }
                        }
                    }

                    // 4. Construct list of RebarModel
                    List<RebarModel> list = new List<RebarModel>();
                    int no = 1;
                    foreach (Element rebar in rebars)
                    {
                        // WH_Rebar_Type parameter (check instance first, then type)
                        string whRebarType = "";
                        Parameter param = rebar.LookupParameter("WH_Rebar_Type");
                        if (param == null)
                        {
                            Element typeElem = doc.GetElement(rebar.GetTypeId());
                            if (typeElem != null)
                                param = typeElem.LookupParameter("WH_Rebar_Type");
                        }
                        if (param != null)
                        {
                            whRebarType = param.AsString() ?? param.AsValueString() ?? "";
                        }

                        // WH_Rebar_Prefix parameter (check instance first, then type)
                        string whRebarPrefix = "";
                        Parameter prefixParam = rebar.LookupParameter("WH_Rebar_Prefix");
                        if (prefixParam == null)
                        {
                            Element typeElem = doc.GetElement(rebar.GetTypeId());
                            if (typeElem != null)
                                prefixParam = typeElem.LookupParameter("WH_Rebar_Prefix");
                        }
                        if (prefixParam != null)
                        {
                            whRebarPrefix = prefixParam.AsString() ?? prefixParam.AsValueString() ?? "";
                        }

                        // Rebar Number built-in parameter
                        Parameter rebarNumParam = rebar.get_Parameter(BuiltInParameter.REBAR_NUMBER);
                        string rebarNumber = rebarNumParam?.AsString() ?? rebarNumParam?.AsValueString() ?? "";

                        // Rebar Type name
                        string typeName = "";
                        ElementId typeId = rebar.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            Element typeElem = doc.GetElement(typeId);
                            if (typeElem != null)
                                typeName = typeElem.Name;
                        }

                        int tagCount = 0;
                        if (rebarTagElements.TryGetValue(rebar.UniqueId, out var tagSet))
                        {
                            tagCount = tagSet.Count;
                        }

                        list.Add(new RebarModel
                        {
                            No = no++,
                            Id = rebar.UniqueId,
                            WhRebarType = whRebarType,
                            WhRebarPrefix = whRebarPrefix,
                            RebarNumber = rebarNumber,
                            TypeName = typeName,
                            TagCount = tagCount
                        });
                    }

                    // Sort rebars (e.g. by WhRebarType then RebarNumber) for better default display
                    var sortedList = list.OrderBy(x => x.WhRebarType)
                                         .ThenBy(x => x.RebarNumber)
                                         .ToList();

                    // Re-index No after sorting
                    for (int i = 0; i < sortedList.Count; i++)
                    {
                        sortedList[i].No = i + 1;
                    }

                    // Push results back to WPF window thread
                    _window.Dispatcher.Invoke(() =>
                    {
                        _window.UpdateRebarList(sortedList);
                    });
                }
                else if (RequestType == RebarActionType.Select && !string.IsNullOrEmpty(SelectedRebarUniqueId))
                {
                    Element selectedElem = doc.GetElement(SelectedRebarUniqueId);
                    if (selectedElem != null)
                    {
                        // Select element in UI
                        uidoc.Selection.SetElementIds(new List<ElementId> { selectedElem.Id });
                        // Zoom to show elements
                        uidoc.ShowElements(selectedElem.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Rebar Checker Error", "Lỗi thực thi lệnh: " + ex.Message);
            }
        }

        public string GetName() => "RebarScheduleEventHandler";
    }
}
