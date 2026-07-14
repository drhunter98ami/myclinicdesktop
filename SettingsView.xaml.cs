using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyClinic
{
    public partial class SettingsView : UserControl
    {
        private readonly ObservableCollection<TreatmentCost> _treatmentCosts = new();

        public SettingsView()
        {
            InitializeComponent();
            TreatmentItemsControl.ItemsSource = _treatmentCosts;
            Loaded += SettingsView_Loaded;
        }

        private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTreatmentsAsync();
            LoadCurrencySettings();
        }

        private void LoadCurrencySettings()
        {
            try
            {
                using var db = new AppDbContext();
                var settings = db.AppSettings.FirstOrDefault();
                if (settings != null)
                {
                    CmbCurrency.SelectedItem = settings.DefaultCurrency;
                }
                else
                {
                    CmbCurrency.SelectedItem = "SYP";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل إعدادات العملة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                CmbCurrency.SelectedItem = "SYP";
            }
        }

        private async Task LoadTreatmentsAsync()
        {
            try
            {
                using var db = new AppDbContext();
                var treatments = await db.TreatmentCosts
                    .OrderBy(t => t.TreatmentName)
                    .ToListAsync();

                _treatmentCosts.Clear();
                foreach (var treatment in treatments)
                {
                    _treatmentCosts.Add(treatment);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل العلاجات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAddTreatment_Click(object sender, RoutedEventArgs e)
        {
            string treatmentName = TxtTreatmentName.Text.Trim();
            string costText = TxtTreatmentCost.Text.Trim();

            if (string.IsNullOrWhiteSpace(treatmentName))
            {
                MessageBox.Show("يرجى إدخال اسم العلاج.", "بيانات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTreatmentName.Focus();
                return;
            }

            if (!decimal.TryParse(costText, out decimal cost) || cost <= 0)
            {
                MessageBox.Show("يرجى إدخال تكلفة صحيحة.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTreatmentCost.Focus();
                TxtTreatmentCost.SelectAll();
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var newTreatment = new TreatmentCost
                {
                    TreatmentName = treatmentName,
                    Cost = cost
                };

                db.TreatmentCosts.Add(newTreatment);
                await db.SaveChangesAsync();

                _treatmentCosts.Add(newTreatment);

                TxtTreatmentName.Clear();
                TxtTreatmentCost.Clear();
                TxtTreatmentName.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في إضافة العلاج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteTreatment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int treatmentId)
            {
                var result = MessageBox.Show(
                    "هل أنت متأكد من حذف هذا العلاج؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var treatment = await db.TreatmentCosts.FindAsync(treatmentId);
                        if (treatment != null)
                        {
                            db.TreatmentCosts.Remove(treatment);
                            await db.SaveChangesAsync();

                            var localTreatment = _treatmentCosts.FirstOrDefault(t => t.Id == treatmentId);
                            if (localTreatment != null)
                            {
                                _treatmentCosts.Remove(localTreatment);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"خطأ في حذف العلاج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void CmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbCurrency.SelectedItem is string selectedCurrency)
            {
                try
                {
                    using var db = new AppDbContext();
                    var settings = db.AppSettings.FirstOrDefault();
                    if (settings != null)
                    {
                        settings.DefaultCurrency = selectedCurrency;
                        settings.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        settings = new AppSettings { DefaultCurrency = selectedCurrency };
                        db.AppSettings.Add(settings);
                    }
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في حفظ إعدادات العملة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
