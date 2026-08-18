using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

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


#if RELEASE
            MessageBox.Show("Tính năng này Trọng chưa cho chạy");
            return Result.Succeeded;
#else
            try
            {
                // 1. Xác định đường dẫn file Shared Parameter (Ưu tiên đường dẫn ổ đĩa chung Z, nếu không tìm thấy sẽ fallback về local)
                string sharedParamPath = @"C:\Users\doan_ductrong\Desktop\PPVCTest\WH_Rebar_Description.txt";

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

                var configs = new List<ParameterBindingConfig>
                {
                    new ParameterBindingConfig
                    {
                        GroupName = "WH_Rebar",
                        ParameterNames = new List<string> { "WH_Rebar_Type", "WH_Rebar_Prefix" },
                        TargetCategory = BuiltInCategory.OST_Rebar,
                        GroupType = GroupTypeId.Text
                    },
                     new ParameterBindingConfig
                    {
                        GroupName = "WH_Rebar",
                        ParameterNames = new List<string> { "WH_Rebar_Dimension_BarLength"},
                        TargetCategory = BuiltInCategory.OST_Rebar,
                        GroupType = GroupTypeId.Geometry
                    },
                    new ParameterBindingConfig
                    {
                        GroupName = "WH_View",
                        ParameterNames = new List<string> { "PPVC", "Project", "Level", "SHEET" },
                        TargetCategory = BuiltInCategory.OST_Views,
                        GroupType = GroupTypeId.IdentityData
                    },
                    new ParameterBindingConfig
                    {
                        GroupName = "WH_Sheet",
                        ParameterNames = new List<string> { "PPVC_SHEET", "Project_SHEET", "Level_Sheet" },
                        TargetCategory = BuiltInCategory.OST_Sheets,
                        GroupType = GroupTypeId.IdentityData
                    }
                };

                List<string> loadedParams = new List<string>();
                List<string> skippedParams = new List<string>();
                var groupDefinitions = new Dictionary<string, List<Definition>>();

                try
                {
                    DefinitionFile spFile = doc.Application.OpenSharedParameterFile();
                    if (spFile == null)
                    {
                        TaskDialog.Show("Lỗi", "Không thể mở file Shared Parameter.");
                        return Result.Failed;
                    }

                    foreach (var config in configs)
                    {
                        DefinitionGroup group = spFile.Groups.get_Item(config.GroupName);
                        if (group == null)
                        {
                            skippedParams.Add($"Group '{config.GroupName}' (Không tìm thấy group)");
                            continue;
                        }

                        var defs = new List<Definition>();
                        foreach (string paramName in config.ParameterNames)
                        {
                            Definition def = group.Definitions.get_Item(paramName);
                            if (def != null)
                            {
                                defs.Add(def);
                            }
                            else
                            {
                                skippedParams.Add($"{paramName} (Không tìm thấy định nghĩa trong group {config.GroupName})");
                            }
                        }
                        groupDefinitions[config.GroupName] = defs;
                    }
                }
                finally
                {
                    // Trả lại đường dẫn file shared parameter cũ
                    doc.Application.SharedParametersFilename = originalSharedParamFile;
                }

                using (Transaction tx = new Transaction(doc, "Load Shared Parameters"))
                {
                    tx.Start();

                    foreach (var config in configs)
                    {
                        if (!groupDefinitions.TryGetValue(config.GroupName, out var definitions))
                            continue;

                        // Chuẩn bị Category Set cho Category đích
                        CategorySet catSet = doc.Application.Create.NewCategorySet();
                        Category targetCat = doc.Settings.Categories.get_Item(config.TargetCategory);
                        if (targetCat == null)
                        {
                            skippedParams.Add($"Category {config.TargetCategory} (Không tìm thấy category trong dự án)");
                            continue;
                        }
                        catSet.Insert(targetCat);

                        Binding binding = doc.Application.Create.NewInstanceBinding(catSet);

                        foreach (Definition def in definitions)
                        {
                            // Kiểm tra xem parameter đã được bind chưa
                            bool exists = doc.ParameterBindings.Contains(def);
                            if (exists)
                            {
                                // Nếu đã tồn tại, dùng ReInsert để cập nhật (đề phòng đổi category hoặc cấu hình)
                                doc.ParameterBindings.ReInsert(def, binding, config.GroupType);
                                skippedParams.Add($"{def.Name} (Đã cập nhật liên kết)");
                            }
                            else
                            {
                                // Nếu chưa tồn tại, chèn mới
                                bool bound = doc.ParameterBindings.Insert(def, binding, config.GroupType);
                                if (bound)
                                {
                                    loadedParams.Add(def.Name);
                                }
                                else
                                {
                                    skippedParams.Add($"{def.Name} (Thất bại khi bind)");
                                }
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
#endif

        }

        private class ParameterBindingConfig
        {
            public string GroupName { get; set; }
            public List<string> ParameterNames { get; set; }
            public BuiltInCategory TargetCategory { get; set; }
            public ForgeTypeId GroupType { get; set; }
        }
    }
}
