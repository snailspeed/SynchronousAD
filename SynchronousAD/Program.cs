using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;

namespace SynchronousAD
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmAD());
        }


    }


    #region## 日志
    /// <summary>
    /// 日志
    /// </summary>
    public class LogRecord
    {
        private static DB_MODE DEBUG_MODE = DB_MODE.ENABLE;
        private LogRecord()
        {
        }

        public static DB_MODE Mode
        {
            set { DEBUG_MODE = value; }
            get { return DEBUG_MODE; }
        }
        private static string sLogFile = string.Format(@"c:\log\adlog{0}.log", DateTime.Now.ToString("yyyy-MM-dd"));

        public static void WriteLog(string sMsg)
        {
            if (DEBUG_MODE == DB_MODE.ENABLE)
            {
                // Write log msg
                try
                {
                    using (StreamWriter sr = new StreamWriter(sLogFile, true))
                    {
                        sr.WriteLine(string.Format("{0}:{1}",
                            DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss --"), sMsg));
                        sr.Flush();
                    }
                }
                catch { }
            }
        }
    }
    #endregion

    #region## 日志类型（Enum）
    /// <summary>
    /// 日志类型
    /// </summary>
    public enum DB_MODE : int
    {
        DISABLE = 0,
        ENABLE = 1,
    }
    #endregion
}
