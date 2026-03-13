using System;
using Tekla.Structures.Model;

namespace TeklaApp.Models
{
    public class TeklaModelMng
    {
        public static string LastError { get; private set; } = "No connection attempted.";
        private Model _myModel;

        public TeklaModelMng()
        {
            _myModel = new Model();
        }

        public Model GetModel()
        {
            return _myModel;
        }

        public bool IsConnected()
        {
            try
            {
                if (_myModel.GetConnectionStatus())
                {
                    LastError = "Connected successfully.";
                    return true;
                }
                else
                {
                    LastError = "Tekla Structures model is not open or not reachable. Make sure a model is open in Tekla.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Connection Error: {ex.Message}";
                return false;
            }
        }

        public void Commit()
        {
            if (IsConnected())
                _myModel.CommitChanges();
        }
    }
}
