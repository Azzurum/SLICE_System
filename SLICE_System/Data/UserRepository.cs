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

        // --- 1. AUTHENTICATION ---
        // Validates user credentials and ensures the account is actively permitted to log in
        public User? Login(string username, string password)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "SELECT * FROM Users WHERE Username = @Username AND PasswordHash = @Password AND IsActive = 1";
                return connection.QuerySingleOrDefault<User>(sql, new { Username = username, Password = password });
            }
        }

        // --- 2. RETRIEVE USERS ---
        // Fetches all users, joins their assigned branch name, and applies an optional search filter
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

        // --- 3. CREATE USER ---
        // Inserts a newly registered employee into the system (defaults to Active = 1)
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

        // --- 4. UPDATE USER ---
        // Modifies an existing employee's details, role, branch assignment, or password
        public void UpdateUser(User user)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    UPDATE Users 
                    SET Username = @Username, 
                        PasswordHash = @PasswordHash, 
                        FullName = @FullName, 
                        Role = @Role, 
                        BranchID = @BranchID
                    WHERE UserID = @UserID";

                connection.Execute(sql, user);
            }
        }

        // --- 5. DEACTIVATE USER (SOFT DELETE) ---
        // Revokes access immediately without deleting the row, keeping financial/audit logs perfectly intact
        public void DeactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET IsActive = 0 WHERE UserID = @UserID";
                connection.Execute(sql, new { UserID = userId });
            }
        }

        // --- 6. REACTIVATE USER ---
        // Restores system access for a previously deactivated employee
        public void ReactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET IsActive = 1 WHERE UserID = @UserID";
                connection.Execute(sql, new { UserID = userId });
            }
        }
    }
}