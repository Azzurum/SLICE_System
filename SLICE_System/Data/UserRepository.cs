using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class UserRepository
    {
        private readonly DatabaseService _dbService;

        public UserRepository()
        {
            _dbService = new DatabaseService();
        }

        // 1. LOGIN (Existing)
        public User? Login(string username, string password)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "SELECT * FROM Users WHERE Username = @Username AND PasswordHash = @Password AND IsActive = 1";
                return connection.QuerySingleOrDefault<User>(sql, new { Username = username, Password = password });
            }
        }

        // 2. GET ALL USERS (Updated with Search)
        public List<User> GetAllUsers(string search = "")
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT u.*, ISNULL(b.BranchName, 'Headquarters') as BranchName 
                    FROM Users u
                    LEFT JOIN Branches b ON u.BranchID = b.BranchID
                    WHERE (@Search = '' OR u.FullName LIKE @Search OR u.Username LIKE @Search)
                    ORDER BY u.Role, u.FullName";

                return connection.Query<User>(sql, new { Search = "%" + search + "%" }).AsList();
            }
        }

        // 3. ADD USER
        public void AddUser(User user)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO Users (Username, PasswordHash, FullName, Role, BranchID, IsActive)
                    VALUES (@Username, @PasswordHash, @FullName, @Role, @BranchID, 1)";

                connection.Execute(sql, user);
            }
        }

        public void ReactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET IsActive = 1 WHERE UserID = @UserID";
                connection.Execute(sql, new { UserID = userId });
            }
        }

        // 4. DEACTIVATE USER (Soft Delete)
        public void DeactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // We set IsActive to 0 instead of deleting the row
                // This preserves their history in Sales/Audit logs.
                string sql = "UPDATE Users SET IsActive = 0 WHERE UserID = @UserID";
                connection.Execute(sql, new { UserID = userId });
            }
        }
    }
}