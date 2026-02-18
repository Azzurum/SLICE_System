using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SLICE_System.Data;

// FIX: Create an alias to avoid confusion with System.Windows.Controls.MenuItem
using MenuModel = SLICE_System.Models.MenuItem;

namespace SLICE_System.Views
{
    public partial class MenuView : UserControl
    {
        private MenuRepository _repo;
        private MenuModel _currentItem; // Uses the alias
        private ObservableCollection<MenuModel> _allItems; // Uses the alias

        public MenuView()
        {
            InitializeComponent();
            _repo = new MenuRepository();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var list = _repo.GetAllMenuItems();
                _allItems = new ObservableCollection<MenuModel>(list);
                icMenuList.ItemsSource = _allItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading menu: " + ex.Message);
            }
        }

        // --- EDITOR PANEL LOGIC ---

        private void OpenEditor(MenuModel item = null)
        {
            if (item == null)
            {
                // ADD MODE
                _currentItem = new MenuModel { IsAvailable = true };
                lblEditorTitle.Text = "Add New Item";
                txtName.Text = "";
                txtPrice.Text = "0.00";
                chkActive.IsChecked = true;
                cmbCategory.SelectedIndex = 0; // Default to Pizza
            }
            else
            {
                // EDIT MODE
                _currentItem = item;
                lblEditorTitle.Text = "Edit Item";

                // Parse Virtual Category
                string category = item.VirtualCategory;
                foreach (ComboBoxItem cbi in cmbCategory.Items)
                {
                    if (cbi.Content.ToString() == category)
                    {
                        cmbCategory.SelectedItem = cbi;
                        break;
                    }
                }

                txtName.Text = item.DisplayName;
                txtPrice.Text = item.BasePrice.ToString("N2");
                chkActive.IsChecked = item.IsAvailable;
            }

            // Animate Slide Out
            DoubleAnimation widthAnim = new DoubleAnimation(300, TimeSpan.FromSeconds(0.3)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            pnlEditor.BeginAnimation(WidthProperty, widthAnim);
        }

        private void CloseEditor_Click(object sender, RoutedEventArgs e)
        {
            // Animate Slide In
            DoubleAnimation widthAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            pnlEditor.BeginAnimation(WidthProperty, widthAnim);
        }

        // --- CRUD ACTIONS ---

        private void AddNew_Click(object sender, RoutedEventArgs e) => OpenEditor(null);

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn.Tag as MenuModel; // Cast using alias
            OpenEditor(item);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(txtName.Text) || !decimal.TryParse(txtPrice.Text, out decimal price))
            {
                MessageBox.Show("Please enter a valid name and price.");
                return;
            }

            if (price <= 0)
            {
                MessageBox.Show("Price must be greater than zero.");
                return;
            }

            try
            {
                // 2. Format Data (Combine Category + Name)
                string category = (cmbCategory.SelectedItem as ComboBoxItem).Content.ToString();
                string fullName = $"{category} | {txtName.Text.Trim()}";

                _currentItem.ProductName = fullName;
                _currentItem.BasePrice = price;
                _currentItem.IsAvailable = chkActive.IsChecked == true;

                // 3. Save to DB
                if (_currentItem.ProductID == 0)
                    _repo.AddMenuItem(_currentItem);
                else
                    _repo.UpdateMenuItem(_currentItem);

                // 4. Refresh & Close
                LoadData();
                CloseEditor_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving: " + ex.Message);
            }
        }

        // --- SEARCH FILTER ---
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allItems == null) return;

            string query = txtSearch.Text.ToLower();
            var filtered = _allItems.Where(i => i.ProductName.ToLower().Contains(query)).ToList();
            icMenuList.ItemsSource = filtered;
        }
    }
}