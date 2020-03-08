using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSSQLLogAnalyzer
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(FThreadException);
            Application.Run(new Form1());
        }

        public static void FThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Exception ex;
            StringBuilder sb;

            ex = e.Exception;
            sb = new StringBuilder();
            sb.AppendLine("Exception Type: " + ex.GetType().Name).AppendLine();
            sb.AppendLine("Message: " + ex.Message).AppendLine();
            sb.AppendLine("Stack Trace: " + ex.StackTrace).AppendLine();

            MessageBox.Show(sb.ToString(), "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}
