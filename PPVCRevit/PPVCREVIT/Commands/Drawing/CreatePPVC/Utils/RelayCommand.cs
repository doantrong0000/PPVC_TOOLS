using System.Windows.Input;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Utils
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        // Hàm khởi tạo
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Sự kiện khi trạng thái CanExecute thay đổi
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // Điều kiện để nút (Button) được phép bấm (true = sáng lên, false = mờ đi)
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        // Hành động thực thi khi bấm nút
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}