using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HttpFileServer.Controls
{
    /// <summary>
    /// PathControls.xaml 的交互逻辑
    /// </summary>
    public partial class PathSelector : System.Windows.Controls.UserControl
    {
        #region Fields

        // Using a DependencyProperty as the backing store for CanEdit.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CanEditProperty =
            DependencyProperty.Register("CanEdit", typeof(bool), typeof(PathSelector), new PropertyMetadata(false, OnPropertyCanEditChanged));

        // Using a DependencyProperty as the backing store for SelectedPath.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedPathProperty =
            DependencyProperty.Register("SelectedPath", typeof(string), typeof(PathSelector), new PropertyMetadata(""));

        private FolderBrowserDialog _fbd;

        #endregion Fields

        #region Constructors

        public PathSelector()
        {
            InitializeComponent();
        }

        #endregion Constructors

        #region Properties

        public bool CanEdit
        {
            get { return (bool)GetValue(CanEditProperty); }
            set { SetValue(CanEditProperty, value); }
        }

        public string SelectedPath
        {
            get { return (string)GetValue(SelectedPathProperty); }
            set { SetValue(SelectedPathProperty, value); }
        }

        #endregion Properties

        #region Methods

        private static void OnPropertyCanEditChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PathSelector ps)
            {
                ps.txtPath.IsReadOnly = !(bool)e.NewValue;
            }
        }

        private void Btn_SelectPath_Click(object sender, RoutedEventArgs e)
        {
            DoSelectPath();
        }

        private void DoSelectPath()
        {
            if (_fbd is null)
            {
                _fbd = new FolderBrowserDialog();
                _fbd.Description = "请选择";
            }

            _fbd.SelectedPath = SelectedPath;
            if (_fbd.ShowDialog() == DialogResult.OK)
            {
                SelectedPath = _fbd.SelectedPath;
            }
        }

        private void DoSelectPathClick(object sender, RoutedEventArgs e)
        {
            DoSelectPath();
        }

        #endregion Methods
    }
}