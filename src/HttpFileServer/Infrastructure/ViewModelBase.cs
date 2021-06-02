using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HttpFileServer.Infrastructure
{
    public class ViewModelBase : BindableBase
    {
        #region Fields

        private readonly string _typeName = "ViewModelName";

        private string _title = "";

        #endregion Fields

        #region Constructors

        public ViewModelBase()
        {
            _typeName = GetType().Name;
            Log("Create.");

            LoadedCommand = new CommandImpl<object>(OnViewLoaded, (sender) => true);
            UnLoadedCommand = new CommandImpl<object>(OnViewUnLoaded, (sender) => true);
            InitViewModel();
        }

        #endregion Constructors

        #region Properties

        public DateTime CurrentTime => DateTime.Now;

        public ICommand LoadedCommand { get; private set; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ICommand UnLoadedCommand { get; private set; }

        #endregion Properties

        #region Methods

        protected virtual void InitViewModel()
        {
        }

        protected virtual void Log(string msg)
        {
            var content = $"{_typeName} {msg}";
            System.Diagnostics.Trace.WriteLine(content);
        }

        protected virtual void OnViewLoaded(object sender)
        {
            Log("Loaded.");
        }

        protected virtual void OnViewUnLoaded(object sender)
        {
            Log("UnLoaded.");
        }

        #endregion Methods
    }
}