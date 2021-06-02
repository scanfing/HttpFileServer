using System;
using System.Linq.Expressions;
using System.Windows.Input;
using Prism.Commands;

namespace HttpFileServer.Infrastructure
{
    public class CommandImpl : DelegateCommand
    {
        #region Constructors

        public CommandImpl(Action action) : base(action)
        {
        }

        public CommandImpl(Action executeMethod, Func<bool> canExecuteMethod) : base(executeMethod, canExecuteMethod)
        {
        }

        #endregion Constructors
    }
}