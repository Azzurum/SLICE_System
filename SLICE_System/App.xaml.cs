using System.Configuration;
using System.Data;
using System.Windows;
using SLICE_System.Models; // Required to recognize the User model

namespace SLICE_System
{
    public partial class App : Application
    {
        // This makes the logged-in user accessible from ANYWHERE in your code
        public static User CurrentUser { get; set; }
    }
}