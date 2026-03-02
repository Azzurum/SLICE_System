using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class SuggestionRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        public void AddSuggestion(CustomerSuggestion suggestion)
        {
            using (var conn = _db.GetConnection())
            {
                string sql = @"INSERT INTO CustomerSuggestions (SuggestionType, Description, SubmittedBy, SubmittedDate, Status) 
                               VALUES (@SuggestionType, @Description, @SubmittedBy, GETDATE(), 'Pending')";
                conn.Execute(sql, suggestion);
            }
        }

        public List<CustomerSuggestion> GetAllSuggestions()
        {
            using (var conn = _db.GetConnection())
            {
                // Joins with the Users table so the Owner can see WHO submitted it
                string sql = @"
                    SELECT s.*, u.FullName as SubmitterName 
                    FROM CustomerSuggestions s
                    LEFT JOIN Users u ON s.SubmittedBy = u.UserID
                    ORDER BY s.SubmittedDate DESC";
                return conn.Query<CustomerSuggestion>(sql).ToList();
            }
        }

        public void UpdateSuggestionStatus(int suggestionId, string status, string ownerNotes)
        {
            using (var conn = _db.GetConnection())
            {
                string sql = @"UPDATE CustomerSuggestions 
                               SET Status = @Status, OwnerNotes = @Notes 
                               WHERE SuggestionID = @Id";
                conn.Execute(sql, new { Status = status, Notes = ownerNotes, Id = suggestionId });
            }
        }
    }
}