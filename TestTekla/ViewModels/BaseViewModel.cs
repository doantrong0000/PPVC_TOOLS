using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestTekla.ViewModels
{
    // Thêm abstract vì ta không bao giờ khởi tạo trực tiếp BaseViewModel
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Bổ sung [CallerMemberName] để tự động nhận diện tên property gọi nó
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Hàm SetProperty chuẩn để gán giá trị và gọi sự kiện
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            // Kiểm tra nếu giá trị mới giống giá trị cũ thì không làm gì cả
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            // Cập nhật giá trị mới
            storage = value;

            // Kích hoạt sự kiện cập nhật UI
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}