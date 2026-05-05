using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using TeklaApp.Views;

namespace TeklaApp
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Auto-detect Tekla install path from Registry and config directories
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                string assemblyName = new System.Reflection.AssemblyName(eventArgs.Name).Name;
                string[] searchPaths = GetTeklaSearchPaths();

                foreach (string path in searchPaths)
                {
                    string assemblyPath = Path.Combine(path, assemblyName + ".dll");
                    if (File.Exists(assemblyPath))
                    {
                        return System.Reflection.Assembly.LoadFrom(assemblyPath);
                    }
                }
                return null;
            };

            // Run UI separately to avoid loading Tekla API before assembly resolve hook completes
            RunUI();
        }

        static void RunUI()
        {
            var app = new System.Windows.Application();

            // Call intermediate method instead of initializing directly here
            if (!CheckTeklaConnection())
            {
                return;
            }

            app.Run(new MainWindow());
        }

        static bool CheckTeklaConnection()
        {
            var connCheck = new TeklaApp.Models.TeklaModelMng();
            if (!connCheck.IsConnected())
            {
                MessageBox.Show("Please open Tekla Structures first!");
                return false;
            }
            return true;
        }

        private static string[] GetTeklaSearchPaths()
        {
            var paths = new System.Collections.Generic.List<string>();

            // 1. Add paths from csproj (Tekla 2020.0)
            paths.Add(@"D:\Tekla Structure\2020.0\nt\bin\plugins");
            paths.Add(@"D:\Tekla Structure\2020.0\nt\bin");

            // 2. Find InstallDir path from Registry (Tekla 2020.0)
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Tekla\Structures\2020.0\setup"))
                {
                    if (key != null)
                    {
                        string installDir = key.GetValue("InstallDir") as string;
                        if (!string.IsNullOrEmpty(installDir))
                        {
                            paths.Add(Path.Combine(installDir, @"nt\bin\plugins"));
                            paths.Add(Path.Combine(installDir, @"nt\bin"));
                        }
                    }
                }
            }
            catch { }

            return paths.ToArray();
        }
    }
}