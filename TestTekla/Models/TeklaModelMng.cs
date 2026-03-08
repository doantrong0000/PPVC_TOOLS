using System;
using Tekla.Structures.Model;

namespace TeklaApp.Models
{
    public class TeklaModelMng
    {
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
            return _myModel.GetConnectionStatus();
        }

        public void Commit()
        {
            _myModel.CommitChanges();
        }
    }
}
