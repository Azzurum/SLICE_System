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

        // FIX: Store ONLY the filename, not the entire C:\ folder path!
        private string _uploadedFileName;

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

            // FIX: Use the FullImagePath property from the model to load the preview
            _uploadedFileName = itemToEdit.ImagePath;
            string fullPath = itemToEdit.FullImagePath;

            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                // CacheOption.OnLoad prevents WPF from locking the file
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(fullPath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                imgIngredient.Source = bmp;
            }

            // Update UI Header
            txtHeaderTitle.Text = "Edit Ingredient";
        }

        // --- UPLOAD IMAGE LOGIC ---
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
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(dlg.FileName);

                    // --- SAVE 1: LIVE APP FOLDER (So UI sees it immediately) ---
                    string liveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "Inventory");
                    if (!Directory.Exists(liveDir)) Directory.CreateDirectory(liveDir);
                    string livePath = Path.Combine(liveDir, fileName);
                    File.Copy(dlg.FileName, livePath, true);

                    // --- SAVE 2: SOURCE CODE FOLDER (For GitHub Tracker!) ---
                    // Goes up 3 folders from bin\Debug\net6.0-windows to reach the project root
                    string projectDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Images", "Inventory");
                    string fullProjectDir = Path.GetFullPath(projectDir);
                    if (Directory.Exists(fullProjectDir))
                    {
                        string projPath = Path.Combine(fullProjectDir, fileName);
                        File.Copy(dlg.FileName, projPath, true);
                    }

                    _uploadedFileName = fileName;

                    // Preview the image in the dialog safely
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(livePath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    imgIngredient.Source = bmp;
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
                            Img = _uploadedFileName // Saves just the filename
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
                            Img = _uploadedFileName, // Saves just the filename
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