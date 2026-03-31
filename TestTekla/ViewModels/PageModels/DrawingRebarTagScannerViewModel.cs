using Fusion;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;
using Tekla.Structures.Model;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace TeklaApp.ViewModels.PageModels
{
    public class DrawingRebarTagScannerViewModel
    {
        public ObservableCollection<ScannedRebarItem> ScannedData { get; set; } = new ObservableCollection<ScannedRebarItem>();


        public void ScanActiveDrawing(out string status)
        {
            ScannedData.Clear();
            status = "Ready";

            DrawingHandler dh = new DrawingHandler();
            Tekla.Structures.Drawing.Drawing activeDrawing = dh.GetActiveDrawing();
            if (activeDrawing == null) return;

            try
            {
                Dictionary<string, ScannedRebarItem> rebarDict = new Dictionary<string, ScannedRebarItem>();
                Tekla.Structures.Model.Model model = new Tekla.Structures.Model.Model();

                DrawingObjectEnumerator viewEnum = activeDrawing.GetSheet().GetViews();
                while (viewEnum.MoveNext())
                {
                    if (viewEnum.Current is Tekla.Structures.Drawing.View view)
                    {
                        string viewName = string.IsNullOrEmpty(view.Name) ? "Unnamed View" : view.Name;

                        // 1. Xử lý Mark
                        DrawingObjectEnumerator markEnum = view.GetObjects(new Type[] { typeof(Tekla.Structures.Drawing.Mark) });
                        while (markEnum.MoveNext())
                        {
                            if (markEnum.Current is Tekla.Structures.Drawing.Mark mark)
                            {
                                string dobjId = mark.GetIdentifier().ID.ToString();

                                // Kiểm tra mark này có liên kết với thép (ReinforcementBase) không?
                                DrawingObjectEnumerator relatedEnum = null;
                                try
                                {
                                    // Cách lấy đối tượng liên quan (Model object mà Mark trỏ tới)
                                    var method = mark.GetType().GetMethod("GetRelatedObjects", Type.EmptyTypes);
                                    if (method != null)
                                    {
                                        relatedEnum = (DrawingObjectEnumerator)method.Invoke(mark, null);
                                    }
                                    else
                                    {
                                        relatedEnum = mark.GetRelatedObjects(new Type[] { typeof(ReinforcementBase) });
                                    }
                                }
                                catch { }

                                if (relatedEnum != null)
                                {
                                    while (relatedEnum.MoveNext())
                                    {
                                        if (relatedEnum.Current is ReinforcementBase dRebar)
                                        {
                                            // Lấy ModelIdentifier của cây Thép chứa Mark
                                            ProcessRebarWithTag(rebarDict, model, dRebar.ModelIdentifier, viewName, "Mark", "Mark_ID:" + dobjId);
                                        }
                                    }
                                }
                            }
                        }

                        // 2. Xử lý các đối tượng khác (Dimension...)
                        DrawingObjectEnumerator objEnum = view.GetObjects();
                        while (objEnum.MoveNext())
                        {
                            var dobj = objEnum.Current;
                            if (dobj == null) continue;

                            string dobjId = dobj.GetIdentifier().ID.ToString();

                            if (dobj.GetType().Name.Contains("RebarDimensionMark"))
                            {
                                var method = dobj.GetType().GetMethod("GetRelatedObjects");
                                if (method != null)
                                {
                                    var rEnum = (DrawingObjectEnumerator)method.Invoke(dobj, null);
                                    while (rEnum != null && rEnum.MoveNext())
                                    {
                                        if (rEnum.Current is ReinforcementBase dRebar)
                                        {
                                            ProcessRebarWithTag(rebarDict, model, dRebar.ModelIdentifier, viewName, "DimMark", "ID:" + dobjId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var item in rebarDict.Values)
                {
                    item.TagCount = item.Tags.Count;
                    item.TagSummary = string.Join(", ", item.Tags.Select(t => t.TagType + ":" + t.TagContent));
                    ScannedData.Add(item);
                }

                status = $"Scanned {rebarDict.Count} items.";
            }
            catch (Exception ex) { status = "Error: " + ex.Message; }
        }

        private void ProcessRebarWithTag(Dictionary<string, ScannedRebarItem> dict, Tekla.Structures.Model.Model model, Tekla.Structures.Identifier mId, string viewName, string type, string content)
        {
            string key = viewName + "_" + mId.ID;
            if (!dict.ContainsKey(key))
            {
                string pos = "";
                string name = "";
                var mObj = model.SelectModelObject(mId);
                if (mObj != null)
                {
                    mObj.GetReportProperty("REBAR_POS", ref pos);
                    mObj.GetReportProperty("NAME", ref name);
                }

                dict[key] = new ScannedRebarItem
                {
                    RebarId = mId.ID,
                    Name = name,
                    Position = pos,
                    ViewName = viewName,
                    Tags = new List<RebarTagData>()
                };
            }
            dict[key].Tags.Add(new RebarTagData { TagType = type, TagContent = content });
        }
        public void ExportToJson()
        {
            if (ScannedData.Count == 0) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON Files (*.json)|*.json";
            saveFileDialog.FileName = "AI_RebarTagData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var exportData = new
                    {
                        ExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Rebars = ScannedData.ToList()
                    };

                    string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    System.Windows.MessageBox.Show("Exported successful to " + saveFileDialog.FileName, "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Error exporting JSON: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    public class ScannedRebarItem
    {
        public int RebarId { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public int TagCount { get; set; }
        public string ViewName { get; set; }
        public string TagSummary { get; set; } // for grid preview

        public List<RebarTagData> Tags { get; set; }
    }

    public class RebarTagData
    {
        public string TagType { get; set; }
        public string TagContent { get; set; }
        public string ViewName { get; set; }
    }
}
