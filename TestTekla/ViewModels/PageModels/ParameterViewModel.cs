using System;
using Tekla.Structures.Model;
using TeklaApp.Models;

namespace TeklaApp.ViewModels
{
    public class ParameterViewModel
    {
        private TeklaModelMng _teklaModel;

        public ParameterViewModel()
        {
            _teklaModel = new TeklaModelMng();
        }

        public string ReadParameters()
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_OBJECT, "Please select an object to read parameters");

                if (pickedObject is Part part)
                {
                    string info = "User Defined Attributes (UDA):\r\n\r\n";
                    info += $"- Part Name: {part.Name}\r\n";

                    string userField1 = "";
                    part.GetUserProperty("USER_FIELD_1", ref userField1);
                    info += $"- User field 1: {userField1}\r\n";

                    string userField2 = "";
                    part.GetUserProperty("USER_FIELD_2", ref userField2);
                    info += $"- User field 2: {userField2}\r\n";

                    string userField3 = "";
                    part.GetUserProperty("USER_FIELD_3", ref userField3);
                    info += $"- User field 3: {userField3}\r\n";

                    string userField4 = "";
                    part.GetUserProperty("USER_FIELD_4", ref userField4);
                    info += $"- User field 4: {userField4}\r\n";

                    string comment = "";
                    part.GetUserProperty("comment", ref comment);
                    info += $"- Comment: {comment}\r\n";

                    string prelimMark = "";
                    part.GetUserProperty("PRELIM_MARK", ref prelimMark);
                    info += $"- Preliminary mark: {prelimMark}\r\n";

                    return info;
                }
                else
                {
                    return "Selected object is not a Part. Type: " + (pickedObject != null ? pickedObject.GetType().Name : "null");
                }
            }
            catch (Exception ex)
            {
                return "Cancelled or error occurred: " + ex.Message;
            }
        }
    }
}
