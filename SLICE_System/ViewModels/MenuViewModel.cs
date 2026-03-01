using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SLICE_System.ViewModels
{
    public class MenuViewModel : ViewModelBase
    {
        private readonly MenuRepository _menuRepo;
        private readonly InventoryRepository _inventoryRepo;

        private MenuItem _selectedMenuItem;
        private MasterInventory _selectedIngredient;
        private decimal _ingredientQuantity;
        private string _searchText;
        private List<MenuItem> _allMenuItems;

        // --- PROPERTIES FOR THE UI ---

        // Left Panel: The 39 existing pizzas
        public ObservableCollection<MenuItem> MenuItems { get; set; }

        // Right Panel: The dropdown for raw ingredients (Flour, Cheese, etc.)
        public ObservableCollection<MasterInventory> AvailableIngredients { get; set; }

        // The currently clicked pizza (binds to the Right Panel edit fields)
        public MenuItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value))
                {
                    LoadRecipeForSelected(); // Automatically fetch its recipe when clicked
                    OnPropertyChanged(nameof(IsItemSelected));
                }
            }
        }

        // Hides/Shows the right panel if nothing is selected
        public bool IsItemSelected => SelectedMenuItem != null;

        // Recipe Builder Input Fields
        public MasterInventory SelectedIngredient
        {
            get => _selectedIngredient;
            set
            {
                SetProperty(ref _selectedIngredient, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public decimal IngredientQuantity
        {
            get => _ingredientQuantity;
            set
            {
                SetProperty(ref _ingredientQuantity, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Search Bar for the left panel
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterMenuItems();
            }
        }

        // --- COMMANDS ---
        public ICommand AddIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand SaveMenuCommand { get; }
        public ICommand AddNewMenuCommand { get; }

        public MenuViewModel()
        {
            _menuRepo = new MenuRepository();
            _inventoryRepo = new InventoryRepository();

            MenuItems = new ObservableCollection<MenuItem>();
            AvailableIngredients = new ObservableCollection<MasterInventory>();

            // Initialize Commands
            AddIngredientCommand = new RelayCommand(AddIngredient, CanAddIngredient);
            RemoveIngredientCommand = new RelayCommand<RecipeItemVM>(RemoveIngredient);
            SaveMenuCommand = new RelayCommand(SaveMenu, CanSaveMenu);
            AddNewMenuCommand = new RelayCommand(AddNewMenu);

            LoadData();
        }

        // --- METHODS ---
        private void LoadData()
        {
            // 1. Load Master Inventory (Raw Ingredients)
            var ingredients = _inventoryRepo.GetAllIngredients();
            AvailableIngredients.Clear();
            foreach (var ing in ingredients) AvailableIngredients.Add(ing);

            // 2. Load Menu Items
            _allMenuItems = _menuRepo.GetAllMenuItems();
            FilterMenuItems();
        }

        private void FilterMenuItems()
        {
            MenuItems.Clear();
            var query = string.IsNullOrWhiteSpace(SearchText)
                ? _allMenuItems
                : _allMenuItems.Where(m => m.ProductName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var item in query) MenuItems.Add(item);
        }

        private void LoadRecipeForSelected()
        {
            if (SelectedMenuItem == null) return;

            // If it's an existing product (ID > 0), fetch its recipe from the DB
            if (SelectedMenuItem.ProductID > 0)
            {
                var recipe = _menuRepo.GetRecipeForProduct(SelectedMenuItem.ProductID);
                SelectedMenuItem.Recipe = new ObservableCollection<RecipeItemVM>(recipe);
            }
            else
            {
                // If it's a brand new item, start with an empty list
                SelectedMenuItem.Recipe = new ObservableCollection<RecipeItemVM>();
            }

            // Clear the input fields ready for the next ingredient
            SelectedIngredient = null;
            IngredientQuantity = 0;
        }

        private bool CanAddIngredient() => SelectedMenuItem != null && SelectedIngredient != null && IngredientQuantity > 0;

        private void AddIngredient()
        {
            // Check if ingredient is already in the list to avoid duplicates
            var existing = SelectedMenuItem.Recipe.FirstOrDefault(r => r.IngredientID == SelectedIngredient.ItemID);
            if (existing != null)
            {
                existing.RequiredQty += IngredientQuantity; // Just add to the quantity
            }
            else
            {
                SelectedMenuItem.Recipe.Add(new RecipeItemVM
                {
                    IngredientID = SelectedIngredient.ItemID,
                    ItemName = SelectedIngredient.ItemName,
                    BaseUnit = SelectedIngredient.BaseUnit, // Crucial so the user knows if it's ml or grams
                    RequiredQty = IngredientQuantity
                });
            }

            // Reset inputs
            SelectedIngredient = null;
            IngredientQuantity = 0;
        }

        private void RemoveIngredient(RecipeItemVM item)
        {
            if (item != null && SelectedMenuItem?.Recipe != null)
            {
                SelectedMenuItem.Recipe.Remove(item);
            }
        }

        private void AddNewMenu()
        {
            // Creates a blank slate on the right panel
            SelectedMenuItem = new MenuItem
            {
                ProductName = "New Item",
                BasePrice = 0,
                IsAvailable = true,
                Recipe = new ObservableCollection<RecipeItemVM>()
            };
        }

        private bool CanSaveMenu() => SelectedMenuItem != null && !string.IsNullOrWhiteSpace(SelectedMenuItem.ProductName) && SelectedMenuItem.BasePrice >= 0;

        private void SaveMenu()
        {
            try
            {
                if (SelectedMenuItem.ProductID == 0)
                {
                    _menuRepo.AddMenuItem(SelectedMenuItem);
                    MessageBox.Show("New menu item created successfully!", "Success");
                }
                else
                {
                    // This triggers the "Wipe and Replace" transaction we built in Step 2
                    _menuRepo.UpdateMenuItem(SelectedMenuItem);
                    MessageBox.Show("Menu item and recipe updated successfully!", "Success");
                }

                LoadData(); // Refresh the whole screen
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving menu item: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}