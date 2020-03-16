using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DBLOG;

namespace MSSQLLogAnalyzer
{
    public partial class Form1 : Form
    {
        private delegate void ShowResult(object x);

        public Form1()
        {
            InitializeComponent();
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
        }

        private async void btnReadlog_Click(object sender, EventArgs e)
        {
            Action t;
            t = new Action(Readlog);

            btnReadlog.Enabled = false;
            try
            {
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
            DatabaseLogAnalyzer dbla;
            DatabaseLog[] logs;

            ConnectionString = txtConnectionstring.Text;
            StartTime = dtStarttime.Value.ToString("yyyy-MM-dd HH:mm:ss");
            EndTime = dtEndtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
            TableName = txtTablename.Text.TrimEnd();

            dbla = new DatabaseLogAnalyzer(ConnectionString);
            logs = dbla.ReadLog(StartTime, EndTime, TableName);
            Invoke(new ShowResult(ResetDataSource), new object[] { logs });

        }

        private void ResetDataSource(object d)
        {
            bindingSource1.DataSource = d;
            bindingSource1.ResetBindings(false);
        }

    }
}
