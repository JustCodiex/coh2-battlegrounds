﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BattlegroundsApp.Views {

    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : ViewState {

        public DashboardView() {
            InitializeComponent();
        }

        public override void StateOnFocus() => throw new NotImplementedException();
        public override void StateOnLostFocus() => throw new NotImplementedException();

    }

}
