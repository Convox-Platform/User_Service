using Dapper;
using Npgsql;
using System.Data.Common;

namespace User_Service.Migration
{
    public class DbCreate
    {
        public static void CreateDatabase(string connectionString,string sqlSecretPassword)
        {
            var masterBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres",
                Password = sqlSecretPassword
            };
            string masterConnectionString = masterBuilder.ConnectionString;

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

                if (!tableExists)
                {
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

                connection.Execute(@"
                    WITH ranked_usernames AS (
                        SELECT
                            Id,
                            Username,
                            ROW_NUMBER() OVER (
                                PARTITION BY LOWER(Username)
                                ORDER BY Id
                            ) AS username_rank
                        FROM UserProfiles
                    )
                    UPDATE UserProfiles AS profile
                    SET Username = LEFT(ranked.Username, 80) || '-' || ranked.Id::text
                    FROM ranked_usernames AS ranked
                    WHERE profile.Id = ranked.Id
                      AND ranked.username_rank > 1");

                connection.Execute(@"
                    CREATE UNIQUE INDEX IF NOT EXISTS ux_userprofiles_username_normalized
                    ON UserProfiles (LOWER(Username))");

            }
        }
    }
}
