using System;

namespace SLICE_System.Models
{
    public class AuditEntry
    {
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } // e.g., "SALE", "WASTE"
        public string Description { get; set; }
        public string BranchName { get; set; }
        public string PerformedBy { get; set; }

        // --- UI Helper for XAML Binding ---
        // This calculates the color based on the ActivityType
        public string BadgeColor
        {
            get
            {
                switch (ActivityType?.ToUpper())
                {
                    case "SALE":
                    case "SHIPMENT":
                    case "TRANSFER":
                        return "#27AE60"; // Green
                    case "WASTE":
                    case "DELETE":
                        return "#C0392B"; // Red
                    case "LOGIN":
                    case "UPDATE":
                        return "#2980B9"; // Blue
                    default:
                        return "#95A5A6"; // Gray
                }
            }
        }

        // Helper to match the XAML binding "ActionType" if the XAML uses that name
        // (This acts as an alias so you don't have to change your SQL)
        public string ActionType => ActivityType;
    }
}