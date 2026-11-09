using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using WellTool.Model;
using WellTool.Services;


namespace WellTool.ViewModel
{
    public class MainViewModel
    {
        //  ПОЛЕ для работы с БД
        private readonly OracleConnector _oracleConnector;

        //  КОНСТРУКТОР - инициализация
        public MainViewModel()
        {
            _oracleConnector = new OracleConnector();

        }

        //------------------------------ Поиск скважин --------------------------------------------------
        public List<WellData> FindWells(List<SearchCondition> searchConditions)
        {
            if (searchConditions == null || searchConditions.Count == 0)
                throw new ArgumentException("Не заданы условия поиска");

            try
            {
                string sql = BuildSearchQuery(searchConditions);
                var parameters = new List<OracleParameter>();

                for (int i = 0; i < searchConditions.Count; i++)
                {
                    string value = searchConditions[i].Value ?? string.Empty;

                    // Передаем значение как есть
                    parameters.Add(new OracleParameter($":searchValue{i}", OracleDbType.Varchar2)
                    {
                        Value = value
                    });
                }

                DataTable resultTable = _oracleConnector.GetDataTable(sql, parameters.ToArray());
                return ConvertToWellList(resultTable);
            }
            catch (OracleException ex)
            {
                throw new Exception($"Ошибка базы данных: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при поиске: {ex.Message}", ex);
            }
        }
        private string BuildSearchQuery(List<SearchCondition> conditions)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine(Queries.GetWellInfoBase());

            var columnMapping = new Dictionary<string, string>
            {
                ["ID"] = "id",
                ["Скважина"] = "\"скв\"",
                ["Куст"] = "\"куст\"",
                ["Цех"] = "\"цех\"",
                ["ДНС"] = "\"днс\"",
                ["Месторождение"] = "\"месторождение\"",
                ["Характер ЦДС"] = "\"характер цдс\"",
                ["Фонд ОИС"] = "\"фонд оис\""
            };

            if (conditions.Any(c => !string.IsNullOrWhiteSpace(c.Value)))
            {
                sqlBuilder.AppendLine("WHERE 1=1");

                for (int i = 0; i < conditions.Count; i++)
                {
                    string value = conditions[i].Value;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string dbColumn = columnMapping[conditions[i].Column];

                        // КЛЮЧЕВАЯ ЛОГИКА:
                        // Если нет % и _ → точный поиск (UPPER = UPPER)
                        // Если есть % или _ → LIKE-поиск
                        if (!value.Contains("%") && !value.Contains("_"))
                        {
                            sqlBuilder.AppendLine($"  AND UPPER({dbColumn}) = UPPER(:searchValue{i})");
                        }
                        else
                        {
                            sqlBuilder.AppendLine($"  AND UPPER({dbColumn}) LIKE UPPER(:searchValue{i})");
                        }
                    }
                }
            }

            return sqlBuilder.ToString();
        }

        //  приватный метод для FindWells, конвертация DataTable → List<Well>
        private List<WellData> ConvertToWellList(DataTable table)
        {
            var wells = new List<WellData>();

            if (table == null || table.Rows.Count == 0)
                return wells;

            foreach (DataRow row in table.Rows)
            {

                var well = new WellData
                {
                    Id = row["Id"]?.ToString() ?? string.Empty,
                    Name = row["Name"]?.ToString() ?? string.Empty,
                    Pad = row["Pad"]?.ToString() ?? string.Empty,
                    Workshop = row["Workshop"]?.ToString() ?? string.Empty,
                    DNS = row["DNS"]?.ToString() ?? string.Empty,
                    Field = row["Field"]?.ToString() ?? string.Empty,
                    CdsCharacter = row["CdsCharacter"]?.ToString() ?? string.Empty,
                    OisCharacter = row["OisCharacter"]?.ToString() ?? string.Empty,
                    OisFund = row["OisFund"]?.ToString() ?? string.Empty,
                    RegionId = row["RegionId"]?.ToString() ?? string.Empty
                };

                wells.Add(well);
            }

            return wells;
        }
        //-------------------------Методы контекстного меню------------------------------------------------
        // 1. Метод журнал событий
        public DataTable GetEventJournal(string wellId)
        {
            string sql = Queries.GetEventJournal(wellId); // Берём SQL из Queries
            var param = new OracleParameter(":wellId", OracleDbType.Varchar2)
            {
                Value = wellId // Передаём ID скважины как параметр
            };
            return _oracleConnector.GetDataTable(sql, param); // Выполняем запрос
        }

        // 2. Метод для событий из пульта
        public DataTable GetPultEvents(string wellId)
        {
            string sql = Queries.GetPultEvents(wellId);
            var param = new OracleParameter(":wellId", OracleDbType.Varchar2) { Value = wellId };
            return _oracleConnector.GetDataTable(sql, param);
        }

        // 3. Метод для истории постановки бригады
        public DataTable GetBrigadeHistory(string wellId)
        {
            string sql = Queries.GetBrigadeHistory(wellId);
            var param = new OracleParameter(":wellId", OracleDbType.Varchar2) { Value = wellId };
            return _oracleConnector.GetDataTable(sql, param);
        }

        // 4. Метод для посуточного нахождения
        public DataTable GetDailyStatus(string wellId)
        {
            string sql = Queries.GetDailyStatus(wellId);
            var param = new OracleParameter(":wellId", OracleDbType.Varchar2) { Value = wellId };
            return _oracleConnector.GetDataTable(sql, param);
        }

        // 5. Метод для планов ГТМ
        public DataTable GetGtmPlans(string wellId)
        {
            string sql = Queries.GetGtmPlans(wellId);
            var param = new OracleParameter(":wellId", OracleDbType.Varchar2) { Value = wellId };
            return _oracleConnector.GetDataTable(sql, param);
        }

        //------------------------Методы проверки истории-----------------------------------------------

        public DataPresenceCheck CheckDataPresence(string wellId)
        {
            if (string.IsNullOrWhiteSpace(wellId))
                throw new ArgumentException("ID скважины не может быть пустым");

            try
            {
                var result = new DataPresenceCheck { WellId = wellId };

                // 1. ЖУРНАЛ СОБЫТИЙ (bool вместо int)
                result.HasEventLog = ExecuteBoolCheckQuery(Queries.CheckEventLog(), wellId);

                // 2. СОБЫТИЯ ПУЛЬТА
                result.HasPultEvent = ExecuteBoolCheckQuery(Queries.CheckPultEvent(), wellId);

                // 3. РЕЕСТР ТКРС (активные ремонты)
                result.HasTKRS = ExecuteBoolCheckQuery(Queries.CheckTKRS(), wellId);

                // 4. ПРОВЕРКА БРИГАД (для "требуется удалить ремонт")
                bool hasRigs = ExecuteBoolCheckQuery(Queries.CheckTKRSRigs(), wellId);

                // ЛОГИКА: "Требуется удалить ремонт" = есть бригады И нет активных ремонтов
                result.RequiresDeleteRemont = hasRigs && !result.HasTKRS;

                // 5. ГТМ (последние 30 дней)
                result.HasGTM = ExecuteBoolCheckQuery(Queries.CheckGTM(), wellId);

                // 6. ТЕХНОЛОГИЧЕСКИЕ РАЗРЯДКИ
                result.HasTehnRazr = ExecuteBoolCheckQuery(Queries.CheckTehnRazr(), wellId);

                // Свойство IsReadyToDelete вычислится автоматически из булевых полей
                return result;
            }
            catch (OracleException ex)
            {
                throw new Exception($"Ошибка базы данных при проверке данных: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при проверке данных: {ex.Message}", ex);
            }
        }

        // ВСПОМОГАТЕЛЬНЫЙ МЕТОД: возвращает bool вместо int
        private bool ExecuteBoolCheckQuery(string sql, string wellId)
        {
            var param = new OracleParameter(":wellId", OracleDbType.Varchar2)
            {
                Value = wellId
            };

            DataTable resultTable = _oracleConnector.GetDataTable(sql, param);

            if (resultTable.Rows.Count > 0 && resultTable.Rows[0][0] != DBNull.Value)
            {
                // ПРЕОБРАЗУЕМ ЧИСЛО (0/1) В BOOL
                int intResult = Convert.ToInt32(resultTable.Rows[0][0]);
                return intResult == 1; // 1 → true, 0 → false
            }

            return false; // Если что-то пошло не так, считаем false
        }
    }
}