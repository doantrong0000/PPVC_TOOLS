using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TeklaApp.ViewModels;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;

namespace TeklaApp.Views
{
    public partial class StepTagPage : UserControl
    {
        private StepTagViewModel _viewModel;
        private bool _initialized = false;

        public StepTagPage()
        {
            InitializeComponent();
            _viewModel = new StepTagViewModel();
            LoadSettings();
            _initialized = true;
            DrawPreview();
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                SaveSettings();
                DrawPreview();
            }
        }

        private string GetSettingsPath()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
            return System.IO.Path.Combine(assemblyDir, "StepTagSettings.json");
        }

        private void LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonConvert.DeserializeObject<StepTagSettings>(json);
                    if (settings != null)
                    {
                        txtTextHeight.Text = settings.TextHeight.ToString();
                        txtFontName.Text = settings.FontName;
                        SetComboBoxText(cmbTextColor, settings.TextColor);
                        txtSurfLen.Text = settings.SurfLen.ToString();
                        txtStepHeight.Text = settings.StepHeight.ToString();
                        txtHatchSpc.Text = settings.HatchSpc.ToString();
                        txtHatchLen.Text = settings.HatchLen.ToString();
                        chkUseRectFill.IsChecked = settings.UseRectFill;
                        txtFillName.Text = settings.FillName;
                        if (settings.ScaleX == 0) settings.ScaleX = 1.0;
                        if (settings.ScaleY == 0) settings.ScaleY = 1.0;
                        txtScaleX.Text = settings.ScaleX.ToString();
                        txtScaleY.Text = settings.ScaleY.ToString();
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new StepTagSettings
                {
                    TextHeight = V(txtTextHeight, 3.5),
                    FontName = txtFontName.Text,
                    TextColor = GetComboBoxText(cmbTextColor),
                    SurfLen = V(txtSurfLen, 15),
                    StepHeight = V(txtStepHeight, 10),
                    HatchSpc = V(txtHatchSpc, 3),
                    HatchLen = V(txtHatchLen, 12),
                    UseRectFill = chkUseRectFill.IsChecked ?? false,
                    FillName = txtFillName.Text,
                    ScaleX = V(txtScaleX, 1.0),
                    ScaleY = V(txtScaleY, 1.0)
                };
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(GetSettingsPath(), json);
            }
            catch { }
        }

        private void DrawPreview()
        {
            if (previewCanvas == null) return;
            previewCanvas.Children.Clear();

            double th = V(txtTextHeight, 3.5);
            double surfLen = V(txtSurfLen, 15);
            double stepH = V(txtStepHeight, 10);
            double hatchSpc = V(txtHatchSpc, 3);
            double hatchLen = V(txtHatchLen, 12);

            double cW = 400, cH = 300;
            double margin = 50;

            double s = Math.Min((cW - margin * 2) / (surfLen * 2 + 10), (cH - margin * 2) / (stepH + hatchLen + 20));
            if (s < 0.5) s = 0.5;

            // Dựa trên Tekla (Horizontal Z-bar => Vertical Joint, High Right)
            double pX_J = 0, pY_J = 0;
            double pX_HighEnd = surfLen, pY_HighEnd = 0;
            double pX_LowJ = 0, pY_LowJ = -stepH;
            double pX_LowEnd = -surfLen, pY_LowEnd = -stepH;

            // Ánh xạ tọa độ Tekla -> Màn hình Canvas
            double ox = cW / 2.0;
            double oy = cH / 2.0;
            double MapX(double x) => ox + x * s;
            double MapY(double y) => oy - (y + stepH / 2.0) * s;

            Point cJ = new Point(MapX(pX_J), MapY(pY_J));
            Point cHighEnd = new Point(MapX(pX_HighEnd), MapY(pY_HighEnd));
            Point cLowJ = new Point(MapX(pX_LowJ), MapY(pY_LowJ));
            Point cLowEnd = new Point(MapX(pX_LowEnd), MapY(pY_LowEnd));

            // ======== Z-bar lines ========
            Ln(cHighEnd.X, cHighEnd.Y, cJ.X, cJ.Y, Brushes.Black, 2);
            Ln(cJ.X, cJ.Y, cLowJ.X, cLowJ.Y, Brushes.Black, 2);
            Ln(cLowJ.X, cLowJ.Y, cLowEnd.X, cLowEnd.Y, Brushes.Black, 2);

            // ======== Hatching ========
            bool useRectFill = chkUseRectFill?.IsChecked ?? false;
            double hx = 0.707;
            double hy = -0.707;

            if (!useRectFill)
            {
                void DrawTeklaHatch(double fx, double fy, double tx, double ty)
                {
                    double dx = tx - fx;
                    double dy = ty - fy;
                    double lineLen = Math.Sqrt(dx * dx + dy * dy);
                    if (lineLen < 0.1 || hatchSpc < 0.1) return;

                    double nx = dx / lineLen;
                    double ny = dy / lineLen;

                    double startPadding = hatchSpc * 0.5;
                    double endPadding = hatchSpc * 0.5;
                    double effectiveLen = lineLen - (startPadding + endPadding);
                    if (effectiveLen <= 0) return;

                    int n = (int)(effectiveLen / hatchSpc);
                    for (int h = 0; h <= n; h++)
                    {
                        double t = startPadding + (h * hatchSpc);
                        double hsX = fx + nx * t;
                        double hsY = fy + ny * t;
                        double heX = hsX + hx * hatchLen;
                        double heY = hsY + hy * hatchLen;

                        Ln(MapX(hsX), MapY(hsY), MapX(heX), MapY(heY), Brushes.Black, 1);
                    }
                }

                DrawTeklaHatch(pX_HighEnd, pY_HighEnd, pX_J, pY_J);
                DrawTeklaHatch(pX_LowJ, pY_LowJ, pX_LowEnd, pY_LowEnd);
            }
            else
            {
                hy = -1.0; 
                double hDepthX = 0; 
                double hDepthY = -hatchLen; 

                Polygon hPoly = new Polygon();
                hPoly.Points.Add(new Point(MapX(pX_HighEnd), MapY(pY_HighEnd)));
                hPoly.Points.Add(new Point(MapX(pX_J), MapY(pY_J)));
                hPoly.Points.Add(new Point(MapX(pX_J + hDepthX), MapY(pY_J + hDepthY)));
                hPoly.Points.Add(new Point(MapX(pX_HighEnd + hDepthX), MapY(pY_HighEnd + hDepthY)));
                hPoly.Stroke = Brushes.Black;
                hPoly.StrokeThickness = 1;
                hPoly.Fill = new SolidColorBrush(Color.FromArgb(80, 100, 100, 100));
                previewCanvas.Children.Add(hPoly);

                Polygon lPoly = new Polygon();
                lPoly.Points.Add(new Point(MapX(pX_LowJ), MapY(pY_LowJ)));
                lPoly.Points.Add(new Point(MapX(pX_LowEnd), MapY(pY_LowEnd)));
                lPoly.Points.Add(new Point(MapX(pX_LowEnd + hDepthX), MapY(pY_LowEnd + hDepthY)));
                lPoly.Points.Add(new Point(MapX(pX_LowJ + hDepthX), MapY(pY_LowJ + hDepthY)));
                lPoly.Stroke = Brushes.Black;
                lPoly.StrokeThickness = 1;
                lPoly.Fill = new SolidColorBrush(Color.FromArgb(80, 100, 100, 100));
                previewCanvas.Children.Add(lPoly);
            }

            // ======== Text ========
            double tX = (pX_J + pX_LowJ) / 2.0 - surfLen * 0.3; 
            double tY = (pY_J + pY_LowJ) / 2.0;

            double textSize = Math.Max(12, th * s * 0.6); 
            var stepText = new TextBlock
            {
                Text = ((int)stepH).ToString(),
                FontSize = textSize,
                Foreground = GetWpfBrush(GetComboBoxText(cmbTextColor)),
                FontWeight = FontWeights.Bold
            };
            
            Canvas.SetLeft(stepText, MapX(tX) - textSize * 0.5);
            Canvas.SetTop(stepText, MapY(tY) - textSize * 0.6);
            previewCanvas.Children.Add(stepText);

            // ======== Dimensions ========
            double yHatchMax = MapY(-stepH - Math.Abs(hy) * hatchLen);
            DimH(cLowEnd.X, cLowJ.X, yHatchMax + 15, surfLen.ToString());
            DimV(cHighEnd.Y, cLowEnd.Y, cHighEnd.X + 20, stepH.ToString());
        }

        private void Ln(double x1, double y1, double x2, double y2, Brush c, double t)
        {
            previewCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = c, StrokeThickness = t });
        }

        private void DimH(double x1, double x2, double y, string label)
        {
            Ln(x1, y, x2, y, Brushes.DarkRed, 0.8);
            Ln(x1, y - 3, x1, y + 3, Brushes.DarkRed, 0.8);
            Ln(x2, y - 3, x2, y + 3, Brushes.DarkRed, 0.8);
            var tb = new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.DarkRed };
            Canvas.SetLeft(tb, (x1 + x2) / 2.0 - 8);
            Canvas.SetTop(tb, y - 15);
            previewCanvas.Children.Add(tb);
        }

        private void DimV(double y1, double y2, double x, string label)
        {
            Ln(x, y1, x, y2, Brushes.DarkRed, 0.8);
            Ln(x - 3, y1, x + 3, y1, Brushes.DarkRed, 0.8);
            Ln(x - 3, y2, x + 3, y2, Brushes.DarkRed, 0.8);
            var tb = new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.DarkRed };
            Canvas.SetLeft(tb, x + 4);
            Canvas.SetTop(tb, (y1 + y2) / 2.0 - 7);
            previewCanvas.Children.Add(tb);
        }

        private double V(TextBox tb, double fallback)
        {
            if (string.IsNullOrEmpty(tb?.Text)) return fallback;
            return double.TryParse(tb.Text, out double v) ? v : fallback;
        }



        private string GetComboBoxText(ComboBox cb)
        {
            if (cb.SelectedItem is ComboBoxItem item) return item.Content.ToString();
            return "Green";
        }

        private void SetComboBoxText(ComboBox cb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (ComboBoxItem item in cb.Items)
            {
                if (item.Content.ToString() == text)
                {
                    cb.SelectedItem = item;
                    break;
                }
            }
        }

        private Brush GetWpfBrush(string colorName)
        {
            try
            {
                return (Brush)new BrushConverter().ConvertFromString(colorName);
            }
            catch { return Brushes.Green; }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            parentWindow.Hide();
            try
            {
                bool useRectFill = chkUseRectFill?.IsChecked ?? false;
                string fillName = txtFillName?.Text ?? "ANSI31_13";
                string textColor = GetComboBoxText(cmbTextColor);

                string result = _viewModel.CreateStepTag(
                    V(txtTextHeight, 3.5), txtFontName.Text, textColor,
                    V(txtSurfLen, 15), V(txtStepHeight, 10), V(txtHatchSpc, 3), V(txtHatchLen, 12), useRectFill, fillName, V(txtScaleX, 1.0), V(txtScaleY, 1.0));
                txtStatus.Text = result;
            }
            finally
            {
                parentWindow.Show();
            }
        }
    }

    public class StepTagSettings
    {
        public double TextHeight { get; set; }
        public string FontName { get; set; }
        public string TextColor { get; set; }
        public double SurfLen { get; set; }
        public double StepHeight { get; set; }
        public double HatchSpc { get; set; }
        public double HatchLen { get; set; }
        public bool UseRectFill { get; set; }
        public string FillName { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
    }
}
