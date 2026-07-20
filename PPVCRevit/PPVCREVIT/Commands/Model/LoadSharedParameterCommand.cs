using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace PPVCREVIT.Commands.Model
{
    [Transaction(TransactionMode.Manual)]
    public class LoadSharedParameterCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Xác định đường dẫn file Shared Parameter (Ưu tiên đường dẫn ổ đĩa chung Z, nếu không tìm thấy sẽ fallback về local)
                string sharedParamPath = @"Z:\05 Prefab\00 REVIT tools\ShareParameter\WH_Rebar_Description.txt";

                if (!File.Exists(sharedParamPath))
                {
                    string assemblyDir = Path.GetDirectoryName(typeof(LoadSharedParameterCommand).Assembly.Location);
                    sharedParamPath = Path.Combine(assemblyDir, "Parameter", "WH_Rebar_Description.txt");
                }

                if (!File.Exists(sharedParamPath))
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy file Shared Parameter tại cả hai đường dẫn:\n" +
                                           "- Đường dẫn mạng: Z:\\05 Prefab\\00 REVIT tools\\ShareParameter\\WH_Rebar_Description.txt\n" +
                                           $"- Đường dẫn local: {sharedParamPath}");
                    return Result.Failed;
                }

                // 2. Mở file Shared Parameter
                string originalSharedParamFile = doc.Application.SharedParametersFilename;
                doc.Application.SharedParametersFilename = sharedParamPath;

                DefinitionFile spFile = null;
                DefinitionGroup group = null;
                try
                {
                    spFile = doc.Application.OpenSharedParameterFile();
                    if (spFile == null)
                    {
                        TaskDialog.Show("Lỗi", "Không thể mở file Shared Parameter.");
                        return Result.Failed;
                    }

                    // Tìm Group "WH_Rebar_Description"
                    group = spFile.Groups.get_Item("WH_Rebar_Description");
                }
                finally
                {
                    // Trả lại đường dẫn file shared parameter cũ
                    doc.Application.SharedParametersFilename = originalSharedParamFile;
                }

                if (group == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy Group 'WH_Rebar_Description' trong file Shared Parameter.");
                    return Result.Failed;
                }

                // Danh sách parameter cần load
                List<string> paramsToLoad = new List<string> { "WH_Rebar_Type", "WH_Rebar_Prefix" };
                List<string> loadedParams = new List<string>();
                List<string> skippedParams = new List<string>();

                using (Transaction tx = new Transaction(doc, "Load Shared Parameters"))
                {
                    tx.Start();

                    // Chuẩn bị Category Set cho Structural Rebar (OST_Rebar)
                    CategorySet catSet = doc.Application.Create.NewCategorySet();
                    Category rebarCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rebar);
                    if (rebarCat != null)
                    {
                        catSet.Insert(rebarCat);
                    }
                    else
                    {
                        TaskDialog.Show("Lỗi", "Không tìm thấy Category Structural Rebar (OST_Rebar) trong dự án.");
                        return Result.Failed;
                    }

                    Binding binding = doc.Application.Create.NewInstanceBinding(catSet);

                    foreach (string paramName in paramsToLoad)
                    {
                        Definition def = group.Definitions.get_Item(paramName);
                        if (def == null)
                        {
                            skippedParams.Add($"{paramName} (Không tìm thấy định nghĩa)");
                            continue;
                        }

                        // Kiểm tra xem parameter đã được bind chưa
                        bool exists = doc.ParameterBindings.Contains(def);
                        if (exists)
                        {
                            // Nếu đã tồn tại, dùng ReInsert để cập nhật (đề phòng đổi category hoặc cấu hình)
                            doc.ParameterBindings.ReInsert(def, binding, GroupTypeId.Text);
                            skippedParams.Add($"{paramName} (Đã cập nhật liên kết)");
                        }
                        else
                        {
                            // Nếu chưa tồn tại, chèn mới
                            bool bound = doc.ParameterBindings.Insert(def, binding, GroupTypeId.Text);
                            if (bound)
                            {
                                loadedParams.Add(paramName);
                            }
                            else
                            {
                                skippedParams.Add($"{paramName} (Thất bại khi bind)");
                            }
                        }
                    }

                    tx.Commit();
                }

                string msg = "";
                if (loadedParams.Count > 0)
                {
                    msg += $"Đã load thành công: {string.Join(", ", loadedParams)}\n";
                }
                if (skippedParams.Count > 0)
                {
                    msg += $"Thông tin khác: \n- {string.Join("\n- ", skippedParams)}";
                }

                TaskDialog.Show("Kết Quả", msg);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Hệ Thống", ex.ToString());
                return Result.Failed;
            }
        }
    }
}
