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
            // Lắng nghe sự kiện nạp Assembly của .NET 4.8 để tìm DLL của Tekla 2025
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                string teklaBin = @"C:\Program Files\Tekla Structures\2025.0\bin\";
                string assemblyPath = Path.Combine(teklaBin, new System.Reflection.AssemblyName(eventArgs.Name).Name + ".dll");
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
    }
}