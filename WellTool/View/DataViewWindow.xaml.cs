using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro.Controls;

namespace WellTool.View
{
    /// <summary>
    /// Логика взаимодействия для DataViewWindow.xaml
    /// </summary>
    public partial class DataViewWindow : MetroWindow
    {
        // КОНСТРУКТОР принимает заголовок окна и данные
        public DataViewWindow(string title, DataTable dataTable)
        {
            InitializeComponent(); // 1. Инициализируем компоненты XAML

            this.Title = title; // 2. Устанавливаем заголовок окна

            // 3. Привязываем DataTable к DataGrid через DefaultView
            ResultsDataGrid.ItemsSource = dataTable.DefaultView;
        }
    }
}