using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TeklaApp.Helpers
{
    public static class PromptDialog
    {
        public static string ShowDialog(string text, string caption, string defaultValue = "")
        {
            Window prompt = new Window()
            {
                Width = 350,
                Height = 160,
                Title = caption,
                WindowStyle = WindowStyle.ToolWindow,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true
            };

            StackPanel stack = new StackPanel() { Margin = new Thickness(15) };
            
            TextBlock textLabel = new TextBlock() { Text = text, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
            
            TextBox textBox = new TextBox() { Text = defaultValue, Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(2) };
            
            Button confirmation = new Button() { Content = "OK", Width = 80, Height = 25, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
            
            confirmation.Click += (sender, e) => { prompt.DialogResult = true; prompt.Close(); };
            
            // Auto Select All string on focus
            textBox.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            stack.Children.Add(textLabel);
            stack.Children.Add(textBox);
            stack.Children.Add(confirmation);
            prompt.Content = stack;

            return prompt.ShowDialog() == true ? textBox.Text : "";
        }
    }
}
