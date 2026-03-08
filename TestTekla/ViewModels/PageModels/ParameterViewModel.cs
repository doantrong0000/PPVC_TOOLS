using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TeklaApp.Models;

namespace TeklaApp.ViewModels
{
    public class ParameterViewModel : INotifyPropertyChanged
    {
        private TeklaModelMng _teklaModel;

        // Area 1 Properties (Source)
        private string _field1_1;
        private string _field2_1;
        private string _field3_1;
        private string _field4_1;
        private string _object1Name = "Not Selected";
        private ModelObject _modelObject1;

        // Area 2 Properties (Target)
        private string _field1_2;
        private string _field2_2;
        private string _field3_2;
        private string _field4_2;
        private string _object2Name = "Not Selected";
        private ModelObject _modelObject2;

        public string Field1_1 { get => _field1_1; set { _field1_1 = value; OnPropertyChanged(); } }
        public string Field2_1 { get => _field2_1; set { _field2_1 = value; OnPropertyChanged(); } }
        public string Field3_1 { get => _field3_1; set { _field3_1 = value; OnPropertyChanged(); } }
        public string Field4_1 { get => _field4_1; set { _field4_1 = value; OnPropertyChanged(); } }
        public string Object1Name { get => _object1Name; set { _object1Name = value; OnPropertyChanged(); } }

        public string Field1_2 { get => _field1_2; set { _field1_2 = value; OnPropertyChanged(); } }
        public string Field2_2 { get => _field2_2; set { _field2_2 = value; OnPropertyChanged(); } }
        public string Field3_2 { get => _field3_2; set { _field3_2 = value; OnPropertyChanged(); } }
        public string Field4_2 { get => _field4_2; set { _field4_2 = value; OnPropertyChanged(); } }
        public string Object2Name { get => _object2Name; set { _object2Name = value; OnPropertyChanged(); } }

        private string _statusMessage;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        public ParameterViewModel()
        {
            _teklaModel = new TeklaModelMng();
        }

        public string PickObject(int areaIndex)
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Picker picker = new Picker();
                ModelObject pickedObject = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_OBJECT, "Select an object to read parameters");

                if (pickedObject is Part part)
                {
                    string f1 = "", f2 = "", f3 = "", f4 = "";
                    part.GetUserProperty("USER_FIELD_1", ref f1);
                    part.GetUserProperty("USER_FIELD_2", ref f2);
                    part.GetUserProperty("USER_FIELD_3", ref f3);
                    part.GetUserProperty("USER_FIELD_4", ref f4);

                    if (areaIndex == 1)
                    {
                        _modelObject1 = part;
                        Object1Name = $"{part.Name} ({part.Identifier.ID})";
                        Field1_1 = f1; Field2_1 = f2; Field3_1 = f3; Field4_1 = f4;
                    }
                    else
                    {
                        _modelObject2 = part;
                        Object2Name = $"{part.Name} ({part.Identifier.ID})";
                        Field1_2 = f1; Field2_2 = f2; Field3_2 = f3; Field4_2 = f4;
                    }
                    return "Success: Parameters loaded.";
                }
                return "Selected object is not a Part.";
            }
            catch (Exception ex)
            {
                return "Selection cancelled or error: " + ex.Message;
            }
        }

        public void CopyField(int fieldIndex)
        {
            switch (fieldIndex)
            {
                case 1: Field1_2 = Field1_1; break;
                case 2: Field2_2 = Field2_1; break;
                case 3: Field3_2 = Field3_1; break;
                case 4: Field4_2 = Field4_1; break;
            }
        }

        public void CopyAll()
        {
            Field1_2 = Field1_1;
            Field2_2 = Field2_1;
            Field3_2 = Field3_1;
            Field4_2 = Field4_1;
        }

        public string SaveToTarget()
        {
            if (_modelObject2 == null) return "No target object selected.";
            if (!(_modelObject2 is Part part)) return "Target is not a Part.";

            part.SetUserProperty("USER_FIELD_1", Field1_2 ?? "");
            part.SetUserProperty("USER_FIELD_2", Field2_2 ?? "");
            part.SetUserProperty("USER_FIELD_3", Field3_2 ?? "");
            part.SetUserProperty("USER_FIELD_4", Field4_2 ?? "");
            part.Modify();
            _teklaModel.Commit();
            return "Success: Parameters saved to target object.";
        }

        public string ApplyToAllSelected(int fieldIndex)
        {
            return ApplyGlobalInternal(fieldIndex, true);
        }

        public string ApplyToSweepSelected(int fieldIndex)
        {
            return ApplyGlobalInternal(fieldIndex, false);
        }

        private string ApplyGlobalInternal(int fieldIndex, bool currentSelection)
        {
            string valueToCopy = "";
            string fieldName = $"USER_FIELD_{fieldIndex}";

            switch (fieldIndex)
            {
                case 1: valueToCopy = Field1_1; break;
                case 2: valueToCopy = Field2_1; break;
                case 3: valueToCopy = Field3_1; break;
                case 4: valueToCopy = Field4_1; break;
            }

            if (string.IsNullOrEmpty(valueToCopy)) return "Source field is empty.";

            Model model = _teklaModel.GetModel();
            List<Part> targets = new List<Part>();

            if (currentSelection)
            {
                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
                while (selector.MoveNext())
                {
                    if (selector.Current is Part p) targets.Add(p);
                }
                if (targets.Count == 0) return "No objects selected in Tekla.";
            }
            else
            {
                try
                {
                    Picker picker = new Picker();
                    var pickedObjects = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_OBJECTS, $"Sweep select target objects for {fieldName}");
                    while (pickedObjects.MoveNext())
                    {
                        if (pickedObjects.Current is Part p) targets.Add(p);
                    }
                }
                catch { return "Selection cancelled."; }
            }

            int count = 0;
            foreach (var part in targets)
            {
                part.SetUserProperty(fieldName, valueToCopy);
                part.Modify();
                count++;
            }
            model.CommitChanges();
            return $"Success: Applied to {count} objects.";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

