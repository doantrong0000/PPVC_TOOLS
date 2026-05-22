namespace REBAR_SEQUENCE_NUMBER
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txt_StartNumbering = new System.Windows.Forms.TextBox();
            this.rdb_CompareToOld = new System.Windows.Forms.RadioButton();
            this.rdb_TakeNewForAll = new System.Windows.Forms.RadioButton();
            this.rdb_TakeNewSelect = new System.Windows.Forms.RadioButton();
            this.btn_SelectInModel = new System.Windows.Forms.Button();
            this.btn_UpdateInModel = new System.Windows.Forms.Button();
            this.btn_ChangeData = new System.Windows.Forms.Button();
            this.btn_GetRebarFromModel = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.dgvRebar = new System.Windows.Forms.DataGridView();
            this.DataGColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.nOTE = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRebar)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.txt_StartNumbering);
            this.groupBox1.Controls.Add(this.rdb_CompareToOld);
            this.groupBox1.Controls.Add(this.rdb_TakeNewForAll);
            this.groupBox1.Controls.Add(this.rdb_TakeNewSelect);
            this.groupBox1.Location = new System.Drawing.Point(650, 92);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(137, 120);
            this.groupBox1.TabIndex = 41;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Numbering RSQN";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 95);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 32;
            this.label2.Text = "Start numbering";
            // 
            // txt_StartNumbering
            // 
            this.txt_StartNumbering.Location = new System.Drawing.Point(93, 88);
            this.txt_StartNumbering.Name = "txt_StartNumbering";
            this.txt_StartNumbering.Size = new System.Drawing.Size(30, 20);
            this.txt_StartNumbering.TabIndex = 31;
            this.txt_StartNumbering.Text = "1";
            this.txt_StartNumbering.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txt_StartNumbering_KeyPress);
            // 
            // rdb_CompareToOld
            // 
            this.rdb_CompareToOld.AutoSize = true;
            this.rdb_CompareToOld.Location = new System.Drawing.Point(6, 19);
            this.rdb_CompareToOld.Name = "rdb_CompareToOld";
            this.rdb_CompareToOld.Size = new System.Drawing.Size(96, 17);
            this.rdb_CompareToOld.TabIndex = 28;
            this.rdb_CompareToOld.TabStop = true;
            this.rdb_CompareToOld.Text = "Compare to old";
            this.rdb_CompareToOld.UseVisualStyleBackColor = true;
            // 
            // rdb_TakeNewForAll
            // 
            this.rdb_TakeNewForAll.AutoSize = true;
            this.rdb_TakeNewForAll.Location = new System.Drawing.Point(6, 42);
            this.rdb_TakeNewForAll.Name = "rdb_TakeNewForAll";
            this.rdb_TakeNewForAll.Size = new System.Drawing.Size(87, 17);
            this.rdb_TakeNewForAll.TabIndex = 29;
            this.rdb_TakeNewForAll.TabStop = true;
            this.rdb_TakeNewForAll.Text = "Renumber all";
            this.rdb_TakeNewForAll.UseVisualStyleBackColor = true;
            // 
            // rdb_TakeNewSelect
            // 
            this.rdb_TakeNewSelect.AutoSize = true;
            this.rdb_TakeNewSelect.Location = new System.Drawing.Point(6, 65);
            this.rdb_TakeNewSelect.Name = "rdb_TakeNewSelect";
            this.rdb_TakeNewSelect.Size = new System.Drawing.Size(117, 17);
            this.rdb_TakeNewSelect.TabIndex = 30;
            this.rdb_TakeNewSelect.TabStop = true;
            this.rdb_TakeNewSelect.Text = "Renumber selected";
            this.rdb_TakeNewSelect.UseVisualStyleBackColor = true;
            // 
            // btn_SelectInModel
            // 
            this.btn_SelectInModel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_SelectInModel.Location = new System.Drawing.Point(650, 61);
            this.btn_SelectInModel.Name = "btn_SelectInModel";
            this.btn_SelectInModel.Size = new System.Drawing.Size(138, 23);
            this.btn_SelectInModel.TabIndex = 38;
            this.btn_SelectInModel.Text = "Select";
            this.btn_SelectInModel.UseVisualStyleBackColor = true;
            this.btn_SelectInModel.Click += new System.EventHandler(this.btn_SelectInModel_Click);
            // 
            // btn_UpdateInModel
            // 
            this.btn_UpdateInModel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_UpdateInModel.Location = new System.Drawing.Point(650, 279);
            this.btn_UpdateInModel.Name = "btn_UpdateInModel";
            this.btn_UpdateInModel.Size = new System.Drawing.Size(137, 39);
            this.btn_UpdateInModel.TabIndex = 36;
            this.btn_UpdateInModel.Text = "Update in Model";
            this.btn_UpdateInModel.UseVisualStyleBackColor = true;
            this.btn_UpdateInModel.Click += new System.EventHandler(this.btn_UpdateInModel_Click);
            // 
            // btn_ChangeData
            // 
            this.btn_ChangeData.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_ChangeData.Location = new System.Drawing.Point(650, 250);
            this.btn_ChangeData.Name = "btn_ChangeData";
            this.btn_ChangeData.Size = new System.Drawing.Size(137, 23);
            this.btn_ChangeData.TabIndex = 35;
            this.btn_ChangeData.Text = "Change";
            this.btn_ChangeData.UseVisualStyleBackColor = true;
            this.btn_ChangeData.Click += new System.EventHandler(this.btn_ChangeData_Click);
            // 
            // btn_GetRebarFromModel
            // 
            this.btn_GetRebarFromModel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_GetRebarFromModel.Location = new System.Drawing.Point(650, 12);
            this.btn_GetRebarFromModel.Name = "btn_GetRebarFromModel";
            this.btn_GetRebarFromModel.Size = new System.Drawing.Size(137, 43);
            this.btn_GetRebarFromModel.TabIndex = 37;
            this.btn_GetRebarFromModel.Text = "Get rebar from Model";
            this.btn_GetRebarFromModel.UseVisualStyleBackColor = true;
            this.btn_GetRebarFromModel.Click += new System.EventHandler(this.btn_GetRebarFromModel_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox1.Location = new System.Drawing.Point(651, 423);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(137, 49);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 40;
            this.pictureBox1.TabStop = false;
            // 
            // dgvRebar
            // 
            this.dgvRebar.AllowUserToAddRows = false;
            this.dgvRebar.AllowUserToDeleteRows = false;
            this.dgvRebar.AllowUserToResizeRows = false;
            this.dgvRebar.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvRebar.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.ColumnHeader;
            this.dgvRebar.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRebar.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.DataGColumn1,
            this.DataGridViewTextBoxColumn3,
            this.DataGridViewTextBoxColumn4,
            this.Column1,
            this.Column2,
            this.Column3,
            this.DataGridViewTextBoxColumn5,
            this.Column4,
            this.nOTE});
            this.dgvRebar.Location = new System.Drawing.Point(6, 12);
            this.dgvRebar.Name = "dgvRebar";
            this.dgvRebar.ReadOnly = true;
            this.dgvRebar.RowHeadersVisible = false;
            this.dgvRebar.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvRebar.Size = new System.Drawing.Size(638, 441);
            this.dgvRebar.TabIndex = 39;
            // 
            // DataGColumn1
            // 
            this.DataGColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.DataGColumn1.HeaderText = "Name";
            this.DataGColumn1.Name = "DataGColumn1";
            this.DataGColumn1.ReadOnly = true;
            this.DataGColumn1.Width = 80;
            // 
            // DataGridViewTextBoxColumn3
            // 
            this.DataGridViewTextBoxColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.DataGridViewTextBoxColumn3.HeaderText = "Mark";
            this.DataGridViewTextBoxColumn3.Name = "DataGridViewTextBoxColumn3";
            this.DataGridViewTextBoxColumn3.ReadOnly = true;
            this.DataGridViewTextBoxColumn3.Width = 80;
            // 
            // DataGridViewTextBoxColumn4
            // 
            this.DataGridViewTextBoxColumn4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.DataGridViewTextBoxColumn4.FillWeight = 50F;
            this.DataGridViewTextBoxColumn4.HeaderText = "Rebar Sequence Number";
            this.DataGridViewTextBoxColumn4.Name = "DataGridViewTextBoxColumn4";
            this.DataGridViewTextBoxColumn4.ReadOnly = true;
            this.DataGridViewTextBoxColumn4.Width = 80;
            // 
            // Column1
            // 
            this.Column1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Column1.HeaderText = "Grade";
            this.Column1.Name = "Column1";
            this.Column1.ReadOnly = true;
            this.Column1.Width = 50;
            // 
            // Column2
            // 
            this.Column2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Column2.HeaderText = "Size";
            this.Column2.Name = "Column2";
            this.Column2.ReadOnly = true;
            this.Column2.Width = 50;
            // 
            // Column3
            // 
            this.Column3.HeaderText = "Qty";
            this.Column3.Name = "Column3";
            this.Column3.ReadOnly = true;
            this.Column3.Width = 48;
            // 
            // DataGridViewTextBoxColumn5
            // 
            this.DataGridViewTextBoxColumn5.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.DataGridViewTextBoxColumn5.FillWeight = 50F;
            this.DataGridViewTextBoxColumn5.HeaderText = "Length";
            this.DataGridViewTextBoxColumn5.Name = "DataGridViewTextBoxColumn5";
            this.DataGridViewTextBoxColumn5.ReadOnly = true;
            this.DataGridViewTextBoxColumn5.Width = 80;
            // 
            // Column4
            // 
            this.Column4.HeaderText = "Weight";
            this.Column4.Name = "Column4";
            this.Column4.ReadOnly = true;
            this.Column4.Width = 66;
            // 
            // nOTE
            // 
            this.nOTE.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.nOTE.HeaderText = "Note";
            this.nOTE.Name = "nOTE";
            this.nOTE.ReadOnly = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(279, 459);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(205, 13);
            this.label1.TabIndex = 42;
            this.label1.Text = "Developed by Digital team - WH Viet Nam";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(794, 481);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btn_SelectInModel);
            this.Controls.Add(this.btn_UpdateInModel);
            this.Controls.Add(this.btn_ChangeData);
            this.Controls.Add(this.btn_GetRebarFromModel);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.dgvRebar);
            this.MinimumSize = new System.Drawing.Size(810, 520);
            this.Name = "Form1";
            this.Text = "(90_01) Auto Numbering Rebar Sequence Number";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRebar)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txt_StartNumbering;
        private System.Windows.Forms.RadioButton rdb_CompareToOld;
        private System.Windows.Forms.RadioButton rdb_TakeNewForAll;
        private System.Windows.Forms.RadioButton rdb_TakeNewSelect;
        private System.Windows.Forms.Button btn_SelectInModel;
        private System.Windows.Forms.Button btn_UpdateInModel;
        private System.Windows.Forms.Button btn_ChangeData;
        private System.Windows.Forms.Button btn_GetRebarFromModel;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.DataGridView dgvRebar;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn5;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.DataGridViewTextBoxColumn nOTE;
        private System.Windows.Forms.Label label1;
    }
}

