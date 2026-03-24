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
                List<ModelObject> targetObjects = new List<ModelObject>();
                
                // 1. Check for PRE-SELECTION
                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();
                
                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is Part || selectedObjects.Current is Reinforcement)
                    {
                        targetObjects.Add(selectedObjects.Current);
                    }
                }

                // 2. If nothing selected, then PICK manually (Multiple)
                if (targetObjects.Count == 0)
                {
                    status = "Picking objects...";
                    ModelObjectEnumerator pickedEnum = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS, "Select parts or rebars to inspect (Esc to finish)");
                    while (pickedEnum.MoveNext())
                    {
                        targetObjects.Add(pickedEnum.Current);
                    }
                }

                if (targetObjects.Count == 0)
                {
                    status = "No objects selected.";
                    return results;
                }

                // Update UI header with first object name or count
                if (targetObjects.Count == 1)
                {
                    var first = targetObjects[0];
                    SelectedObjectName = (first is Part p) ? (string.IsNullOrEmpty(p.Name) ? p.Profile.ProfileString : p.Name) : "Selected Rebar";
                }
                else
                {
                    SelectedObjectName = $"{targetObjects.Count} Objects Selected";
                }

                Dictionary<string, Reinforcement> uniqueRebars = new Dictionary<string, Reinforcement>();
                Dictionary<string, string> rebarToHostMap = new Dictionary<string, string>();

                foreach (var obj in targetObjects)
                {
                    if (obj is Part part)
                    {
                        ModelObjectEnumerator children = part.GetChildren();
                        while (children.MoveNext())
                        {
                            if (children.Current is Reinforcement rebar)
                            {
                                string id = rebar.Identifier.ID.ToString();
                                if (!uniqueRebars.ContainsKey(id))
                                {
                                    uniqueRebars[id] = rebar;
                                    rebarToHostMap[id] = string.IsNullOrEmpty(part.Name) ? part.Profile.ProfileString : part.Name;
                                }
                            }
                        }
                    }
                    else if (obj is Reinforcement rebar)
                    {
                        string id = rebar.Identifier.ID.ToString();
                        if (!uniqueRebars.ContainsKey(id))
                        {
                            uniqueRebars[id] = rebar;
                            
                            // Try to find host part for direct rebar selection using Report Property
                            string hostName = "Unknown";
                            rebar.GetReportProperty("MAIN_PART.NAME", ref hostName);
                            if (string.IsNullOrEmpty(hostName)) 
                                rebar.GetReportProperty("FATHER.NAME", ref hostName);
                            
                            rebarToHostMap[id] = string.IsNullOrEmpty(hostName) ? "Unknown" : hostName;
                        }
                    }
                }

                foreach (var kvp in uniqueRebars)
                {
                    var r = kvp.Value;
                    string id = kvp.Key;
                    
                    string name = ""; r.GetReportProperty("NAME", ref name);
                    string size = ""; r.GetReportProperty("SIZE", ref size);
                    string grade = ""; r.GetReportProperty("GRADE", ref grade);
                    string pos = ""; r.GetReportProperty("REBAR_POS", ref pos);
                    string spacing = "---";
                    int qty = 1;
                    
                    if (r is RebarGroup group) 
                    {
                        double dQty = 0;
                        group.GetReportProperty("NUMBER", ref dQty);
                        qty = (int)dQty;

                        if (group.Spacings != null && group.Spacings.Count > 0)
                        {
                            var spacingList = group.Spacings.Cast<double>().Where(s => s > 0).Select(s => Math.Round(s, 0)).ToList();
                            if (spacingList.Count > 0)
                                spacing = spacingList.GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key.ToString();
                        }
                    }

                    results.Add(new RebarInfoItem 
                    { 
                        Name = name, 
                        Size = size, 
                        Grade = grade, 
                        Position = pos, 
                        Quantity = qty, 
                        Id = id,
                        TargetSpacing = spacing,
                        HostName = rebarToHostMap[id]
                    });
                }

                status = $"Showing {results.Count} rebar entries from {targetObjects.Count} objects.";
                return results;
            }
            catch (Exception) { status = "Cancelled."; return results; }
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
        public string HostName { get; set; }
    }
}
