using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HttpFileServer.Infrastructure
{
    public abstract class BindableBase : INotifyPropertyChanged
    {
        #region Events

        [method: CompilerGenerated]
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Methods

        /// <summary>
        /// 属性变更时的事件
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storage"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns>是否成功</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            bool flag = Equals(storage, value);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                storage = value;
                OnPropertyChanged(propertyName);
                result = true;
            }
            return result;
        }

        #endregion Methods
    }
}