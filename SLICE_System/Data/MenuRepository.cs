using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class MenuRepository
    {
        private readonly DatabaseService _dbService;

        public MenuRepository() => _dbService = new DatabaseService();

        // 1. GET ALL ITEMS
        public List<MenuItem> GetAllMenuItems()
        {
            using (var connection = _dbService.GetConnection())
            {
                // Order by Availability first, then Name
                string sql = "SELECT * FROM MenuItems ORDER BY IsAvailable DESC, ProductName";
                return connection.Query<MenuItem>(sql).AsList();
            }
        }

        // 2. ADD NEW ITEM
        public void AddMenuItem(MenuItem item)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO MenuItems (ProductName, BasePrice, IsAvailable)
                    VALUES (@ProductName, @BasePrice, @IsAvailable)";
                connection.Execute(sql, item);
            }
        }

        // 3. UPDATE ITEM
        public void UpdateMenuItem(MenuItem item)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    UPDATE MenuItems 
                    SET ProductName = @ProductName, 
                        BasePrice = @BasePrice, 
                        IsAvailable = @IsAvailable
                    WHERE ProductID = @ProductID";
                connection.Execute(sql, item);
            }
        }

        // 4. TOGGLE STATUS (Quick Action)
        public void ToggleStatus(int id, bool newStatus)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE MenuItems SET IsAvailable = @Status WHERE ProductID = @ID";
                connection.Execute(sql, new { Status = newStatus, ID = id });
            }
        }
    }
}