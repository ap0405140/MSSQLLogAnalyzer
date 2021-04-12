using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using DBLOG;

namespace MSSQLLogAnalyzer
{
    public partial class Form1 : Form
    {
        private delegate void ShowResult(DatabaseLogAnalyzer x);
        private DatabaseLogAnalyzer dbla;
        private DatabaseLog[] logs;
        private System.Timers.Timer timer;

        public Form1()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Connection String: Please change below connection string for your environment.
            txtConnectionstring.Text = "server=[ServerName];database=[DatabaseName];uid=[LoginName];pwd=[Password];Connection Timeout=5;Integrated Security=false;";

            //Time Range: Default to read at last 10 seconds 's logs, you can change the time range for need.
            dtStarttime.Value = Convert.ToDateTime(DateTime.Now.AddSeconds(-10).ToString("yyyy/MM/dd HH:mm:ss"));
            dtEndtime.Value = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));

            //Table Name: Need include schema name(like dbo.Table1), When blank means query all tables 's logs, you can change it for need.
            txtTablename.Text = "";

            timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.AutoReset = true;
            timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_elapsed);
            timer.Enabled = false;
        }

        private async void btnReadlog_Click(object sender, EventArgs e)
        {
            Action t;
            
            try
            {
                btnReadlog.Enabled = false;

                logs = new DatabaseLog[] { };
                bindingSource1.DataSource = logs;
                bindingSource1.ResetBindings(false);
                timer.Enabled = true;

                t = new Action(Readlog);
                await Task.Run(t);
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                btnReadlog.Enabled = true;
            }
        }

        private void Readlog()
        {
            string ConnectionString, StartTime, EndTime, TableName;

            ConnectionString = txtConnectionstring.Text;
            StartTime = dtStarttime.Value.ToString("yyyy-MM-dd HH:mm:ss");
            EndTime = dtEndtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
            TableName = txtTablename.Text.TrimEnd();

            dbla = new DatabaseLogAnalyzer(ConnectionString);
            logs = dbla.ReadLog(StartTime, EndTime, TableName);
        }

        private void timer_elapsed(object sender, ElapsedEventArgs e)
        {
            Invoke(new ShowResult(fshowresult), new object[] { dbla });
        }

        private void fshowresult(DatabaseLogAnalyzer p)
        {
            if (p is null)
            {
                return;
            }

            btnReadlog.Text = "ReadLog\r\n[" + p.ReadPercent.ToString() + "%]";

            if (p.ReadPercent >= 100)
            {
                bindingSource1.DataSource = logs;
                bindingSource1.ResetBindings(false);

                timer.Enabled = false;
                btnReadlog.Text = "ReadLog";
            }
        }

        private void dgLogs_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            DatabaseLog currlog;
            string tfilename;

            if (e.ColumnIndex != redoSQLDataGridViewTextBoxColumn.Index 
                && e.ColumnIndex != undoSQLDataGridViewTextBoxColumn.Index)
            {
                return;
            }

            currlog = dgLogs.CurrentRow.DataBoundItem as DatabaseLog;
            tfilename = "temp\\" + Guid.NewGuid().ToString().Replace("-", "") + ".txt";

            if (e.ColumnIndex == redoSQLDataGridViewTextBoxColumn.Index)
            {
                currlog.RedoSQLFile.ToFile(tfilename);
            }
            else
            {
                currlog.UndoSQLFile.ToFile(tfilename);
            }
         
            Process.Start(tfilename);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            string temppath;
            DirectoryInfo di;

            try
            {
                temppath = Application.StartupPath + "\\temp\\";
                di = new DirectoryInfo(temppath);

                foreach (FileInfo tf in di.GetFiles())
                {
                    tf.Delete();
                }
            }
            catch(Exception ex)
            {

            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (timer.Enabled == true)
            {
                timer.Enabled = false;
            }

            Application.DoEvents();
        }

    }
}
