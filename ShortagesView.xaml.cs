using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyClinic
{
    public partial class ShortagesView : UserControl
    {
        public ObservableCollection<ShortageRowModel> UrgentItems { get; set; }
        public ObservableCollection<ShortageRowModel> NonUrgentItems { get; set; }

        private decimal _usdToSypRate = 15000;

        public ShortagesView()
        {
            InitializeComponent();

            UrgentItems = new ObservableCollection<ShortageRowModel>();
            NonUrgentItems = new ObservableCollection<ShortageRowModel>();

            LoadExchangeRate();
            LoadData();

            ListUrgent.ItemsSource = UrgentItems;
            ListNonUrgent.ItemsSource = NonUrgentItems;

            UrgentItems.CollectionChanged += (_, _) => UpdateTotals();
            NonUrgentItems.CollectionChanged += (_, _) => UpdateTotals();
        }

        private void LoadExchangeRate()
        {
            try
            {
                using var context = new AppDbContext();
                var settings = context.AppSettings.AsNoTracking().FirstOrDefault();
                if (settings != null)
                {
                    _usdToSypRate = settings.UsdToSypRate;
                }
            }
            catch
            {
            }
        }

        private decimal ConvertToSyp(decimal price, string currency)
        {
            return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)
                ? price * _usdToSypRate
                : price;
        }

        private void UpdateTotals()
        {
            decimal urgentTotal = UrgentItems.Sum(i => ConvertToSyp(i.Price, i.Currency));
            decimal nonUrgentTotal = NonUrgentItems.Sum(i => ConvertToSyp(i.Price, i.Currency));

            TxtUrgentTotal.Text = $"{urgentTotal.ToString("N0", CultureInfo.CurrentCulture)} ل.س";
            TxtNonUrgentTotal.Text = $"{nonUrgentTotal.ToString("N0", CultureInfo.CurrentCulture)} ل.س";
        }

        private void BtnAddUrgent_Click(object sender, RoutedEventArgs e)
        {
            if (TryAddShortage(TxtUrgentInput, TxtUrgentPrice, CmbUrgentCurrency, isUrgent: true, out ShortageRowModel? row))
            {
                UrgentItems.Add(row);
            }
        }

        private void TxtUrgentInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAddUrgent_Click(sender, e);
        }

        private void BtnAddNonUrgent_Click(object sender, RoutedEventArgs e)
        {
            if (TryAddShortage(TxtNonUrgentInput, TxtNonUrgentPrice, CmbNonUrgentCurrency, isUrgent: false, out ShortageRowModel? row))
            {
                NonUrgentItems.Add(row);
            }
        }

        private void TxtNonUrgentInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAddNonUrgent_Click(sender, e);
        }

        private bool TryAddShortage(TextBox input, TextBox priceInput, ComboBox currencyCombo, bool isUrgent, out ShortageRowModel? row)
        {
            row = null;

            string text = input.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (!decimal.TryParse(priceInput.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal price) || price < 0)
            {
                MessageBox.Show("يرجى إدخال سعر صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                priceInput.Focus();
                return false;
            }

            string currency = (currencyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SYP";

            try
            {
                using var context = new AppDbContext();
                var shortage = new Shortage
                {
                    Item = text,
                    IsUrgent = isUrgent,
                    Price = price,
                    Currency = currency,
                    CreatedAt = DateTime.Now
                };
                context.Shortages.Add(shortage);
                context.SaveChanges();

                row = MapRow(shortage);

                input.Clear();
                priceInput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء إضافة النقص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is ShortageRowModel itemToRemove)
            {
                try
                {
                    using var context = new AppDbContext();
                    var shortage = context.Shortages.FirstOrDefault(s => s.Id == itemToRemove.Id);
                    if (shortage != null)
                    {
                        context.Shortages.Remove(shortage);
                        context.SaveChanges();
                    }

                    if (UrgentItems.Contains(itemToRemove))
                    {
                        UrgentItems.Remove(itemToRemove);
                    }
                    else if (NonUrgentItems.Contains(itemToRemove))
                    {
                        NonUrgentItems.Remove(itemToRemove);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء حذف النقص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadData()
        {
            try
            {
                using var context = new AppDbContext();
                var shortages = context.Shortages.AsNoTracking().OrderBy(s => s.CreatedAt).ToList();

                UrgentItems.Clear();
                NonUrgentItems.Clear();

                foreach (var shortage in shortages)
                {
                    var row = MapRow(shortage);
                    if (shortage.IsUrgent)
                    {
                        UrgentItems.Add(row);
                    }
                    else
                    {
                        NonUrgentItems.Add(row);
                    }
                }

                UpdateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء تحميل النواقص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ShortageRowModel MapRow(Shortage s)
        {
            string priceText = s.Price == 0
                ? "بدون سعر"
                : $"{s.Price.ToString("0.##", CultureInfo.CurrentCulture)} {s.Currency}";

            return new ShortageRowModel
            {
                Id = s.Id,
                Item = s.Item,
                Price = s.Price,
                Currency = s.Currency,
                PriceText = priceText,
                CurrencyBadge = s.Currency
            };
        }
    }

    public sealed class ShortageRowModel
    {
        public int Id { get; init; }
        public string Item { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public string Currency { get; init; } = "SYP";
        public string PriceText { get; init; } = string.Empty;
        public string CurrencyBadge { get; init; } = "SYP";
    }
}
