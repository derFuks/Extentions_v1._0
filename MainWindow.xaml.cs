using System;
using System.Data;
using System.Linq;
using System.Windows;
using Npgsql;
using Dapper;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Extentions_v1._0
{
    public partial class MainWindow : Window
    {
        private NpgsqlConnection _connection;
        private int _currentPage = 0;
        private const int PageSize = 15;
        private const string AllColumnsOption = "_Все значения";
        private Dictionary<string, string> columnTypes = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private string GetConnectionString()
        {
            string server = ServerInput.Text;
            string port = PortInput.Text;
            string database = DatabaseInput.Text;
            string user = UserInput.Text;
            string password = PasswordInput.Password;
            return $"Host={server};Port={port};Database={database};Username={user};Password={password};SslMode=Require;Trust Server Certificate=true";
        }

        private void ConnectToDatabase(object sender, RoutedEventArgs e)
        {
            if (_connection != null && _connection.State == ConnectionState.Open)
            {
                MessageBox.Show("Соединение уже установлено!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _connection = new NpgsqlConnection(GetConnectionString());
                _connection.Open();
                ConnectionStatus.Text = "Подключено";
                ConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectDatabase(object sender, RoutedEventArgs e)
        {
            if (_connection != null && _connection.State == ConnectionState.Open)
            {
                _connection.Close();
                ConnectionStatus.Text = "Отключено";
                ConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private void LoadTables()
        {
            try
            {
                var tables = _connection.Query<string>("SELECT tablename FROM pg_tables WHERE schemaname = 'public';").ToList();
                TableSelection.ItemsSource = tables;
                TableSelection.SelectedIndex = -1;
                ColumnSelection.ItemsSource = new List<string> { AllColumnsOption };
                ColumnSelection.SelectedItem = AllColumnsOption;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TableSelection_Changed(object sender, SelectionChangedEventArgs e)
        {
            string table = TableSelection.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(table)) return;

            try
            {
                var columns = _connection.Query<(string column_name, string data_type)>(
                    $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = '{table}';").ToList();
                columnTypes = columns.ToDictionary(c => c.column_name, c => c.data_type);
                var columnNames = columns.Select(c => c.column_name).ToList();
                columns.Insert(0, (AllColumnsOption, "text"));
                ColumnSelection.ItemsSource = columnNames;
                ColumnSelection.SelectedItem = AllColumnsOption;
                ColumnSelection.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки столбцов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteQuery(object sender, RoutedEventArgs e)
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                MessageBox.Show("Сначала подключитесь к базе данных!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string table = TableSelection.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(table))
            {
                MessageBox.Show("Выберите таблицу!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string column = ColumnSelection.SelectedItem?.ToString();
            string filter = FilterInput.Text;
            // string groupBy = GroupByInput.Text;
            filter = filter.Replace("'", "''");
            string query = $"SELECT * FROM \"{table}\" ";
            
            if (!string.IsNullOrEmpty(filter) && !string.IsNullOrEmpty(column) && column != AllColumnsOption)
            {
                if (columnTypes.ContainsKey(column))
                {
                    string columnType = columnTypes[column];
                    if (columnType == "smallint" || columnType == "integer" || columnType == "bigint" || columnType == "numeric")
                    { 
                        if (int.TryParse(filter, out _))
                        {
                            query += $"WHERE \"{column}\" = {filter} "; // убрал кавычки вокруг {filter}
                        }
                        else
                        {
                            MessageBox.Show("Фильтр должен содержать только числа для числовых колонок!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        query += $"WHERE \"{column}\" ~ '{filter}' ";
                    }
                }
            }
            // if (!string.IsNullOrEmpty(groupBy)) query += $"GROUP BY {groupBy} ";
            query += $"LIMIT {PageSize} OFFSET {_currentPage * PageSize}";

            try
            {
                var data = _connection.Query(query).ToList();
                ResultsGrid.ItemsSource = data;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запроса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetFilters(object sender, RoutedEventArgs e)
        {
            TableSelection.SelectedIndex = -1;
            ColumnSelection.ItemsSource = new List<string> { AllColumnsOption };
            ColumnSelection.SelectedItem = AllColumnsOption;
            FilterInput.Text = string.Empty;
        }
        private void NextPage(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            ExecuteQuery(sender, e);
        }

        private void PrevPage(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                ExecuteQuery(sender, e);
            }
        }
    }
}
