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
            // Tự động tìm đường dẫn cài đặt Tekla từ Registry
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                string assemblyName = new System.Reflection.AssemblyName(eventArgs.Name).Name;
                string teklaBin = GetTeklaBinPath();
                
                string assemblyPath = Path.Combine(teklaBin, assemblyName + ".dll");
                if (File.Exists(assemblyPath))
                {
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);
                }
                return null;
            };

            // Chạy giao diện riêng để tránh tải Tekla API trước khi hook sự kiện Resolving hoàn tất
            RunUI();
        }

        static void RunUI()
        {
            var app = new System.Windows.Application();
            app.Run(new MainWindow());
        }

        private static string GetTeklaBinPath()
        {
            try
            {
                // Tìm đường dẫn InstallDir từ Registry (Tekla 2025.0)
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Tekla\Structures\2025.0\Setup"))
                {
                    if (key != null)
                    {
                        string installDir = key.GetValue("InstallDir") as string;
                        if (!string.IsNullOrEmpty(installDir))
                        {
                            return Path.Combine(installDir, "bin\\");
                        }
                    }
                }
            }
            catch { }

            // Fallback nếu không tìm thấy Registry (ví dụ cài bản portable hoặc registry bị lỗi)
            return @"C:\Program Files\Tekla Structures\2025.0\bin\";
        }
    }
}