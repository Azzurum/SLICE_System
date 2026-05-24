using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Configuration; // Add this using statement at the top!

namespace SLICE_System.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            // This line magically reaches into App.config and grabs the string!
            _connectionString = ConfigurationManager.ConnectionStrings["SliceDbConnection"].ConnectionString;
        }

        public IDbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

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
                Console.WriteLine("Connection Error: " + ex.Message);
                return false;
            }
        }
    }
}