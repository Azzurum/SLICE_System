using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SLICE_System.Data
{
    public class DatabaseService
    {
        // 1. CONNECTION STRING
        private readonly string _connectionString =
            "Server=tcp:sqlserver-slice-jp-1.database.windows.net,1433;" +
            "Initial Catalog=sqldb-slice;" +
            "Persist Security Info=False;" +
            "User ID=slice_admin;" +
            "Password=SL1C3_Engine@2026;" +
            "MultipleActiveResultSets=False;" +
            "Encrypt=True;" +
            "TrustServerCertificate=False;" +
            "Connection Timeout=30;";

        // 2. METHOD TO GET A CONNECTION
        public IDbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // 3. THE SMOKE TEST METHOD
        public bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                // If it fails, we can see why (Firewall? Password?)
                System.Diagnostics.Debug.WriteLine("Connection Error: " + ex.Message);
                return false;
            }
        }
    }
}