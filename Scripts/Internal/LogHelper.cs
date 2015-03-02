using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BTEServer
{
    /// <summary>
    /// log4net助手
    /// </summary>
    public class LogHelper
    {
        static bool init = false;
        private static string _fileName =
#if UNITY_ANDROID
		"Config/log4net";
#elif UNITY_STANDALONE_WIN
 System.Environment.CurrentDirectory + "/Log/BTEServer.config";
#endif

        /// <summary>
        /// 输出日志到Log4Net
        /// </summary>
        /// <param name="t"></param>
        /// <param name="ex"></param>
        #region static void WriteLog(Type t, Exception ex)


        public static void WriteLog(Type t, Exception ex)
        {
            if (init == false)
            {
                log4net.Config.XmlConfigurator.ConfigureAndWatch(new System.IO.FileInfo(_fileName));
                init = true;
            }
            log4net.ILog log = log4net.LogManager.GetLogger(t);
            Debug.LogError(ex);
            log.Error("Error", ex);
            Console.WriteLine(ex);
        }

        #endregion

        /// <summary>
        /// 输出日志到Log4Net
        /// </summary>
        /// <param name="t"></param>
        /// <param name="msg"></param>
        #region static void WriteLog(Type t, string msg)

        public static void WriteLog(Type t, string msg)
        {
            if (init == false)
            {
                log4net.Config.XmlConfigurator.ConfigureAndWatch(new System.IO.FileInfo(_fileName));
                init = true;
            }
            log4net.ILog log = log4net.LogManager.GetLogger(t);
            Debug.LogError(msg);
            log.Error(msg);
            Console.WriteLine(msg);
        }

        #endregion


    }
}