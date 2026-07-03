using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyClinic
{
    public partial class LabView : UserControl
    {
        private List<LabWork> _allLabWorks = new List<LabWork>();
        private decimal _usdToSypRate = 15000;

        public LabView()
        {
            InitializeComponent();
            LoadExchangeRate();
            LoadLabNames();
            LoadLabWorks();
            UpdateSummary();
        }

        private void LoadLabNames()
        {
            try
            {
                using var context = new AppDbContext();
                var labNames = context.LabNames.OrderBy(l => l.Name).ToList();
                CmbLabName.ItemsSource = labNames;
                CmbLabName.DisplayMemberPath = "Name";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل أسماء المخابر: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLabWorks()
        {
            try
            {
                using var context = new AppDbContext();
                _allLabWorks = context.LabWorks.OrderByDescending(l => l.DateSent).ToList();
                DgLabWorks.ItemsSource = _allLabWorks;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadExchangeRate()
        {
            try
            {
                using var context = new AppDbContext();
                var settings = context.AppSettings.FirstOrDefault();
                if (settings != null)
                {
                    _usdToSypRate = settings.UsdToSypRate;
                }
                else
                {
                    // Create default settings if not exists
                    var newSettings = new AppSettings { UsdToSypRate = 15000 };
                    context.AppSettings.Add(newSettings);
                    context.SaveChanges();
                    _usdToSypRate = 15000;
                }
                TxtExchangeRate.Text = _usdToSypRate.ToString("N0", CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل سعر الصرف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtExchangeRate.Text = "15000";
            }
        }

        private void BtnSaveExchangeRate_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(TxtExchangeRate.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal rate) && rate > 0)
            {
                try
                {
                    using var context = new AppDbContext();
                    var settings = context.AppSettings.FirstOrDefault();
                    if (settings != null)
                    {
                        settings.UsdToSypRate = rate;
                        settings.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        settings = new AppSettings { UsdToSypRate = rate };
                        context.AppSettings.Add(settings);
                    }
                    context.SaveChanges();
                    _usdToSypRate = rate;
                    UpdateSummary();
                    MessageBox.Show("تم حفظ سعر الصرف بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في حفظ سعر الصرف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("يرجى إدخال سعر صرف صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateSummary()
        {
            try
            {
                using var context = new AppDbContext();
                var labWorks = context.LabWorks.ToList();

                decimal totalPaid = labWorks.Where(l => l.Status == "تم الدفع").Sum(l => l.Cost);
                decimal totalRequired = labWorks.Where(l => l.Status == "تم الإستلام" || l.Status == "تم الدفع").Sum(l => l.Cost);
                decimal remaining = totalRequired - totalPaid;

                TxtTotalPaid.Text = $"${totalPaid:F2}";
                TxtTotalRequired.Text = $"${totalRequired:F2}";
                TxtRemaining.Text = $"${remaining:F2}";

                // Update SYP amounts
                decimal totalPaidSYP = totalPaid * _usdToSypRate;
                decimal totalRequiredSYP = totalRequired * _usdToSypRate;
                decimal remainingSYP = remaining * _usdToSypRate;

                TxtTotalPaidSYP.Text = $"{totalPaidSYP:N0} ل.س";
                TxtTotalRequiredSYP.Text = $"{totalRequiredSYP:N0} ل.س";
                TxtRemainingSYP.Text = $"{remainingSYP:N0} ل.س";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحديث الملخص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddLabName_Click(object sender, RoutedEventArgs e)
        {
            string newLabName = Microsoft.VisualBasic.Interaction.InputBox("أدخل اسم المخبر الجديد:", "إضافة مخبر جديد", "");
            if (!string.IsNullOrWhiteSpace(newLabName))
            {
                try
                {
                    using var context = new AppDbContext();
                    // Check if name already exists
                    if (context.LabNames.Any(l => l.Name == newLabName.Trim()))
                    {
                        MessageBox.Show("هذا الاسم موجود بالفعل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var labName = new LabName { Name = newLabName.Trim() };
                    context.LabNames.Add(labName);
                    context.SaveChanges();
                    LoadLabNames();
                    CmbLabName.SelectedItem = labName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في إضافة المخبر: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnAddWork_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPatientName.Text))
            {
                MessageBox.Show("يرجى إدخال اسم المريض", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(TxtCost.Text, out decimal cost) || cost <= 0)
            {
                MessageBox.Show("يرجى إدخال تكلفة صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new AppDbContext();
                var labWork = new LabWork
                {
                    PatientName = TxtPatientName.Text.Trim(),
                    LabName = CmbLabName.SelectedItem is LabName selectedLab ? selectedLab.Name : null,
                    Cost = cost,
                    Teeth = TxtTeeth.Text?.Trim(),
                    Status = "تم الإرسال",
                    DateSent = DateTime.Now,
                    Notes = TxtNotes.Text?.Trim()
                };

                context.LabWorks.Add(labWork);
                context.SaveChanges();

                // Clear inputs
                TxtPatientName.Clear();
                TxtCost.Clear();
                TxtTeeth.Clear();
                TxtNotes.Clear();
                CmbLabName.SelectedIndex = -1;

                LoadLabWorks();
                UpdateSummary();

                MessageBox.Show("تم إضافة العمل بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في إضافة العمل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMarkReceived_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int id)
            {
                try
                {
                    using var context = new AppDbContext();
                    var labWork = context.LabWorks.FirstOrDefault(l => l.Id == id);
                    if (labWork != null)
                    {
                        labWork.Status = "تم الإستلام";
                        labWork.DateReceived = DateTime.Now;
                        context.SaveChanges();
                        LoadLabWorks();
                        UpdateSummary();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في تحديث الحالة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnMarkPaid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int id)
            {
                try
                {
                    using var context = new AppDbContext();
                    var labWork = context.LabWorks.FirstOrDefault(l => l.Id == id);
                    if (labWork != null)
                    {
                        labWork.Status = "تم الدفع";
                        labWork.DatePaid = DateTime.Now;
                        labWork.AmountPaid = labWork.Cost;
                        context.SaveChanges();
                        LoadLabWorks();
                        UpdateSummary();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في تحديث الحالة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            string searchTerm = TxtSearch.Text?.Trim().ToLower() ?? "";
            string? statusFilter = null;

            if (CmbStatusFilter.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                string content = item.Content.ToString() ?? "";
                if (content != "الكل")
                {
                    statusFilter = content;
                }
            }

            var filtered = _allLabWorks.AsEnumerable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filtered = filtered.Where(l => l.PatientName.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                filtered = filtered.Where(l => l.Status == statusFilter);
            }

            DgLabWorks.ItemsSource = filtered.ToList();
        }
    }
}
