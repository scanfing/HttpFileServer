using System;
using System.Linq.Expressions;
using System.Windows.Input;
using Prism.Commands;

namespace HttpFileServer.Infrastructure
{
    public class CommandImpl<T> : DelegateCommand<T>
    {
        #region Constructors

        public CommandImpl(Action<T> action) : base(action)
        {
        }

        public CommandImpl(Action<T> executeMethod, Func<T, bool> canExecuteMethod) : base(executeMethod, canExecuteMethod)
        {
        }

        #endregion Constructors
    }
}