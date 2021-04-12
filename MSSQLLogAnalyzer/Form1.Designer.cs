namespace MSSQLLogAnalyzer
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.txtConnectionstring = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtTablename = new System.Windows.Forms.TextBox();
            this.dtEndtime = new System.Windows.Forms.DateTimePicker();
            this.label1 = new System.Windows.Forms.Label();
            this.dtStarttime = new System.Windows.Forms.DateTimePicker();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnReadlog = new System.Windows.Forms.Button();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.dgLogs = new System.Windows.Forms.DataGridView();
            this.transactionIDDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.beginTimeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.objectNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.operationDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.redoSQLDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.undoSQLDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgLogs)).BeginInit();
            this.SuspendLayout();
            // 
            // txtConnectionstring
            // 
            this.txtConnectionstring.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtConnectionstring.Location = new System.Drawing.Point(126, 18);
            this.txtConnectionstring.Name = "txtConnectionstring";
            this.txtConnectionstring.Size = new System.Drawing.Size(897, 23);
            this.txtConnectionstring.TabIndex = 45;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(12, 23);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(116, 17);
            this.label4.TabIndex = 44;
            this.label4.Text = "ConnectionString";
            // 
            // txtTablename
            // 
            this.txtTablename.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTablename.Location = new System.Drawing.Point(697, 60);
            this.txtTablename.Name = "txtTablename";
            this.txtTablename.Size = new System.Drawing.Size(168, 23);
            this.txtTablename.TabIndex = 43;
            // 
            // dtEndtime
            // 
            this.dtEndtime.CalendarFont = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtEndtime.CustomFormat = "yyyy/MM/dd HH:mm:ss";
            this.dtEndtime.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtEndtime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtEndtime.Location = new System.Drawing.Point(376, 60);
            this.dtEndtime.MaxDate = new System.DateTime(2099, 12, 31, 0, 0, 0, 0);
            this.dtEndtime.MinDate = new System.DateTime(2012, 1, 1, 0, 0, 0, 0);
            this.dtEndtime.Name = "dtEndtime";
            this.dtEndtime.Size = new System.Drawing.Size(170, 23);
            this.dtEndtime.TabIndex = 42;
            this.dtEndtime.TabStop = false;
            this.dtEndtime.Value = new System.DateTime(2020, 1, 1, 0, 0, 0, 0);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 65);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 17);
            this.label1.TabIndex = 37;
            this.label1.Text = "StartTime";
            // 
            // dtStarttime
            // 
            this.dtStarttime.CalendarFont = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtStarttime.CustomFormat = "yyyy/MM/dd HH:mm:ss";
            this.dtStarttime.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtStarttime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtStarttime.Location = new System.Drawing.Point(83, 60);
            this.dtStarttime.MaxDate = new System.DateTime(2099, 12, 31, 0, 0, 0, 0);
            this.dtStarttime.MinDate = new System.DateTime(2012, 1, 1, 0, 0, 0, 0);
            this.dtStarttime.Name = "dtStarttime";
            this.dtStarttime.Size = new System.Drawing.Size(170, 23);
            this.dtStarttime.TabIndex = 41;
            this.dtStarttime.TabStop = false;
            this.dtStarttime.Value = new System.DateTime(2020, 1, 1, 0, 0, 0, 0);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(615, 65);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(81, 17);
            this.label3.TabIndex = 39;
            this.label3.Text = "TableName";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(311, 65);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 17);
            this.label2.TabIndex = 38;
            this.label2.Text = "EndTime";
            // 
            // btnReadlog
            // 
            this.btnReadlog.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReadlog.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnReadlog.Location = new System.Drawing.Point(1052, 18);
            this.btnReadlog.Name = "btnReadlog";
            this.btnReadlog.Size = new System.Drawing.Size(93, 66);
            this.btnReadlog.TabIndex = 40;
            this.btnReadlog.Text = "ReadLog";
            this.btnReadlog.UseVisualStyleBackColor = true;
            this.btnReadlog.Click += new System.EventHandler(this.btnReadlog_Click);
            // 
            // bindingSource1
            // 
            this.bindingSource1.DataSource = typeof(DBLOG.DatabaseLog);
            // 
            // dgLogs
            // 
            this.dgLogs.AllowUserToAddRows = false;
            this.dgLogs.AllowUserToDeleteRows = false;
            this.dgLogs.AllowUserToResizeRows = false;
            this.dgLogs.AutoGenerateColumns = false;
            this.dgLogs.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.dgLogs.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.Silver;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(192)))));
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgLogs.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgLogs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgLogs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.transactionIDDataGridViewTextBoxColumn,
            this.beginTimeDataGridViewTextBoxColumn,
            this.objectNameDataGridViewTextBoxColumn,
            this.operationDataGridViewTextBoxColumn,
            this.redoSQLDataGridViewTextBoxColumn,
            this.undoSQLDataGridViewTextBoxColumn});
            this.dgLogs.DataSource = this.bindingSource1;
            this.dgLogs.EnableHeadersVisualStyles = false;
            this.dgLogs.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dgLogs.Location = new System.Drawing.Point(15, 102);
            this.dgLogs.Name = "dgLogs";
            this.dgLogs.ReadOnly = true;
            this.dgLogs.RowHeadersVisible = false;
            this.dgLogs.RowTemplate.Height = 23;
            this.dgLogs.ShowCellToolTips = false;
            this.dgLogs.ShowEditingIcon = false;
            this.dgLogs.ShowRowErrors = false;
            this.dgLogs.Size = new System.Drawing.Size(1164, 558);
            this.dgLogs.TabIndex = 46;
            this.dgLogs.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgLogs_CellDoubleClick);
            // 
            // transactionIDDataGridViewTextBoxColumn
            // 
            this.transactionIDDataGridViewTextBoxColumn.DataPropertyName = "TransactionID";
            this.transactionIDDataGridViewTextBoxColumn.HeaderText = "TransactionID";
            this.transactionIDDataGridViewTextBoxColumn.Name = "transactionIDDataGridViewTextBoxColumn";
            this.transactionIDDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // beginTimeDataGridViewTextBoxColumn
            // 
            this.beginTimeDataGridViewTextBoxColumn.DataPropertyName = "BeginTime";
            this.beginTimeDataGridViewTextBoxColumn.HeaderText = "BeginTime";
            this.beginTimeDataGridViewTextBoxColumn.Name = "beginTimeDataGridViewTextBoxColumn";
            this.beginTimeDataGridViewTextBoxColumn.ReadOnly = true;
            this.beginTimeDataGridViewTextBoxColumn.Width = 145;
            // 
            // objectNameDataGridViewTextBoxColumn
            // 
            this.objectNameDataGridViewTextBoxColumn.DataPropertyName = "ObjectName";
            this.objectNameDataGridViewTextBoxColumn.HeaderText = "ObjectName";
            this.objectNameDataGridViewTextBoxColumn.Name = "objectNameDataGridViewTextBoxColumn";
            this.objectNameDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // operationDataGridViewTextBoxColumn
            // 
            this.operationDataGridViewTextBoxColumn.DataPropertyName = "Operation";
            this.operationDataGridViewTextBoxColumn.HeaderText = "Operation";
            this.operationDataGridViewTextBoxColumn.Name = "operationDataGridViewTextBoxColumn";
            this.operationDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // redoSQLDataGridViewTextBoxColumn
            // 
            this.redoSQLDataGridViewTextBoxColumn.DataPropertyName = "RedoSQL";
            this.redoSQLDataGridViewTextBoxColumn.HeaderText = "RedoSQL  (DoubleClick cell to View full SQL)";
            this.redoSQLDataGridViewTextBoxColumn.Name = "redoSQLDataGridViewTextBoxColumn";
            this.redoSQLDataGridViewTextBoxColumn.ReadOnly = true;
            this.redoSQLDataGridViewTextBoxColumn.Width = 320;
            // 
            // undoSQLDataGridViewTextBoxColumn
            // 
            this.undoSQLDataGridViewTextBoxColumn.DataPropertyName = "UndoSQL";
            this.undoSQLDataGridViewTextBoxColumn.HeaderText = "UndoSQL  (DoubleClick cell to View full SQL)";
            this.undoSQLDataGridViewTextBoxColumn.Name = "undoSQLDataGridViewTextBoxColumn";
            this.undoSQLDataGridViewTextBoxColumn.ReadOnly = true;
            this.undoSQLDataGridViewTextBoxColumn.Width = 320;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1191, 673);
            this.Controls.Add(this.dgLogs);
            this.Controls.Add(this.txtConnectionstring);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtTablename);
            this.Controls.Add(this.dtEndtime);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dtStarttime);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnReadlog);
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgLogs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtConnectionstring;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtTablename;
        private System.Windows.Forms.DateTimePicker dtEndtime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DateTimePicker dtStarttime;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnReadlog;
        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.DataGridView dgLogs;
        private System.Windows.Forms.DataGridViewTextBoxColumn transactionIDDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn beginTimeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn objectNameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn operationDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn redoSQLDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn undoSQLDataGridViewTextBoxColumn;
    }
}

