using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSM = Tekla.Structures.Model;
using TS = Tekla.Structures;
using TSMUI = Tekla.Structures.Model.UI;
using Tekla.Structures.Model;

namespace REBAR_SEQUENCE_NUMBER
{
    public class Lib
    {
        public static List<TSM.Reinforcement> GetRebarSelected()
        {
            List<TSM.Reinforcement> listRebar = new List<TSM.Reinforcement>();

            TSMUI.ModelObjectSelector selected = new TSMUI.ModelObjectSelector();
            TSM.ModelObjectEnumerator enumOBJ = selected.GetSelectedObjects();
            foreach (var item in enumOBJ)
            {
                TSM.Reinforcement rein = item as TSM.Reinforcement;
                if (rein != null)
                {
                    listRebar.Add(rein);
                }
            }
            return listRebar;
        }
        public void SelectionInModel(List<TS.Identifier> ListIden)
        {
            ArrayList objs = new System.Collections.ArrayList();
            TSMUI.ModelObjectSelector selector = new TSMUI.ModelObjectSelector();
            foreach (var item in ListIden)
            {
                TSM.ModelObject obj = new TSM.Model().SelectModelObject(item);
                objs.Add(obj);
            }
            TSMUI.ModelObjectSelector selec = new TSMUI.ModelObjectSelector();
            selec.Select(objs);
        }
        public List<PropertisGroup> GrouprebarToCompare(List<ReinProperties> listRebarproperties)
        {
            List<PropertisGroup> listGroup = new List<PropertisGroup>();
            int i = 0;
            while (listRebarproperties.Count != 0)
            {
                List<ReinProperties> ListRebarPropertiesGroup = new List<ReinProperties>();
                for (int j = 0; j < listRebarproperties.Count; j++)
                {
                    if (listRebarproperties[i].rebarPos == listRebarproperties[j].rebarPos)
                    {
                        ListRebarPropertiesGroup.Add(listRebarproperties[j]);
                        if (j != 0)
                        {
                            listRebarproperties.RemoveAt(j);
                            j = j - 1;
                        }
                    }
                }
                listRebarproperties.RemoveAt(0);
                PropertisGroup propertiesGroup = new PropertisGroup(ListRebarPropertiesGroup);
                listGroup.Add(propertiesGroup);
            }
            return listGroup;
        }
        public void SetRebarSQN(List<TS.Identifier> listIdens, double SQN)
        {
            ArrayList array = new ArrayList();
            foreach (var item in listIdens)
            {
                ModelObject obj = new TSM.Model().SelectModelObject(item);
                if (obj is TSM.Reinforcement)
                {
                    obj.SetUserProperty("REBAR_SEQ_NO", SQN);

                    array.Add(obj);

                }
            }
        }
    }
    public class ReinProperties
    {
        public Reinforcement rein { get; set; }
        public string iden { get; set; }
        public string name { get; set; }
        public string rebarPos { get; set; }
        public double rebarSeque { get; set; }
        public string grade { get; set; }
        public string size { get; set; }
        public int rebarQty { get; set; }
        public double rebarLength { get; set; }
        public double rebarWeight { get; set; }
        public ReinProperties(Reinforcement rei)
        {
            rein = rei;
            name = rei.Name;
            iden = rei.Identifier.ToString();
            grade = rei.Grade;
            string size1 = string.Empty;
            string rebarPos1 = string.Empty;
            int rebarQty1 = 0;
            double rebarSeque1 = 0.0;
            double rebarLength1 = 0.0;
            double rebarWeight1 = 0.0;
            rei.GetReportProperty("NUMBER", ref rebarQty1);
            rei.GetReportProperty("SIZE", ref size1);
            rei.GetReportProperty("REBAR_POS", ref rebarPos1);
            rei.GetUserProperty("REBAR_SEQ_NO", ref rebarSeque1);
            rei.GetReportProperty("LENGTH", ref rebarLength1);
            rei.GetReportProperty("WEIGHT", ref rebarWeight1);
            size = size1;
            rebarPos = rebarPos1;
            rebarSeque = rebarSeque1;
            rebarLength = rebarLength1;
            rebarWeight = Math.Round(rebarWeight1, 2);
            rebarQty = rebarQty1;
        }
    }
    public class PropertisGroup
    {
        public List<TS.Identifier> idens { get; set; }
        public string name { get; set; }
        public string rebarPos { get; set; }
        public double rebarSeque { get; set; }
        public List<double> listRebarSque { get; set; }
        public string grade { get; set; }
        public string size { get; set; }
        public double rebarLength { get; set; }
        public double rebarWeight { get; set; }
        public int totalQTY { get; set; }
        public PropertisGroup(List<ReinProperties> rebarproperties)
        {
            totalQTY = 0;
            idens = new List<TS.Identifier>();
            listRebarSque = new List<double>();
            foreach (ReinProperties property in rebarproperties)
            {
                TS.Identifier idenTS = new TS.Identifier(Convert.ToInt32(property.iden));
                idens.Add(idenTS);
                listRebarSque.Add(property.rebarSeque);
                totalQTY += property.rebarQty;
            }
            if (listRebarSque.Count != 1)
            {
                listRebarSque = CompareList(listRebarSque);
            }
            if (listRebarSque.Count == 1)
            {
                rebarSeque = listRebarSque[0];
            }
            name = rebarproperties[0].name;
            rebarPos = rebarproperties[0].rebarPos;
            grade = rebarproperties[0].grade;
            size = rebarproperties[0].size;
            rebarLength = rebarproperties[0].rebarLength;
            rebarWeight = rebarproperties[0].rebarWeight;
        }
        public List<double> CompareList(List<double> listRSQN)
        {
            List<int> indexs = new List<int>();
            int count = listRSQN.Count;
            for (int i = 0; i < count - 1; i++)
            {
                int j = i + 1;
                if (j < listRSQN.Count)
                {
                    if (listRSQN[i] == listRSQN[j])
                    {
                        listRSQN.RemoveAt(j);
                        i--;
                    }
                }

            }
            return listRSQN;
        }
    }
}
