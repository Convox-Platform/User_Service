using Dapper;
using Npgsql;
using System.Data.Common;

namespace User_Service.Migration
{
    public class DbCreate
    {
        public static void CreateDatabase(string connectionString,string sqlSecretPassword)
        {
            string masterConnectionString = $"Host=localhost;Port=5432;Database=postgres;Username=postgres;Password={sqlSecretPassword};";

            using (var connection = new NpgsqlConnection(masterConnectionString))
            {
                string checkDbSql = "SELECT COUNT(*) FROM pg_database WHERE datname = @DbName";
                var DbName = "User_Service";

                bool dbExists = connection.ExecuteScalar<int>(checkDbSql, new { DbName = DbName }) > 0;

                if (!dbExists)
                {
                    connection.Execute($"CREATE DATABASE \"{DbName}\"");
                }
            }

            using (var connection = new NpgsqlConnection(connectionString))
            {
                string checkTableSql = @"
            SELECT COUNT(*) 
            FROM information_schema.tables 
            WHERE table_schema = 'public' AND table_name = LOWER(@TableName)";

                bool tableExists = connection.ExecuteScalar<int>(checkTableSql, new { TableName = "UserProfiles" }) > 0;

                if (tableExists)
                {
                    return;
                }

                string sql = @"CREATE TABLE UserProfiles (
            Id BIGINT PRIMARY KEY,
            Username VARCHAR(100) NOT NULL,
            DisplayName VARCHAR(100) NOT NULL,
            Description TEXT NULL,
            Img TEXT NULL,
            BirthDate DATE NULL,
            Status VARCHAR(100) NULL,
            StatusUpdatedAt TIMESTAMPTZ NULL)";

                connection.Execute(sql);

            }
        }
    }
}
