using System;
using Tekla.Structures.Model;
using TeklaApp.Models;

namespace TeklaApp.ViewModels
{
    public class MainViewModel
    {
        private TeklaModelMng _teklaModel;

        public MainViewModel()
        {
            _teklaModel = new TeklaModelMng();
        }


        public string DeletePartCuts()
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Please select a part to delete PartCuts");

                if (pickedObject is Part hostPart)
                {
                    ModelObjectEnumerator cutEnumerator = hostPart.GetBooleans();
                    int cutCount = 0;

                    while (cutEnumerator.MoveNext())
                    {
                        if (cutEnumerator.Current is BooleanPart booleanCut)
                        {
                            cutCount++;
                            booleanCut.Delete();
                        }
                    }

                    if (cutCount > 0)
                    {
                        _teklaModel.Commit();
                        return $"Successfully deleted {cutCount} PartCuts.";
                    }
                    else
                    {
                        return "This part has no PartCuts.";
                    }
                }
                else
                {
                    return "Invalid object selected.";
                }
            }
            catch (Exception ex)
            {
                return "Cancelled or error occurred: " + ex.Message;
            }
        }

        public string JoinAssembly()
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();

                ModelObject mainObj = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Please select the main part...");
                if (mainObj is Part mainPart)
                {
                    ModelObjectEnumerator secondaryObjects = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_PARTS, "Sweep select secondary parts and press MIDDLE mouse button to finish...");

                    Tekla.Structures.Model.Assembly assembly = mainPart.GetAssembly();
                    int count = 0;

                    while (secondaryObjects.MoveNext())
                    {
                        if (secondaryObjects.Current is Part secPart && secPart.Identifier.ID != mainPart.Identifier.ID)
                        {
                            assembly.Add(secPart);
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        assembly.Modify();
                        _teklaModel.Commit();
                        return $"Done!\r\nSuccessfully added {count} secondary parts to the Assembly/CastUnit of the main part (Profile: {mainPart.Profile.ProfileString}).";
                    }
                    else
                    {
                        return "No valid secondary parts were selected.";
                    }
                }
                else
                {
                    return "Selected main object is invalid (Not a Part).";
                }
            }
            catch (Exception ex)
            {
                return "Cancelled or error occurred: " + ex.Message;
            }
        }
        public string RemoveDuplicateCuts()
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Please select a part to remove duplicate PartCuts");

                if (pickedObject is Part hostPart)
                {
                    ModelObjectEnumerator cutEnumerator = hostPart.GetBooleans();
                    var existingCuts = new System.Collections.Generic.Dictionary<string, BooleanPart>();
                    int deletedCount = 0;
                    int totalCuts = 0;

                    while (cutEnumerator.MoveNext())
                    {
                        if (cutEnumerator.Current is BooleanPart booleanCut)
                        {
                            totalCuts++;
                            Part cuttingPart = booleanCut.OperativePart;
                            if (cuttingPart == null) continue;

                            double volume = 0;
                            cuttingPart.GetReportProperty("VOLUME", ref volume);

                            // Lấy tâm khối của Solid để xác định vị trí
                            var solid = booleanCut.OperativePart.GetSolid();
                            double midX = Math.Round((solid.MinimumPoint.X + solid.MaximumPoint.X) / 2.0, 1);
                            double midY = Math.Round((solid.MinimumPoint.Y + solid.MaximumPoint.Y) / 2.0, 1);
                            double midZ = Math.Round((solid.MinimumPoint.Z + solid.MaximumPoint.Z) / 2.0, 1);

                            // Tạo chữ ký định danh cho vết cắt: Thể tích + Tọa độ tâm
                            string signature = $"{Math.Round(volume, 2)}_{midX}_{midY}_{midZ}";

                            if (existingCuts.ContainsKey(signature))
                            {
                                // Nếu đã tồn tại vết cắt y hệt -> Xóa cái này đi
                                booleanCut.Delete();
                                deletedCount++;
                            }
                            else
                            {
                                existingCuts.Add(signature, booleanCut);
                            }
                        }
                    }

                    if (deletedCount > 0)
                    {
                        _teklaModel.Commit();
                        return $"Success!\r\n- Initial total cuts: {totalCuts}\r\n- Deleted {deletedCount} duplicate cuts.\r\n- Kept {existingCuts.Count} unique cuts.";
                    }
                    else
                    {
                        return $"No duplicate cuts found on this part (Total: {totalCuts}).";
                    }
                }
                else
                {
                    return "Invalid object selected.";
                }
            }
            catch (Exception ex)
            {
                return "Error processing duplicates: " + ex.Message;
            }
        }
    }
}
