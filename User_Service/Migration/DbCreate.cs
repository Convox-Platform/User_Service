using Dapper;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace User_Service.Migration
{
    public class DbCreate
    {
        public static void CreateDatabase(string connectionString)
        {
            string masterConnectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;";

            using (var connection = new SqlConnection(masterConnectionString))
            {
                string checkDbSql = "SELECT COUNT(*) FROM sys.databases WHERE name = @DbName";

                bool dbExists = connection.ExecuteScalar<int>(checkDbSql, new { DbName = "User_ServiceDB" }) > 0;

                if (!dbExists)
                {
                    connection.Execute("CREATE DATABASE User_ServiceDB;");
                }
            }

            using (var connection = new SqlConnection(connectionString))
            {
                string checkTableSql = "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(@TableName) AND type = 'U'";

                bool tableExists = connection.ExecuteScalar<int>(checkTableSql, new { TableName = "UserProfiles" }) > 0;

                if (tableExists)
                {
                    return;
                }

                string sql = @"CREATE TABLE UserProfiles (
                Id BIGINT PRIMARY KEY,
                Username NVARCHAR(100) NOT NULL,
                DisplayName NVARCHAR(100) NOT NULL,
                Description NVARCHAR(MAX) NULL,
                Img NVARCHAR(MAX) NULL,
                BirthDate DATE NULL,
                Status NVARCHAR(100) NULL,
                StatusUpdatedAt DATETIME2 NULL)";

                connection.Execute(sql);

            }
        }
    }
}
