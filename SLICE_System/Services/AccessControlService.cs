using System;
using System.Collections.Generic;
using SLICE_System.Models;

namespace SLICE_System.Services
{
    public static class AccessControlService
    {
        // Define all possible modules in the system
        public enum Module
        {
            Dashboard,
            IncomingOrders,
            MyInventory,
            RequestStock,
            SalesPOS,           // Selling items
            ApproveRequests,    // Manager approval
            WasteTracker,
            Reconciliation,     // Stock counting
            MenuRegistry,       // Edit menu items
            GlobalInventory,    // Edit master ingredients
            UserAdmin,          // Add/Edit users
            AuditLogs           // View system history
        }

        // Define which roles can access which modules
        private static readonly Dictionary<string, HashSet<Module>> _rolePermissions = new Dictionary<string, HashSet<Module>>(StringComparer.OrdinalIgnoreCase)
        {
            // SUPER ADMIN: Strategic & Config focus + Operational Auditing
            { "Super-Admin", new HashSet<Module> {
                Module.Dashboard,
                Module.IncomingOrders,
                Module.WasteTracker,
                Module.MyInventory,
                Module.ApproveRequests,
                Module.MenuRegistry,
                Module.GlobalInventory,
                Module.UserAdmin,
                Module.AuditLogs,
                Module.Reconciliation
            }},

            // MANAGER: Operational control for their branch
            { "Manager", new HashSet<Module> {
                Module.Dashboard,
                Module.IncomingOrders,
                Module.MyInventory,
                Module.RequestStock,
                Module.SalesPOS,
                Module.ApproveRequests,
                Module.WasteTracker,
                Module.Reconciliation
                // FIX: Removed MenuRegistry. Managers cannot alter global pricing.
            }},

            // CLERK: Execution only
            { "Clerk", new HashSet<Module> {
                Module.IncomingOrders,
                Module.MyInventory,
                Module.RequestStock,
                Module.SalesPOS,
                Module.WasteTracker
            }}
        };

        // CHECKER FUNCTION
        public static bool CanAccess(string role, Module module)
        {
            if (string.IsNullOrWhiteSpace(role)) return false;

            // Normalize role string just in case
            if (_rolePermissions.ContainsKey(role))
            {
                return _rolePermissions[role].Contains(module);
            }

            return false; // Unknown roles get no access
        }
    }
}