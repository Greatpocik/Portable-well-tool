using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace WellTool.Services
{
    public class OracleConnector
    {
        public string ConnectionString { get; } = "User Id=system;Password=admin;Data Source=localhost:1521/xe;";

        public DataTable GetDataTable(string sql, params OracleParameter[] parameters)
        {
            using (var connection = new OracleConnection(ConnectionString))
            using (var command = new OracleCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);

                    // ОТЛАДКА: выводим SQL и параметры
                    Console.WriteLine("=== SQL DEBUG ===");
                    Console.WriteLine($"SQL: {sql}");
                    foreach (OracleParameter p in parameters)
                    {
                        Console.WriteLine($"{p.ParameterName} = '{p.Value}' (Type: {p.OracleDbType})");
                    }
                    Console.WriteLine("=================");
                }

                var dataTable = new DataTable();
                connection.Open();

                using (var reader = command.ExecuteReader())
                    dataTable.Load(reader);
                Console.WriteLine(dataTable);
                return dataTable;
            }
        }
    }
}