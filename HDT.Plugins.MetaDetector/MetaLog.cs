using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace HDT.Plugins.MetaDetector.Logging
{
    public class MetaLog
    {
        private const int MaxLogFileAge = 2;
        private const int KeepOldLogs = 15;

        private static string logDir = Path.Combine(Config.Instance.DataDir, @"MetaDetector\Logs");
        private static string logFile = Path.Combine(logDir, "meta_log.txt");

        internal static void Initialize()
        {
            Trace.AutoFlush = true;
            
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            else
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.Exists)
                    {
                        using (var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            //can access log file => no other instance of same installation running
                        }
                        File.Move(logFile, logFile.Replace(".txt", "_" + DateTime.Now.ToUnixTime() + ".txt"));
                        //keep logs from the last 2 days plus 15 before that
                        foreach (var file in
                            new DirectoryInfo(logDir).GetFiles("meta_log*")
                                                     .Where(x => x.LastWriteTime < DateTime.Now.AddDays(-MaxLogFileAge))
                                                     .OrderByDescending(x => x.LastWriteTime)
                                                     .Skip(KeepOldLogs))
                        {
                            try
                            {
                                File.Delete(file.FullName);
                            }
                            catch
                            {
                            }
                        }
                    }
                    else
                        File.Create(logFile).Dispose();
                }
                catch (Exception)
                {
                    try
                    {
                        var errLogFile = Path.Combine(logDir, "meta_log_err.txt");
                        using (var writer = new StreamWriter(errLogFile, true))
                            writer.WriteLine("[{0}]: {1}", DateTime.Now.ToLongTimeString(), "Unable to write to Meta Log");
                        //MessageBox.Show("Another instance of Hearthstone Deck Tracker is already running.", "Error starting Hearthstone Deck Tracker",
                        //                MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception)
                    {
                    }
                    //Application.Current.Shutdown();
                    return;
                }
            }
        }

        public static void WriteLine(string msg, LogType type, [CallerMemberName] string memberName = "",
                                     [CallerFilePath] string sourceFilePath = "")
        {
            var file = sourceFilePath?.Split('/', '\\').LastOrDefault()?.Split('.').FirstOrDefault();

            StreamWriter sw = new StreamWriter(logFile, true);
            sw.WriteLine($"{DateTime.Now.ToLongTimeString()}|{type}|{file}.{memberName} >> {msg}");
            sw.Close();
        }

        public static void Debug(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
            => WriteLine(msg, LogType.Debug, memberName, sourceFilePath);

        public static void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
            => WriteLine(msg, LogType.Info, memberName, sourceFilePath);

        public static void Warn(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
            => WriteLine(msg, LogType.Warning, memberName, sourceFilePath);

        public static void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
            => WriteLine(msg, LogType.Error, memberName, sourceFilePath);

        public static void Error(Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
            => WriteLine(ex.ToString(), LogType.Error, memberName, sourceFilePath);
    }
}