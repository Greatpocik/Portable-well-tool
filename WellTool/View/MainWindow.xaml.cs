using Oracle.ManagedDataAccess.Client;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using WellTool.Model;
using WellTool.Services;
using WellTool.ViewModel;
using MahApps.Metro.Controls;
//using WellTool.View;

namespace WellTool.View
{
    public partial class MainWindow : MetroWindow
    {
        private ObservableCollection<SearchCondition> SearchConditions { get; set; }
        private readonly MainViewModel _viewModel;


        public MainWindow()
        {
            try
            {
                //security.Instance.Initialize();

                InitializeComponent();

                ShowUserInfo();
                UpdateMenuPermissions();
                SearchConditions = new ObservableCollection<SearchCondition>
                {
                    new SearchCondition { Column = "Скважина", Value = "", IsFirst = true }
                };

                SearchConditionsContainer.ItemsSource = SearchConditions;

                _viewModel = new MainViewModel();
            }

            catch (Exception ex)
            {
                // Если Initialize() выбросил исключение - приложение уже закрывается
                MessageBox.Show($"Не удалось запустить приложение: {ex.Message}",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void UpdateMenuPermissions()
        {
            // Получаем доступ к контекстному меню DataGrid
            var contextMenu = WellsDataGrid.ContextMenu;

            // Проходим по всем пунктам меню и отключаем те, к которым нет прав
            foreach (var item in contextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    // Сопоставляем заголовок меню с правами
                    bool isEnabled = menuItem.Header switch
                    {
                        "Пульт: Журнал событий" => security.Instance.HasAccess("Журнал событий по скважине"),
                        "Пульт: События из пульта" => security.Instance.HasAccess("События из пульта по скважине"),
                        "ТКРС: История постановки бригады" => security.Instance.HasAccess("АТОЛЛ история постановки бригад по скважине"),
                        "ГТМ: Планы" => security.Instance.HasAccess("ГТМ нахождение в планах по скважине"),
                        "Запустить проверку истории" => security.Instance.isUserAuth, // Любой авторизованный
                        _ => true // По умолчанию включено
                    };

                    menuItem.IsEnabled = isEnabled;
                }
            }
        }
        private void ShowUserInfo()
        {
            // Можно добавить StatusBar или Label для отображения
            // Например, в заголовок окна
            this.Title += $" | Пользователь: {security.Instance.currentUser}";

            if (security.Instance.isAdmin)
            {
                this.Title += " (Администратор)";
            }
        }

        //------------------------------Обработчики поиска-----------------------------------------------------
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Собираем активные условия (где есть текст)
                var activeConditions = SearchConditions
                    .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                    .ToList();

                // 2. Проверка на наличие условий
                if (activeConditions.Count == 0)
                {
                    MessageBox.Show("Введите хотя бы одно условие для поиска",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Вызываем ViewModel с коллекцией условий
                List<WellData> foundWells = _viewModel.FindWells(activeConditions);

                // 4. Отображаем результат
                WellsDataGrid.ItemsSource = foundWells;

                // 5. Показываем результат пользователю
                MessageBox.Show($"Найдено скважин: {foundWells.Count}",
                    "Результат поиска", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddConditionButton_Click(object sender, RoutedEventArgs e)
        {
            SearchConditions.Add(new SearchCondition
            {
                Column = "Скважина",
                Value = ""
            });
        }

        // 2. УДАЛЕНИЕ УСЛОВИЯ
        private void RemoveConditionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SearchCondition condition)
            {
                // Не удаляем первое условие
                if (!condition.IsFirst)
                {
                    SearchConditions.Remove(condition);
                }
            }
        }

        //---------------------------------------------------Выбор строк в главном окне--------------------------------


        /// <summary>
        /// Получает список выбранных скважин
        /// Если выбраны чекбоксы - возвращает их, иначе - текущую выделенную строку
        /// </summary>
        private List<Model.WellData> GetSelectedWells()
        {
            var selectedWells = new List<Model.WellData>();

            // 1. ПРОВЕРЯЕМ ВЫБРАННЫЕ ЧЕКБОКСЫ
            foreach (var item in WellsDataGrid.Items)
            {
                if (item is Model.WellData well && well.IsSelected)
                {
                    selectedWells.Add(well);
                }
            }

            // 2. ЕСЛИ ЧЕКБОКСЫ НЕ ВЫБРАНЫ - берем текущую выделенную строку
            if (selectedWells.Count == 0 && WellsDataGrid.SelectedItem != null)
            {
                if (WellsDataGrid.SelectedItem is Model.WellData singleWell)
                {
                    selectedWells.Add(singleWell);
                }
            }

            return selectedWells;
        }

        /// <summary>
        /// Получает одну выбранную скважину (для обратной совместимости)
        /// </summary>
        private Model.WellData GetSelectedWell()
        {
            var selected = GetSelectedWells();
            return selected.Count > 0 ? selected[0] : null;
        }
        //выделение всех скважин
        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // ПРОХОДИМ ПО ВСЕМ СТРОКАМ
            foreach (var item in WellsDataGrid.Items)
            {
                if (item is Model.WellData well)
                {
                    well.IsSelected = true; // Устанавливаем флаг выбора
                }
            }

            // ОБНОВЛЯЕМ ОТОБРАЖЕНИЕ (важно!)
            WellsDataGrid.Items.Refresh();
        }

        // СНЯТЬ ВЫДЕЛЕНИЕ СО ВСЕХ СКВАЖИН
        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in WellsDataGrid.Items)
            {
                if (item is Model.WellData well)
                {
                    well.IsSelected = false;
                }
            }

            WellsDataGrid.Items.Refresh();
        }
        

        // 3. КНОПКА "Проверить историю" (дублирует пункт меню)
        private void CheckHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            // ПРОСТО ВЫЗЫВАЕМ ТОТ ЖЕ МЕТОД, ЧТО И ДЛЯ КОНТЕКСТНОГО МЕНЮ
            CheckDataPresenceMenuItem_Click(sender, e);
        }

        //----------------------------------------------Контекстное меню------------------------------------------------

        // 1. ОБРАБОТЧИК для "Журнал событий" - работает с множественным выбором
        private void EventJournalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Используем новый метод для получения ВСЕХ выбранных скважин
            var selectedWells = GetSelectedWells();

            // Проверяем, что хоть одна скважина выбрана
            if (selectedWells.Count == 0)
            {
                MessageBox.Show("Выберите одну или несколько скважин:\n" +
                               "• Кликните по строке\n" +
                               "• Или отметьте чекбоксы",
                               "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Счётчик успешно открытых окон
            int openedWindows = 0;

            // Для КАЖДОЙ выбранной скважины открываем отдельное окно
            foreach (var well in selectedWells)
            {
                try
                {
                    // Вызываем ViewModel для получения данных
                    DataTable dataTable = _viewModel.GetEventJournal(well.Id);

                    // Создаём окно с результатами
                    DataViewWindow window = new DataViewWindow(
                        $"Журнал событий - {well.Name}", // Заголовок окна
                        dataTable); // Данные для отображения

                    window.Show(); // Показываем окно (не модальное)
                    openedWindows++;
                }
                catch (System.Exception ex)
                {
                    // Показываем ошибку для конкретной скважины, но продолжаем с остальными
                    MessageBox.Show($"Ошибка для скважины '{well.Name}': {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Информируем пользователя о результате
            if (openedWindows > 0)
            {
                MessageBox.Show($"Открыто окон: {openedWindows} из {selectedWells.Count}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // 2. ОБРАБОТЧИК для "События из пульта" - работает с множественным выбором
        private void PultEventsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedWells = GetSelectedWells();

            if (selectedWells.Count == 0)
            {
                MessageBox.Show("Выберите одну или несколько скважин:\n" +
                               "• Кликните по строке\n" +
                               "• Или отметьте чекбоксы",
                               "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int openedWindows = 0;

            foreach (var well in selectedWells)
            {
                try
                {
                    DataTable dataTable = _viewModel.GetPultEvents(well.Id);
                    DataViewWindow window = new DataViewWindow(
                        $"События из пульта - {well.Name}",
                        dataTable);
                    window.Show();
                    openedWindows++;
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка для скважины '{well.Name}': {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (openedWindows > 0)
            {
                MessageBox.Show($"Открыто окон: {openedWindows} из {selectedWells.Count}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // 3. ОБРАБОТЧИК для "История постановки бригады" - работает с множественным выбором
        private void BrigadeHistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedWells = GetSelectedWells();

            if (selectedWells.Count == 0)
            {
                MessageBox.Show("Выберите одну или несколько скважин:\n" +
                               "• Кликните по строке\n" +
                               "• Или отметьте чекбоксы",
                               "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int openedWindows = 0;

            foreach (var well in selectedWells)
            {
                try
                {
                    DataTable dataTable = _viewModel.GetBrigadeHistory(well.Id);
                    DataViewWindow window = new DataViewWindow(
                        $"История постановки бригады - {well.Name}",
                        dataTable);
                    window.Show();
                    openedWindows++;
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка для скважины '{well.Name}': {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (openedWindows > 0)
            {
                MessageBox.Show($"Открыто окон: {openedWindows} из {selectedWells.Count}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // 4. ОБРАБОТЧИК для "Посуточное нахождение" - работает с множественным выбором
        private void DailyStatusMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedWells = GetSelectedWells();

            if (selectedWells.Count == 0)
            {
                MessageBox.Show("Выберите одну или несколько скважин:\n" +
                               "• Кликните по строке\n" +
                               "• Или отметьте чекбоксы",
                               "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int openedWindows = 0;

            foreach (var well in selectedWells)
            {
                try
                {
                    DataTable dataTable = _viewModel.GetDailyStatus(well.Id);
                    DataViewWindow window = new DataViewWindow(
                        $"Посуточное нахождение - {well.Name}",
                        dataTable);
                    window.Show();
                    openedWindows++;
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка для скважины '{well.Name}': {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (openedWindows > 0)
            {
                MessageBox.Show($"Открыто окон: {openedWindows} из {selectedWells.Count}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // 5. ОБРАБОТЧИК для "ГТМ: планы" - работает с множественным выбором
        private void GtmPlansMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedWells = GetSelectedWells();

            if (selectedWells.Count == 0)
            {
                MessageBox.Show("Выберите одну или несколько скважин:\n" +
                               "• Кликните по строке\n" +
                               "• Или отметьте чекбоксы",
                               "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int openedWindows = 0;

            foreach (var well in selectedWells)
            {
                try
                {
                    DataTable dataTable = _viewModel.GetGtmPlans(well.Id);
                    DataViewWindow window = new DataViewWindow(
                        $"ГТМ: планы - {well.Name}",
                        dataTable);
                    window.Show();
                    openedWindows++;
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка для скважины '{well.Name}': {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (openedWindows > 0)
            {
                MessageBox.Show($"Открыто окон: {openedWindows} из {selectedWells.Count}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        //-------------------------Проверка Истории--------------------------------
        private void CheckDataPresenceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 1. ПОЛУЧАЕМ ВСЕ ВЫБРАННЫЕ СКВАЖИНЫ (чекбоксы ИЛИ клик)
            List<Model.WellData> selectedWells = GetSelectedWells();

            // 2. ПРОВЕРЯЕМ, ЧТО ХОТЯ БЫ ОДНА СКВАЖИНА ВЫБРАНА
            if (selectedWells.Count == 0)
            {
                MessageBox.Show("Выберите одну или несколько скважин:\n" +
                               "• Кликните по строке\n" +
                               "• Или отметьте чекбоксы в столбце слева\n" +
                               "• Или используйте кнопки 'Выделить все' / 'Снять выделение'",
                               "Внимание",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return; // Выходим из метода, если ничего не выбрано
            }

            // 3. СОЗДАЕМ ТАБЛИЦУ ДЛЯ ВЫВОДА РЕЗУЛЬТАТОВ
            DataTable resultTable = new DataTable();

            // 4. ДОБАВЛЯЕМ КОЛОНКИ В ТАБЛИЦУ
            resultTable.Columns.Add("Скважина", typeof(string));            // Название скважины
            resultTable.Columns.Add("ID", typeof(string));                  // ID скважины
            resultTable.Columns.Add("Журнал событий", typeof(string));      // ✓ или ✗
            resultTable.Columns.Add("События пульта", typeof(string));      // ✓ или ✗
            resultTable.Columns.Add("Реестр ТКРС", typeof(string));         // ✓ или ✗
            resultTable.Columns.Add("Треб. удалить ремонт", typeof(string));// ✓ или ✗
            resultTable.Columns.Add("Сетевой ГТМ", typeof(string));         // ✓ или ✗
            resultTable.Columns.Add("Техн. разрядки", typeof(string));      // ✓ или ✗
            resultTable.Columns.Add("Готова к удалению", typeof(string));   // ДА или НЕТ

            try
            {
                // 5. ПРОВЕРЯЕМ КАЖДУЮ ВЫБРАННУЮ СКВАЖИНУ
                foreach (Model.WellData well in selectedWells)
                {
                    // 6. ВЫЗЫВАЕМ МЕТОД ViewModel ДЛЯ ПРОВЕРКИ ДАННЫХ ЭТОЙ СКВАЖИНЫ
                    Model.DataPresenceCheck checkResult = _viewModel.CheckDataPresence(well.Id);

                    // 7. ДОБАВЛЯЕМ СТРОКУ С РЕЗУЛЬТАТАМИ В ТАБЛИЦУ
                    DataRow newRow = resultTable.NewRow();

                    newRow["Скважина"] = well.Name;                     // Название скважины
                    newRow["ID"] = well.Id;                             // ID скважины
                    newRow["Журнал событий"] = checkResult.HasEventLog ? "✓" : "✗";
                    newRow["События пульта"] = checkResult.HasPultEvent ? "✓" : "✗";
                    newRow["Реестр ТКРС"] = checkResult.HasTKRS ? "✓" : "✗";
                    newRow["Треб. удалить ремонт"] = checkResult.RequiresDeleteRemont ? "✓" : "✗";
                    newRow["Сетевой ГТМ"] = checkResult.HasGTM ? "✓" : "✗";
                    newRow["Техн. разрядки"] = checkResult.HasTehnRazr ? "✓" : "✗";
                    newRow["Готова к удалению"] = checkResult.IsReadyToDelete ? "ДА" : "НЕТ";

                    resultTable.Rows.Add(newRow); // Добавляем строку в таблицу
                }

                // 8. СОЗДАЕМ ОКНО ДЛЯ ОТОБРАЖЕНИЯ РЕЗУЛЬТАТОВ
                DataViewWindow resultsWindow = new DataViewWindow(
                    $"Проверка данных ({selectedWells.Count} скважин)", // Заголовок окна
                    resultTable); // Таблица с результатами

                // 9. ПОКАЗЫВАЕМ ОКНО ПОЛЬЗОВАТЕЛЮ
                resultsWindow.Show();

                // 10. ОПЦИОНАЛЬНО: Сообщаем об успешной проверке
                MessageBox.Show($"Проверка завершена для {selectedWells.Count} скважин",
                               "Готово",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // 11. ОБРАБАТЫВАЕМ ОШИБКИ (если что-то пошло не так)
                MessageBox.Show($"Ошибка при проверке данных:\n{ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

    }
}