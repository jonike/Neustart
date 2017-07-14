﻿using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;

namespace Neustart
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Program
    {
        [JsonProperty]
        private static List<App> appList;
        private static Dictionary<string, App> appDictionary;

        private static string filePath = "Apps.json";

        private static Thread ThinkThread;
        public static Forms.Interface MainWindow { get; set; }

        public static decimal CpuMilliseconds { get; set; } = 0;

        private static bool AlreadyRunning()
        {
            foreach (Process process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
                if (process.Id != Process.GetCurrentProcess().Id)
                    if (Assembly.GetExecutingAssembly().Location.Replace("/", "\\") == Process.GetCurrentProcess().MainModule.FileName)
                        return true;
            
            return false;
        }

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (AlreadyRunning())
            {
                MessageBox.Show(null, "This copy of Neustart is already running. Look in your system tray!", "Neustart");
                return 0;
            }

            MainWindow = new Forms.Interface();
            MainWindow.Show();

            LoadAppData();

            ThinkThread = new Thread(AppThink);
            ThinkThread.Start();

            Application.Run(MainWindow);

            return 0;
        }

        private static void AppThink()
        {
            while (MainWindow.Visible)
            {
                foreach (App app in appList)
                {
                    app.GetTitle();

                    if (!app.Enabled || app.IsClosed() || app.IsCrashed() || app.IsRestarting)
                        continue;

                    app.GetUptime();
                    app.RefreshProcess();
                    app.GetCPU();
                    app.GetRam();
                }

                CpuMilliseconds = 0;
                foreach (Process proc in Process.GetProcesses())
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Refresh();
                            CpuMilliseconds += proc.PrivilegedProcessorTime.Milliseconds;
                        }
                    }
                    catch { }
                }

                Thread.Sleep(1000);
            }
        }

        private static void LoadAppData()
        {
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, "[]");

            appDictionary = new Dictionary<string, App>();
            appList = JsonConvert.DeserializeObject<List<App>>(File.ReadAllText(filePath));

            foreach (App app in appList)
                InitNewApp(app, false);
        }

        public static void SaveAppData()
            => File.WriteAllText(filePath, JsonConvert.SerializeObject(appList, Formatting.Indented));

        public static List<App> GetApps()
            => appList;

        public static App GetAppByID(string id)
            => appDictionary.ContainsKey(id) ? appDictionary[id] : null;


        public static void InitNewApp(App app, bool created)
        {
            appDictionary[app.ID] = app;

            object[] rowData = new object[] { app.ID, app.ID, "0", "00:00:00", "0%", "0 MB", app.Enabled ? "Stop" : "Start", app.Hidden ? "Show" : "Hide", "Edit" };
            app.DataRow = MainWindow.AppsTable.Rows[MainWindow.AppsTable.Rows.Add(rowData)];

            app.Init();

            if (created)
            {
                appList.Add(app);
                SaveAppData();
            }
        }

        public static void RenameApp(string oldID, string newID)
        {
            appDictionary[newID] = appDictionary[oldID];
            appDictionary.Remove(oldID);

            appDictionary[newID].DataRow.Cells[1].Value = newID;
        }

        public static void RemoveApp(App app)
        {
            if (app.Enabled)
                app.Stop();

            app.Close();

            MainWindow.AppsTable.Rows.Remove(app.DataRow);
            appDictionary.Remove(app.ID);
            appList.Remove(app);

            SaveAppData();
        }

        public static void Close()
        {
            ThinkThread.Abort();

            foreach (App app in appList)
                app.Close();
        }
    }
}
