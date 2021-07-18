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
using System.Configuration;
using DBLOG;

namespace MSSQLLogAnalyzer
{
    public partial class Form1 : Form
    {
        private delegate void ShowResult(DatabaseLogAnalyzer x);
        private DatabaseLogAnalyzer dbla;
        private DatabaseLog[] logs;
        private System.Timers.Timer timer;
        private Configuration config;
        private DateTime beginruntime;

        public Form1()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
            
            //Connection String: Please change below connection string for your environment.
            txtConnectionstring.Text = config.AppSettings.Settings["DefaultConnectionString"].Value;

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

            Init();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Maximized;
        }

        private async void btnReadlog_Click(object sender, EventArgs e)
        {
            try
            {
                Init();
                btnReadlog.Enabled = false;

                logs = new DatabaseLog[] { };
                bindingSource1.DataSource = logs;
                bindingSource1.ResetBindings(false);
                timer.Enabled = true;
                beginruntime = DateTime.Now;

                await Task.Run(() => Readlog());
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

        private void Init()
        {
            tsTime.Text = "00:00:00";
            tsRows.Text = "0 rows";
            tsProg.Value = 0;
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
            if (p is null) { return; }

            tsTime.Text = (DateTime.Now - beginruntime).ToString(@"hh\:mm\:ss");
            tsProg.Value = p.ReadPercent;

            if (p.ReadPercent >= 100)
            {
                bindingSource1.DataSource = logs;
                bindingSource1.ResetBindings(false);

                timer.Enabled = false;
                tsRows.Text = $"{logs.Length.ToString()} rows";
            }
        }

        private void dgLogs_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            DatabaseLog currlog;
            string tfilename;

            if (e.ColumnIndex == redoSQLDataGridViewTextBoxColumn.Index 
                || e.ColumnIndex == undoSQLDataGridViewTextBoxColumn.Index)
            {
                currlog = dgLogs.CurrentRow.DataBoundItem as DatabaseLog;
                tfilename = $"temp\\{Guid.NewGuid().ToString().Replace("-", "")}.txt";

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
        }
        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            int newwidth;

            newwidth = (dgLogs.Width - (transactionIDDataGridViewTextBoxColumn.Width
                                        + beginTimeDataGridViewTextBoxColumn.Width
                                        + objectNameDataGridViewTextBoxColumn.Width
                                        + operationDataGridViewTextBoxColumn.Width
                                        + 30)) / 2;
            redoSQLDataGridViewTextBoxColumn.Width = newwidth;
            undoSQLDataGridViewTextBoxColumn.Width = newwidth;

            newwidth = dgLogs.Width - (tsTime.Width + tsRows.Width + 80);
            tsProg.Width = newwidth;
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (timer.Enabled == true)
            {
                timer.Enabled = false;
            }

            Application.DoEvents();
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            string tmpstr;
            DirectoryInfo di;

            try
            {
                tmpstr = txtConnectionstring.Text;
                config.AppSettings.Settings.Remove("DefaultConnectionString");
                config.AppSettings.Settings.Add("DefaultConnectionString", tmpstr);
                config.Save(ConfigurationSaveMode.Minimal);

                tmpstr = Application.StartupPath + "\\temp\\";
                di = new DirectoryInfo(tmpstr);
                if (di.Exists == true)
                {
                    foreach (FileInfo tf in di.GetFiles())
                    {
                        tf.Delete();
                    }
                }
            }
            catch(Exception ex)
            {

            }
        }

    }
}
