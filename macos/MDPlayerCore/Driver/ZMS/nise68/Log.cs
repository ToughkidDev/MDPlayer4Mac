using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MDPlayer.Driver.ZMS.nise68
{
    public static class Log
    {
        private static Action<string, object[]>? msgWrite = null;
        private static LogLevel logLevel = LogLevel.Information;

        //[Conditional("DEBUG")]
        public static void WriteLine(LogLevel level, string msg, params object[] op)
        {
            if (level < logLevel) return;
            msgWrite?.Invoke(msg + "\r\n", op);
        }

        //[Conditional("DEBUG")]
        public static void Write(LogLevel level, string msg, params object[] op)
        {
            if (level < logLevel) return;
            msgWrite?.Invoke(msg , op);
        }

        //[Conditional("DEBUG")]
        public static void SetMsgWrite(Action<string, object[]> msgWrite)
        {
            Log.msgWrite = msgWrite;
        }

        //[Conditional("DEBUG")]
        public static void SetLogLevel(LogLevel level)
        {
            logLevel = level;
        }
    }

    public enum LogLevel
    {
        None = 0,
        Trace ,
        Trace2 ,
        Debug ,
        Information ,
        Warning ,
        Error
    }
}
