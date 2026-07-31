using Autodesk.Revit.UI;
using System;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC
{
    public class PPVCEventHandler : IExternalEventHandler
    {
        private Action<UIApplication> _action;

        public void SetAction(Action<UIApplication> action)
        {
            _action = action;
        }

        public void Execute(UIApplication app)
        {
            if (_action != null)
            {
                try
                {
                    _action(app);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("PPVC Error", "Lỗi thực thi Revit API: " + ex.Message);
                }
                finally
                {
                    _action = null;
                }
            }
        }

        // Đã sửa lại hàm này
        public string GetName()
        {
            return "PPVC Event Handler";
        }
    }
}