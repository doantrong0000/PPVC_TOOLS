using Fusion;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Linq;
using Tekla.Structures;
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
            DrawingHandler dh = new DrawingHandler();
            Tekla.Structures.Drawing.Drawing activeDrawing = dh.GetActiveDrawing();
            if (activeDrawing == null) { status = "No active drawing"; return; }

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

                        // Quét tất cả Mark trong View
                        DrawingObjectEnumerator markEnum = view.GetObjects(new Type[] { typeof(Mark) });
                        while (markEnum.MoveNext())
                        {
                            if (markEnum.Current is Mark mark)
                            {
                                // Lấy đối tượng Model mà Mark này trỏ tới
                                DrawingObjectEnumerator relatedObjects = mark.GetRelatedObjects();
                                while (relatedObjects.MoveNext())
                                {
                                    if (relatedObjects.Current is ReinforcementBase dRebar)
                                    {
                                        string textContent = GetRebarMarkContent(mark, model);
                                        ProcessRebarWithTag(rebarDict, model, dRebar.ModelIdentifier, viewName, "Mark", textContent);
                                    }
                                }
                            }
                        }

                        // Xử lý Rebar Dimension Mark (thường dùng cho nhóm thép)
                        DrawingObjectEnumerator allObjEnum = view.GetObjects();
                        while (allObjEnum.MoveNext())
                        {
                            // Kiểm tra bằng tên Type nếu không muốn dùng thư viện Internal
                            if (allObjEnum.Current.GetType().Name.Contains("RebarDimensionMark"))
                            {
                                var dimMark = allObjEnum.Current;
                                // Dùng Reflection an toàn để gọi GetRelatedObjects
                                var method = dimMark.GetType().GetMethod("GetRelatedObjects", Type.EmptyTypes);
                                if (method != null)
                                {
                                    var rEnum = (DrawingObjectEnumerator)method.Invoke(dimMark, null);
                                    while (rEnum != null && rEnum.MoveNext())
                                    {
                                        if (rEnum.Current is ReinforcementBase dRebar)
                                        {
                                            string textContent = GetRebarMarkContent(dimMark, model);
                                            ProcessRebarWithTag(rebarDict, model, dRebar.ModelIdentifier, viewName, "DimMark", textContent);
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
                    item.TagSummary = string.Join(", ", item.Tags.Select(t => t.TagContent));
                    ScannedData.Add(item);
                }

                status = $"Scanned {rebarDict.Count} rebar groups.";
            }
            catch (Exception ex) { status = "Error: " + ex.Message; }
        }

        private string GetRebarMarkContent(DrawingObject dobj, Tekla.Structures.Model.Model model)
        {
            // Kiểm tra xem đối tượng có phải là Mark không
            if (dobj is Tekla.Structures.Drawing.Mark mark)
            {
                List<string> parts = new List<string>();

                // Lấy đối tượng thép từ Model để tra cứu giá trị thực tế (Pos, Grade, Size...)
                Tekla.Structures.Model.ModelObject mObj = null;
                var relatedEnum = mark.GetRelatedObjects();
                if (relatedEnum.MoveNext() && relatedEnum.Current is ReinforcementBase dRebar)
                {
                    mObj = model.SelectModelObject(dRebar.ModelIdentifier);
                }

                // Duyệt qua các thành phần (Elements) bên trong Tag
                // mark.Attributes.Content là cách gọi "chính quy" nhất, không bao giờ lỗi Ambiguous
                foreach (var element in mark.Attributes.Content)
                {
                    if (element is Tekla.Structures.Drawing.TextElement textElem)
                    {
                        parts.Add(textElem.GetUnformattedString());
                    }
                    else if (element is PropertyElement propElem && mObj != null)
                    {
                        string val = "";
                        // Tra cứu thuộc tính từ Model (ví dụ: "REBAR_POS", "GRADE")
                        if (mObj.GetReportProperty(propElem.Name, ref val) && !string.IsNullOrEmpty(val))
                        {
                            parts.Add(val);
                        }
                        else
                        {
                            double dVal = 0;
                            if (mObj.GetReportProperty(propElem.Name, ref dVal))
                                parts.Add(dVal.ToString("0.##"));
                        }
                    }
                }

                return string.Join(" ", parts).Trim();
            }

            return string.Empty;
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
