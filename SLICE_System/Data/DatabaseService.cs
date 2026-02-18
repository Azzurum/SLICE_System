using System;
using System.Data;
using Microsoft.Data.SqlClient; // The NuGet package we installed

namespace SLICE_System.Data
{
    public class DatabaseService
    {
        // 1. YOUR CONNECTION STRING
        // NOTE: Replace 'YOUR_USERNAME' and 'YOUR_PASSWORD' with your actual Azure credentials.
        private readonly string _connectionString =
            "Server=tcp:sqlserver-slice-jp.database.windows.net,1433;" +
            "Initial Catalog=sqldb-slice;" +
            "Persist Security Info=False;" +
            "User ID=slice_admin;" +      // <--- PUT USERNAME HERE
            "Password=SL1C3_Engine@2026;" +     // <--- PUT PASSWORD HERE
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
                    connection.Open(); // Tries to knock on the door of the server
                    return true; // If we get here, the door opened!
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