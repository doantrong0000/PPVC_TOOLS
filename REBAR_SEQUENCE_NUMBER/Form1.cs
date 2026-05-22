using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Tekla.Structures.Model;
using TSM = Tekla.Structures.Model;
using TS = Tekla.Structures;
using TSMUI = Tekla.Structures.Model.UI;

namespace REBAR_SEQUENCE_NUMBER
{
    public partial class Form1 : Form
    {
        private readonly Tekla.Structures.Model.Model _myModel;
        Dictionary<Tuple<string>, PropertisGroup> _DicCompare = new Dictionary<Tuple<string>, PropertisGroup>();
        List<TSM.Reinforcement> _rein = new List<Reinforcement>();
        List<PropertisGroup> _propertisGroups = new List<PropertisGroup>();
        public Form1()
        {
            InitializeComponent();
            _myModel = new Tekla.Structures.Model.Model();
        }
        private void btn_GetRebarFromModel_Click(object sender, EventArgs e)
        {
            dgvRebar.Rows.Clear();
            _DicCompare.Clear();
            _rein = Lib.GetRebarSelected();
            List<ReinProperties> rebarProperties1 = new List<ReinProperties>();
            foreach (var item in _rein)
            {
                ReinProperties rebarProperties = new ReinProperties(item);
                rebarProperties1.Add(rebarProperties);
            }
            Lib luci = new Lib();
            _propertisGroups = luci.GrouprebarToCompare(rebarProperties1).OrderBy(p => p.rebarSeque).ToList();

            foreach (var item in _propertisGroups)
            {

                if (item.listRebarSque.Count == 1)
                {
                    dgvRebar.Rows.Add(item.name, item.rebarPos, item.rebarSeque, item.grade, item.size, item.totalQTY, item.rebarLength, item.rebarWeight);
                    _DicCompare.Add(new Tuple<string>(item.rebarPos), item);
                }
                else
                {
                    double listrebarSQN = 0.01;
                    foreach (var item2 in item.listRebarSque)
                    {
                        if (item2 != 0)
                        {
                            listrebarSQN = 0.001;
                        }
                    }
                    dgvRebar.Rows.Add(item.name, item.rebarPos, listrebarSQN, item.grade, item.size, item.totalQTY, item.rebarLength, item.rebarWeight, "Overlap");
                    _DicCompare.Add(new Tuple<string>(item.rebarPos), item);
                }
            }
            for (int i = 0; i < dgvRebar.RowCount; i++)
            {
                if ((double)dgvRebar.Rows[i].Cells[2].Value == 0 || (double)dgvRebar.Rows[i].Cells[2].Value == 0.001)
                {
                    dgvRebar.Rows[i].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                }
                if ((double)dgvRebar.Rows[i].Cells[2].Value == 0)
                {
                    dgvRebar.Rows[i].Cells[8].Value = "Unassigned";
                }
            }
            for (int j = 0; j < dgvRebar.RowCount - 1; j++)
            {
                for (int h = j + 1; h < dgvRebar.RowCount; h++)
                {
                    if ((double)dgvRebar.Rows[j].Cells[2].Value == (double)dgvRebar.Rows[h].Cells[2].Value)
                    {
                        if ((string)dgvRebar.Rows[h].Cells[8].Value == null)
                        {
                            dgvRebar.Rows[h].Cells[8].Value = "Check again";
                        }
                        if ((string)dgvRebar.Rows[j].Cells[8].Value == null)
                        {
                            dgvRebar.Rows[j].Cells[8].Value = "Check again";
                        }
                        dgvRebar.Rows[j].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                        dgvRebar.Rows[h].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                    }
                }
            }
        }

        private void btn_SelectInModel_Click(object sender, EventArgs e)
        {
            List<TS.Identifier> listIden = new List<TS.Identifier>();
            ArrayList objs = new System.Collections.ArrayList();
            for (int i = 0; i < dgvRebar.SelectedRows.Count; i++)
            {
                string pos = Convert.ToString(dgvRebar.SelectedRows[i].Cells[1].Value);
                var keys = Tuple.Create(pos);
                foreach (var item in _DicCompare)
                {
                    if (item.Key.Item1 == keys.Item1)
                    {
                        List<TS.Identifier> identifiers = item.Value.idens;
                        foreach (var item2 in identifiers)
                        {
                            listIden.Add(item2);
                        }
                    }
                }
            }
            Lib Luci = new Lib();
            Luci.SelectionInModel(listIden);
            _myModel.CommitChanges();
        }

        public bool ListRebarSQNisNull(List<double> listRebarSQN)
        {
            bool result = true;
            for (int i = 0; i < listRebarSQN.Count; i++)
            {
                if (listRebarSQN[i] != 0)
                {
                    result = false;
                }
            }
            return result;
        }
        public double GetMin(List<double> listRebarSQN)
        {
            double result = 0;
            for (int i = 0; i < listRebarSQN.Count; i++)
            {
                if (listRebarSQN[i] > result)
                {
                    result = listRebarSQN[i];
                }
            }
            for (int i = 0; i < listRebarSQN.Count; i++)
            {
                if (listRebarSQN[i] < result)
                {
                    result = listRebarSQN[i];
                }
            }
            return result;
        }
        public List<double> GetListRSQNfromDic(string Pos)
        {
            List<double> result = new List<double>();
            foreach (var item in _DicCompare)
            {
                if (item.Key.Item1 == Pos)
                {
                    result = item.Value.listRebarSque;
                }
            }
            return result;
        }
        public List<double> GetListRebarSQNformData()
        {
            List<double> result = new List<double>();
            for (int i = 0; i < dgvRebar.Rows.Count; i++)
            {
                if (Convert.ToDouble(dgvRebar.Rows[i].Cells[2].Value) != 0 && Convert.ToDouble(dgvRebar.Rows[i].Cells[2].Value) != 0.0001)
                {
                    result.Add(Convert.ToDouble(dgvRebar.Rows[i].Cells[2].Value));
                }
            }
            return result;
        }
        private void TakeNewNumber()
        {
            int rebarSqn = 1;
            for (int i = 0; i < dgvRebar.RowCount; i++)
            {
                dgvRebar.Rows[i].Cells[2].Value = rebarSqn as object;
                rebarSqn++;
            }
        }
        private void btn_UpdateInModel_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dgvRebar.RowCount; i++)
            {
                string pos = Convert.ToString(dgvRebar.Rows[i].Cells[1].Value);
                string rebarSQN = Convert.ToString(dgvRebar.Rows[i].Cells[2].Value);
                var keys = Tuple.Create(pos);
                foreach (var item in _DicCompare)
                {
                    if (item.Key.Item1 == keys.Item1)
                    {
                        List<TS.Identifier> listIdens = item.Value.idens;
                        Lib Luci = new Lib();
                        Luci.SetRebarSQN(listIdens, Convert.ToDouble(rebarSQN));
                    }
                }
            }
            _myModel.CommitChanges();
            dgvRebar.Rows.Clear();
            List<ReinProperties> rebarProperties1 = new List<ReinProperties>();
            _propertisGroups.Clear();
            _DicCompare.Clear();
            foreach (var item in _rein)
            {
                ReinProperties rebarProperties = new ReinProperties(item);
                rebarProperties1.Add(rebarProperties);
            }
            Lib luci = new Lib();
            _propertisGroups = luci.GrouprebarToCompare(rebarProperties1).OrderBy(p => p.rebarSeque).ToList();

            foreach (var item in _propertisGroups)
            {

                if (item.listRebarSque.Count == 1)
                {
                    dgvRebar.Rows.Add(item.name, item.rebarPos, item.rebarSeque, item.grade, item.size, item.totalQTY, item.rebarLength, item.rebarWeight);
                    _DicCompare.Add(new Tuple<string>(item.rebarPos), item);
                }
                else
                {
                    double listrebarSQN = 0.01;
                    foreach (var item2 in item.listRebarSque)
                    {
                        if (item2 != 0)
                        {
                            listrebarSQN = 0.001;
                        }
                    }
                    dgvRebar.Rows.Add(item.name, item.rebarPos, listrebarSQN, item.grade, item.size, item.totalQTY, item.rebarLength, item.rebarWeight, "Overlap");
                    _DicCompare.Add(new Tuple<string>(item.rebarPos), item);
                }
            }
            for (int i = 0; i < dgvRebar.RowCount; i++)
            {
                if ((double)dgvRebar.Rows[i].Cells[2].Value == 0 || (double)dgvRebar.Rows[i].Cells[2].Value == 0.001)
                {
                    dgvRebar.Rows[i].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                }
                if ((double)dgvRebar.Rows[i].Cells[2].Value == 0)
                {
                    dgvRebar.Rows[i].Cells[8].Value = "Unassigned";
                }
            }
            for (int j = 0; j < dgvRebar.RowCount - 1; j++)
            {
                for (int h = j + 1; h < dgvRebar.RowCount; h++)
                {
                    if ((double)dgvRebar.Rows[j].Cells[2].Value == (double)dgvRebar.Rows[h].Cells[2].Value)
                    {
                        if ((string)dgvRebar.Rows[h].Cells[8].Value == null)
                        {
                            dgvRebar.Rows[h].Cells[8].Value = "Check again";
                        }
                        if ((string)dgvRebar.Rows[j].Cells[8].Value == null)
                        {
                            dgvRebar.Rows[j].Cells[8].Value = "Check again";
                        }
                        dgvRebar.Rows[j].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                        dgvRebar.Rows[h].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                    }
                }
            }
        }

        public void CompareDataGridView()
        {
            List<double> listRebarSQN = GetListRebarSQNformData();
            for (int i = 0; i < dgvRebar.RowCount; i++)
            {
                string pos = Convert.ToString(dgvRebar.Rows[i].Cells[1].Value);
                var keys = Tuple.Create(pos);
                List<double> listRSQN = GetListRSQNfromDic(pos);
                bool isnull = ListRebarSQNisNull(listRSQN);
                if (listRSQN.Count == 1 && listRSQN[0] == 0)
                {
                    double a = 1;
                    foreach (var item in listRebarSQN)
                    {
                        if (a == item)
                        {
                            a++;
                            foreach (var item1 in listRebarSQN)
                            {
                                if (a == item1)
                                {
                                    a++;
                                }
                            }
                        }
                    }
                    dgvRebar.Rows[i].Cells[2].Value = a;
                    listRebarSQN.Add(a);
                }
                if (listRSQN.Count != 1 && isnull == true)
                {
                    double a = 1;
                    foreach (var item1 in listRebarSQN)
                    {
                        if (a == item1)
                        {
                            a++;
                        }
                    }
                    dgvRebar.Rows[i].Cells[2].Value = a;
                    listRebarSQN.Add(a);
                }
                if (listRSQN.Count != 1 && isnull == false)
                {
                    double min = GetMin(listRSQN);
                    foreach (var item1 in listRebarSQN)
                    {
                        if (min == item1)
                        {
                            min++;
                        }
                    }
                    dgvRebar.Rows[i].Cells[2].Value = min;
                    listRebarSQN.Add(min);
                }
                for (int j = 0; j < dgvRebar.RowCount - 1; j++)
                {
                    for (int h = j + 1; h < dgvRebar.RowCount; h++)
                    {
                        if ((double)dgvRebar.Rows[j].Cells[2].Value == (double)dgvRebar.Rows[h].Cells[2].Value)
                        {
                            if ((string)dgvRebar.Rows[h].Cells[8].Value == null)
                            {
                                dgvRebar.Rows[h].Cells[8].Value = "Check again";
                            }
                            if ((string)dgvRebar.Rows[j].Cells[8].Value == null)
                            {
                                dgvRebar.Rows[j].Cells[8].Value = "Check again";
                            }
                            dgvRebar.Rows[j].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                            dgvRebar.Rows[h].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(204, 229, 255);
                        }
                    }
                }
            }
        }
        private void TakeNewAllNumber(double startnumbering)
        {
            if (startnumbering != null)
            {
                for (int i = 0; i < dgvRebar.RowCount; i++)
                {
                    dgvRebar.Rows[i].Cells[2].Value = startnumbering as object;
                    startnumbering++;
                }
            }
            else
            {
                MessageBox.Show("Enter start numbering!");
            }
        }
        private void TakeNewForSelection(double startnumbering)
        {
            if (startnumbering != null)
            {
                DataGridViewSelectedRowCollection selectedRows = dgvRebar.SelectedRows;
                if (selectedRows.Count != 0)
                {
                    List<int> listIndexSelection = new List<int>();
                    foreach (DataGridViewRow row in selectedRows)
                    {
                        listIndexSelection.Add(row.Index);
                    }
                    var list = listIndexSelection.OrderBy(p => p).ToList();
                    for (int i = 0; i < list.Count; i++)
                    {
                        dgvRebar.Rows[list[i]].Cells[2].Value = startnumbering as object;
                        startnumbering++;
                    }
                }
            }
            else
            {
                MessageBox.Show("Enter start numbering!");
            }
        }
        private void btn_ChangeData_Click(object sender, EventArgs e)
        {
            if (rdb_CompareToOld.Checked == true)
            {
                CompareDataGridView();
            }
            if (rdb_TakeNewForAll.Checked == true)
            {
                if (txt_StartNumbering.Text != "")
                {
                    TakeNewAllNumber(Convert.ToDouble(txt_StartNumbering.Text));
                }
                else
                {
                    MessageBox.Show("Enter start numbering");
                }
            }
            if (rdb_TakeNewSelect.Checked == true)
            {
                if (txt_StartNumbering.Text != "")
                {
                    TakeNewForSelection(Convert.ToDouble(txt_StartNumbering.Text));
                }
                else
                {
                    MessageBox.Show("Enter start number");
                }

            }
            if (rdb_CompareToOld.Checked == false && rdb_TakeNewForAll.Checked == false && rdb_TakeNewSelect.Checked == false)
            {
                MessageBox.Show("Choose option!");
            }
        }
        private void txt_StartNumbering_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!Char.IsDigit(e.KeyChar) && !Char.IsControl(e.KeyChar))
                e.Handled = true;
        }
    }
}
