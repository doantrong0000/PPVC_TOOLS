using Autodesk.Revit.UI;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Models;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Utils;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.ViewModels
{
    public class PPVCViewModel
    {
        private PPVCView view;
        private ExternalEvent _externalEvent;
        private PPVCEventHandler _eventHandler;

        public PPVCView View { get { return view; } }

        public string LevelParameter { get; set; } = "L2";
        public string PPVCParameter { get; set; } = "PPVC05a";
        public string ProjectParameter { get; set; } = "ZRA";

        public ICommand CreateViewCommand { get; set; }
        public ICommand CreateDimTagForBaseSlab { get; set; }
        public ICommand CreateDimTagForMillWall { get; set; }
        public ICommand CreateDimTagForRoofPlan { get; set; }
        public ICommand CreateRebarTagForSlabCommand { get; set; }

        public PPVCViewModel(PPVCView view, ExternalEvent externalEvent, PPVCEventHandler eventHandler)
        {
            this.view = view;
            this._externalEvent = externalEvent;
            this._eventHandler = eventHandler;
            CreateViewCommand = new RelayCommand(CreateView);
            CreateDimTagForBaseSlab = new RelayCommand(CreateDetailForBaseSlab);
            CreateDimTagForMillWall = new RelayCommand(CreateDetailForMillWall);
            CreateDimTagForRoofPlan = new RelayCommand(CreateDetailForRoofPlan);
            CreateRebarTagForSlabCommand = new RelayCommand(CreateRebarTagForSlabAction);
        }

        public void CreateView(object obj)
        {
            if (_externalEvent != null)
            {
                _eventHandler.SetAction(app =>
                {
                    CreateSectionModel.CreateAllViewForPPVC(PPVCParameter, ProjectParameter, LevelParameter);
                });
                _externalEvent.Raise();
            }
        }

        public void CreateDetailForBaseSlab(object obj)
        {
            if (_externalEvent != null)
            {
                _eventHandler.SetAction(app =>
                {
                    CreateTagDimForBaseSlabModel.CreateTagDimForBaseSlab();
                });
                _externalEvent.Raise();
            }
        }

        public void CreateDetailForMillWall(object obj)
        {
            if (_externalEvent != null)
            {
                _eventHandler.SetAction(app =>
                {
                    CreateTagDimForMidWallModel.CreateTagDimForMidWall();
                });
                _externalEvent.Raise();
            }
        }

        public void CreateDetailForRoofPlan(object obj)
        {
            if (_externalEvent != null)
            {
                _eventHandler.SetAction(app =>
                {
                    CreateTagDimForRoofPlanModel.CreateTagDimForRoofPlan();
                });
                _externalEvent.Raise();
            }
        }

        public void CreateRebarTagForSlabAction(object obj)
        {
            if (_externalEvent != null)
            {
                _eventHandler.SetAction(app =>
                {
                    CreateSlabRebarTagModel.CreateRebarTagForSlab("SLAB Bottom bars", "Type 3");
                });
                _externalEvent.Raise();
            }
        }
    }
}
