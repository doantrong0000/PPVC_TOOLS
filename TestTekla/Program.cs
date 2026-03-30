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
            // Tự động tìm đường dẫn cài đặt Tekla từ Registry và thư mục cấu hình
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

            // Chạy giao diện riêng để tránh tải Tekla API trước khi hook sự kiện Resolving hoàn tất
            RunUI();
        }

        static void RunUI()
        {
            var app = new System.Windows.Application();

            // Gọi một hàm trung gian thay vì khởi tạo trực tiếp ở đây
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
                MessageBox.Show("Vui lòng mở Tekla trước!");
                return false;
            }
            return true;
        }

        private static string[] GetTeklaSearchPaths()
        {
            var paths = new System.Collections.Generic.List<string>();

            // 1. Thêm đường dẫn từ csproj (Tekla 2020.0)
            paths.Add(@"D:\Tekla Structure\2020.0\nt\bin\plugins");
            paths.Add(@"D:\Tekla Structure\2020.0\nt\bin");

            // 2. Tìm đường dẫn InstallDir từ Registry (Tekla 2020.0)
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