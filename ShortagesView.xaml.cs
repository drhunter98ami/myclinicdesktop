using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyClinic
{
    public partial class ShortagesView : UserControl
    {
        public ObservableCollection<Shortage> UrgentItems { get; set; }
        public ObservableCollection<Shortage> NonUrgentItems { get; set; }

        public ShortagesView()
        {
            InitializeComponent();

            UrgentItems = new ObservableCollection<Shortage>();
            NonUrgentItems = new ObservableCollection<Shortage>();

            LoadData();

            ListUrgent.ItemsSource = UrgentItems;
            ListNonUrgent.ItemsSource = NonUrgentItems;
        }

        private void BtnAddUrgent_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtUrgentInput.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    using var context = new AppDbContext();
                    var shortage = new Shortage
                    {
                        Item = text,
                        IsUrgent = true,
                        CreatedAt = DateTime.Now
                    };
                    context.Shortages.Add(shortage);
                    context.SaveChanges();
                    UrgentItems.Add(shortage);
                    TxtUrgentInput.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء إضافة النقص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtUrgentInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAddUrgent_Click(sender, e);
        }

        private void BtnAddNonUrgent_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtNonUrgentInput.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    using var context = new AppDbContext();
                    var shortage = new Shortage
                    {
                        Item = text,
                        IsUrgent = false,
                        CreatedAt = DateTime.Now
                    };
                    context.Shortages.Add(shortage);
                    context.SaveChanges();
                    NonUrgentItems.Add(shortage);
                    TxtNonUrgentInput.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء إضافة النقص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtNonUrgentInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAddNonUrgent_Click(sender, e);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Shortage itemToRemove)
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
                var shortages = context.Shortages.OrderBy(s => s.CreatedAt).ToList();
                
                UrgentItems.Clear();
                NonUrgentItems.Clear();
                
                foreach (var shortage in shortages)
                {
                    if (shortage.IsUrgent)
                    {
                        UrgentItems.Add(shortage);
                    }
                    else
                    {
                        NonUrgentItems.Add(shortage);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء تحميل النواقص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}