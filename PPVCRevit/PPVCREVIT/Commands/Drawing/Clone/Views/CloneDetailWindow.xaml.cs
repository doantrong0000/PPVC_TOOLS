using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PPVCREVIT.Commands.Drawing.Clone.Views
{
    public partial class CloneDetailWindow : Window
    {
        private Document _doc;
        private View _activeView;
        
        public View SelectedSourceView { get; private set; }

        public CloneDetailWindow(Document doc, View activeView)
        {
            InitializeComponent();
            _doc = doc;
            _activeView = activeView;
            LoadViews();
        }

        private void LoadViews()
        {
            // Collect all views that can potentially be source views
            // Exclude 3D views, schedules, and the active view itself
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && 
                            v.ViewType != ViewType.ThreeD && 
                            v.ViewType != ViewType.Schedule &&
                            v.ViewType != ViewType.DrawingSheet &&
                            v.ViewType != ViewType.ProjectBrowser &&
                            v.ViewType != ViewType.SystemBrowser &&
                            v.Id != _activeView.Id)
                .OrderBy(v => v.Name)
                .ToList();

            CmbViews.ItemsSource = views;
            
            if (views.Count > 0)
            {
                // Find the view with the most similar name to the active view
                string activeName = _activeView.Name;
                int minDistance = int.MaxValue;
                int bestIndex = 0;

                for (int i = 0; i < views.Count; i++)
                {
                    int dist = GetLevenshteinDistance(activeName, views[i].Name);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestIndex = i;
                    }
                }

                CmbViews.SelectedIndex = bestIndex;
            }
        }

        private int GetLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int[] v0 = new int[t.Length + 1];
            int[] v1 = new int[t.Length + 1];

            for (int i = 0; i < v0.Length; i++)
                v0[i] = i;

            for (int i = 0; i < s.Length; i++)
            {
                v1[0] = i + 1;
                for (int j = 0; j < t.Length; j++)
                {
                    int cost = (s[i] == t[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(v1[j] + 1, Math.Min(v0[j + 1] + 1, v0[j] + cost));
                }
                for (int j = 0; j < v0.Length; j++)
                    v0[j] = v1[j];
            }
            return v1[t.Length];
        }

        private void BtnClone_Click(object sender, RoutedEventArgs e)
        {
            SelectedSourceView = CmbViews.SelectedItem as View;
            if (SelectedSourceView == null)
            {
                MessageBox.Show("Vui lòng chọn một view mẫu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
