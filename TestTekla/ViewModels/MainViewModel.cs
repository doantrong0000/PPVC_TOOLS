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

    }
}
