using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace TeklaApp.ViewModels
{
    public class RebarInspectorViewModel
    {
        public ObservableCollection<RebarInfoItem> Rebars { get; set; } = new ObservableCollection<RebarInfoItem>();
        public string SelectedObjectName { get; set; } = "";
        private Model _model = new Model();

        public List<RebarInfoItem> GetRebarData(out string status)
        {
            List<RebarInfoItem> results = new List<RebarInfoItem>();
            status = "Ready";
            
            if (!_model.GetConnectionStatus()) 
            {
                status = "Error: Tekla Model not connected.";
                return results;
            }

            Picker picker = new Picker();
            try
            {
                SelectedObjectName = "None";
                ModelObject pickedObject = null;

                // 1. Check for PRE-SELECTION (Rule #1)
                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();
                
                // Check if exactly one Part is selected
                if (selectedObjects != null)
                {
                    int count = 0;
                    while (selectedObjects.MoveNext())
                    {
                        count++;
                        if (count == 1)
                        {
                            if (selectedObjects.Current is Part p) pickedObject = p;
                        }
                    }
                    // Only use if exactly one was found (to match Rule #1 logic)
                    if (count != 1) pickedObject = null;
                }

                // 2. If nothing selected, then PICK manually
                if (pickedObject == null)
                {
                    pickedObject = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Select a part to see its rebars");
                }

                if (pickedObject is Part part)
                {
                    SelectedObjectName = string.IsNullOrEmpty(part.Name) ? part.Profile.ProfileString : part.Name;
                    List<Reinforcement> rebarObjects = new List<Reinforcement>();

                    // 1. Get direct children (hosted rebars)
                    ModelObjectEnumerator children = part.GetChildren();
                    while (children.MoveNext())
                    {
                        if (children.Current is Reinforcement rebar)
                        {
                            // Add only unique rebars
                            if (!rebarObjects.Any(x => x.Identifier.ID == rebar.Identifier.ID))
                                rebarObjects.Add(rebar);
                        }
                    }

                    if (rebarObjects.Count == 0) 
                    {
                        status = "No rebars found for this part.";
                        return results;
                    }

                    foreach (var r in rebarObjects)
                    {
                        string name = ""; r.GetReportProperty("NAME", ref name);
                        string size = ""; r.GetReportProperty("SIZE", ref size);
                        string grade = ""; r.GetReportProperty("GRADE", ref grade);
                        string pos = ""; r.GetReportProperty("REBAR_POS", ref pos);
                        string spacing = "---";
                        int qty = 1;
                        
                        if (r is RebarGroup group) 
                        {
                            // 1. Lấy số lượng
                            double dQty = 0;
                            group.GetReportProperty("NUMBER", ref dQty);
                            qty = (int)dQty;

                            // 2. Lấy khoảng cách xuất hiện nhiều nhất (Mode)
                            if (group.Spacings != null && group.Spacings.Count > 0)
                            {
                                // Chuyển ArrayList sang List<double>, làm tròn để dễ so sánh
                                var spacingList = group.Spacings.Cast<double>()
                                                               .Select(s => Math.Round(s, 0))
                                                               .ToList();

                                // Dùng LINQ để tìm giá trị xuất hiện nhiều nhất
                                var mostFrequentSpacing = spacingList.GroupBy(s => s)
                                                                     .OrderByDescending(g => g.Count())
                                                                     .First()
                                                                     .Key;

                                spacing = mostFrequentSpacing.ToString();
                            }
                        }

                        results.Add(new RebarInfoItem 
                        { 
                            Name = name, 
                            Size = size, 
                            Grade = grade, 
                            Position = pos, 
                            Quantity = qty, 
                            Id = r.Identifier.ID.ToString(),
                            TargetSpacing = spacing
                        });
                    }

                    status = $"Showing {results.Count} rebar entries.";
                    return results;
                }
                status = "Invalid selection.";
                return results;
            }
            catch { status = "Pick cancelled."; return results; }
        }

        public void SelectRebarInTekla(string idString)
        {
            if (int.TryParse(idString, out int id))
            {
                Tekla.Structures.Identifier identifier = new Tekla.Structures.Identifier(id);
                ModelObject modelObject = _model.SelectModelObject(identifier);
                if (modelObject != null)
                {
                    System.Collections.ArrayList objects = new System.Collections.ArrayList { modelObject };
                    Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                    selector.Select(objects);
                }
            }
        }
    }

    public class RebarInfoItem
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Grade { get; set; }
        public string Position { get; set; }
        public int Quantity { get; set; }
        public string Id { get; set; }
        public string TargetSpacing { get; set; }
    }
}
