﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HttpFileServer.Infrastructure;
using HttpFileServer.Services;
using HttpFileServer.ViewModels;

namespace HttpFileServer.Views
{
    /// <summary>
    /// ShellView.xaml 的交互逻辑
    /// </summary>
    public partial class ShellView : Window
    {
        #region Constructors

        public ShellView()
        {
            InitializeComponent();
            Loaded += ShellView_Loaded;
            Unloaded += ShellView_Unloaded;
        }

        #endregion Constructors

        #region Methods

        private void ShellView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ShellViewModel svm)
            {
                svm.Dispatcher = Dispatcher;
                svm.LoadedCommand?.Execute(sender);
            }
        }

        private void ShellView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ShellViewModel svm)
            {
                svm.Dispatcher = Dispatcher;
                svm.UnLoadedCommand?.Execute(sender);
            }
        }

        #endregion Methods
    }
}