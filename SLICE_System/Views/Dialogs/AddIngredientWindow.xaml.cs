using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class AddIngredientWindow : Window
    {
        private readonly DatabaseService _db = new DatabaseService();
        private MasterInventory _existingItem;

        // NEW: Stores the path of the selected image
        private string _uploadedImagePath;

        // 1. CONSTRUCTOR FOR NEW ITEM
        public AddIngredientWindow()
        {
            InitializeComponent();
        }

        // 2. CONSTRUCTOR FOR EDITING EXISTING ITEM
        public AddIngredientWindow(MasterInventory itemToEdit) : this()
        {
            _existingItem = itemToEdit;

            // Populate UI with existing data
            txtName.Text = itemToEdit.ItemName;
            cmbCategory.Text = itemToEdit.Category;
            txtBulk.Text = itemToEdit.BulkUnit;

            // Set Base Unit ComboBox
            foreach (ComboBoxItem item in cmbBase.Items)
            {
                if (item.Content.ToString() == itemToEdit.BaseUnit)
                {
                    cmbBase.SelectedItem = item;
                    break;
                }
            }

            txtRatio.Text = itemToEdit.ConversionRatio.ToString();

            // Load Existing Image if available
            _uploadedImagePath = itemToEdit.ImagePath;
            if (!string.IsNullOrEmpty(_uploadedImagePath) && File.Exists(_uploadedImagePath))
            {
                imgIngredient.Source = new BitmapImage(new Uri(_uploadedImagePath));
            }

            // Update UI Header
            txtHeaderTitle.Text = "Edit Ingredient";
        }

        // --- NEW: UPLOAD IMAGE LOGIC ---
        private void UploadIngredientImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp",
                Title = "Select Ingredient Image"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // Create a local directory in the app folder to store images permanently
                    string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "IngredientImages");
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    // Generate a unique filename so images don't overwrite each other
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(dlg.FileName);
                    string targetPath = Path.Combine(targetDir, fileName);

                    File.Copy(dlg.FileName, targetPath);
                    _uploadedImagePath = targetPath;

                    // Preview the image in the dialog
                    imgIngredient.Source = new BitmapImage(new Uri(targetPath));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to upload image: {ex.Message}", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Ingredient name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtRatio.Text, out decimal ratio))
            {
                MessageBox.Show("Conversion Ratio must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = _db.GetConnection())
                {
                    string category = (cmbCategory.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "General";
                    string baseUnit = (cmbBase.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "g";

                    if (_existingItem == null)
                    {
                        // --- LOGIC: INSERT NEW ---
                        string sql = @"INSERT INTO MasterInventory (ItemName, Category, BulkUnit, BaseUnit, ConversionRatio, ImagePath) 
                                       VALUES (@Name, @Cat, @Bulk, @Base, @Ratio, @Img)";

                        conn.Execute(sql, new
                        {
                            Name = txtName.Text,
                            Cat = category,
                            Bulk = txtBulk.Text,
                            Base = baseUnit,
                            Ratio = ratio,
                            Img = _uploadedImagePath // New property
                        });

                        MessageBox.Show("New ingredient successfully added to the Warehouse.", "Success");
                    }
                    else
                    {
                        // --- LOGIC: UPDATE EXISTING ---
                        string sql = @"UPDATE MasterInventory 
                                       SET ItemName = @Name, Category = @Cat, BulkUnit = @Bulk, BaseUnit = @Base, ConversionRatio = @Ratio, ImagePath = @Img 
                                       WHERE ItemID = @ID";

                        conn.Execute(sql, new
                        {
                            Name = txtName.Text,
                            Cat = category,
                            Bulk = txtBulk.Text,
                            Base = baseUnit,
                            Ratio = ratio,
                            Img = _uploadedImagePath, // New property
                            ID = _existingItem.ItemID
                        });
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Allows dragging of the borderless window
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}