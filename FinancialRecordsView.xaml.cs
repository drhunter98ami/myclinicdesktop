using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyClinic
{
    public partial class FinancialRecordsView : UserControl
    {
        private readonly ObservableCollection<IncomeRowModel> _incomeRows = new();
        private readonly ObservableCollection<ExpenseRowModel> _expenseRows = new();
        private readonly ObservableCollection<UpcomingPaymentRowModel> _upcomingPayments = new();
        
        private readonly ObservableCollection<PatientSuggestionModel> _patientSuggestions = new();
        private readonly ObservableCollection<PatientHistoryModel> _patientHistory = new();
        private readonly ObservableCollection<UnpaidAccountModel> _unpaidAccounts = new();
        
        private readonly List<VisitFinanceSnapshot> _allVisits = new();
        private readonly List<ExpenseSnapshot> _allExpenses = new();

        private FinancialRangeMode _rangeMode = FinancialRangeMode.Month;
        private bool _refreshRequested = true;
        private bool _hasLoadedData;

        // Tracks the patient currently opened in the search overlay
        private int? _selectedPatientSearchId; 

        public FinancialRecordsView()
        {
            InitializeComponent();

            IncomeItemsControl.ItemsSource = _incomeRows;
            ExpenseItemsControl.ItemsSource = _expenseRows;
            UpcomingItemsControl.ItemsSource = _upcomingPayments;
            
            LstPatientSuggestions.ItemsSource = _patientSuggestions;
            PatientHistoryItemsControl.ItemsSource = _patientHistory;
            UnpaidItemsControl.ItemsSource = _unpaidAccounts;

            DpFilterDate.SelectedDate = GetSyrianTime().Date;

            SetRangeMode(FinancialRangeMode.Month);
            
            this.Loaded += FinancialRecordsView_Loaded;
            this.Unloaded += FinancialRecordsView_Unloaded;
            this.IsVisibleChanged += FinancialRecordsView_IsVisibleChanged;
        }

        private static DateTime GetSyrianTime()
        {
            return DateTime.Now.AddHours(7);
        }

        private void FinancialRecordsView_Loaded(object sender, RoutedEventArgs e)
        {
            GlobalEvents.OnFinancialRecordAdded -= GlobalEvents_OnFinancialRecordAdded;
            GlobalEvents.OnFinancialRecordAdded += GlobalEvents_OnFinancialRecordAdded;
            RefreshFinancialData();
        }

        private void FinancialRecordsView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                RefreshFinancialData();
            }
        }

        private void FinancialRecordsView_Unloaded(object sender, RoutedEventArgs e)
        {
            GlobalEvents.OnFinancialRecordAdded -= GlobalEvents_OnFinancialRecordAdded;
        }

        private void GlobalEvents_OnFinancialRecordAdded()
        {
            Dispatcher.Invoke(() =>
            {
                RefreshFinancialData();
            });
        }

        public async void RefreshFinancialData()
        {
            RequestRefresh();
            await EnsureDataCurrentAsync();
        }

        public void RequestRefresh()
        {
            _refreshRequested = true;
        }

        public async Task EnsureDataCurrentAsync()
        {
            if (!_refreshRequested && _hasLoadedData) return;
            
            _refreshRequested = false;
            SetLoadingState(true);

            try
            {
                _incomeRows.Clear();
                _expenseRows.Clear();
                _allVisits.Clear(); 
                _allExpenses.Clear();

                using (var context = new AppDbContext())
                {
                    var loadedVisits = await context.Visits
                                                   .Include(v => v.Patient)
                                                   .AsNoTracking() 
                                                   .OrderByDescending(v => v.VisitDate)
                                                   .ToListAsync();

                    HashSet<int> loadedVisitIds = loadedVisits
                        .Select(visit => visit.Id)
                        .ToHashSet();

                    List<Visit> mergedVisits = loadedVisits
                        .Concat(AppRuntimeCache.RecentVisits.Where(visit => !loadedVisitIds.Contains(visit.Id)))
                        .OrderByDescending(visit => visit.VisitDate)
                        .ToList();

                    var visits = mergedVisits.Select(visit => new VisitFinanceSnapshot
                    {
                        VisitId = visit.Id,
                        VisitDate = visit.VisitDate,
                        PatientName = string.IsNullOrWhiteSpace(visit.Patient?.FullName) ? "مريض بدون اسم" : visit.Patient!.FullName.Trim(),
                        PhoneNumber = visit.Patient?.PhoneNumber ?? string.Empty,
                        CurrentCost = visit.CurrentCost,
                        TodayPaid = visit.TodayPaid,
                        RemainingAmount = visit.RemainingAmount,
                        SelectedTreatmentsJson = visit.SelectedTreatmentsJson
                    }).ToList();

                    _allVisits.AddRange(visits);

                    List<ExpenseEntry> loadedExpenses = await context.Expenses
                        .AsNoTracking()
                        .OrderByDescending(expense => expense.ExpenseDate)
                        .ToListAsync();

                    var expenses = loadedExpenses.Select(expense => new ExpenseSnapshot
                    {
                        ExpenseDate = expense.ExpenseDate,
                        Description = expense.Description,
                        Amount = expense.Amount
                    }).ToList();

                    _allExpenses.AddRange(expenses);
                    
                    // تحميل الدفعات القادمة
                    var loadedUpcoming = await context.UpcomingPayments
                        .AsNoTracking()
                        .ToListAsync();
                        
                    var upcomingModels = loadedUpcoming.Select(u => new UpcomingPaymentRowModel
                    {
                        Id = u.Id,
                        Description = u.Description,
                        RawAmount = u.Amount,
                        AmountText = FormatMoney(u.Amount)
                    }).ToList();
                    
                    ReplaceCollection(_upcomingPayments, upcomingModels);
                    EmptyUpcomingText.Visibility = _upcomingPayments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                _hasLoadedData = true;
                ApplyFilters();

                // التحديث التلقائي الفوري لقائمة الديون لضمان دقة "المتبقي" بأي وقت يحصل فيه تغيير بالحسابات
                await LoadUnpaidAccounts();
            }
            catch (Exception)
            {
                _allVisits.Clear();
                _allExpenses.Clear();
                ApplyFilters();
                MessageBox.Show("تعذر تحميل السجل المالي حالياً.", "السجل المالي", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void DpFilterDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void DpFilterDate_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (sender is DatePicker datePicker)
            {
                var popup = datePicker.Template.FindName("PART_Popup", datePicker) as System.Windows.Controls.Primitives.Popup;
                if (popup != null && popup.Child is System.Windows.Controls.Calendar calendar)
                {
                    calendar.DisplayModeChanged -= Calendar_DisplayModeChanged;
                    calendar.DisplayModeChanged += Calendar_DisplayModeChanged;

                    if (_rangeMode == FinancialRangeMode.Month)
                        calendar.DisplayMode = CalendarMode.Year; 
                    else if (_rangeMode == FinancialRangeMode.Year)
                        calendar.DisplayMode = CalendarMode.Decade; 
                }
            }
        }

        private void Calendar_DisplayModeChanged(object sender, CalendarModeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Calendar calendar)
            {
                if (_rangeMode == FinancialRangeMode.Month && calendar.DisplayMode == CalendarMode.Month)
                {
                    DpFilterDate.SelectedDate = calendar.DisplayDate;
                    DpFilterDate.IsDropDownOpen = false;
                }
                else if (_rangeMode == FinancialRangeMode.Year && calendar.DisplayMode == CalendarMode.Year)
                {
                    DpFilterDate.SelectedDate = calendar.DisplayDate;
                    DpFilterDate.IsDropDownOpen = false;
                }
            }
        }

        private void BtnDayRange_Click(object sender, RoutedEventArgs e) => SetRangeMode(FinancialRangeMode.Day);
        private void BtnMonthRange_Click(object sender, RoutedEventArgs e) => SetRangeMode(FinancialRangeMode.Month);
        private void BtnYearRange_Click(object sender, RoutedEventArgs e) => SetRangeMode(FinancialRangeMode.Year);

        private void BtnCurrentDate_Click(object sender, RoutedEventArgs e)
        {
            DpFilterDate.SelectedDate = GetSyrianTime().Date;
        }

        private async void BtnAddExpense_Click(object sender, RoutedEventArgs e)
        {
            string description = TxtExpenseDescription.Text.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show("يرجى إدخال تفاصيل المصروف.", "المصاريف", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtExpenseDescription.Focus();
                return;
            }

            if (!TryParseMoney(TxtExpenseAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("يرجى إدخال قيمة صحيحة للمصروف.", "المصاريف", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtExpenseAmount.Focus();
                TxtExpenseAmount.SelectAll();
                return;
            }

            DateTime expenseDate = GetSyrianTime();

            try
            {
                ExpenseEntry newExpense = new()
                {
                    ExpenseDate = expenseDate,
                    Description = description,
                    Amount = (double)amount
                };

                using var db = new AppDbContext();
                db.Expenses.Add(newExpense);
                await db.SaveChangesAsync();

                _allExpenses.Add(new ExpenseSnapshot
                {
                    ExpenseDate = newExpense.ExpenseDate,
                    Description = newExpense.Description,
                    Amount = newExpense.Amount
                });

                if (DpFilterDate.SelectedDate?.Date != expenseDate.Date)
                {
                    DpFilterDate.SelectedDate = expenseDate.Date;
                }

                TxtExpenseDescription.Clear();
                TxtExpenseAmount.Clear();

                ApplyFilters();
            }
            catch (Exception)
            {
                MessageBox.Show("تعذر حفظ المصروف حالياً.", "المصاريف", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // --- Upcoming Payments Logic ---
        
        private async void BtnAddUpcoming_Click(object sender, RoutedEventArgs e)
        {
            string desc = TxtUpcomingDescription.Text.Trim();
            if (string.IsNullOrWhiteSpace(desc))
            {
                MessageBox.Show("يرجى إدخال تفاصيل الدفعة.", "دفعات قادمة", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUpcomingDescription.Focus();
                return;
            }

            if (!TryParseMoney(TxtUpcomingAmount.Text, out decimal amt) || amt <= 0)
            {
                MessageBox.Show("يرجى إدخال قيمة صحيحة للدفعة.", "دفعات قادمة", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUpcomingAmount.Focus();
                TxtUpcomingAmount.SelectAll();
                return;
            }
            
            try
            {
                using var db = new AppDbContext();
                var newUpcoming = new UpcomingPayment
                {
                    Description = desc,
                    Amount = (double)amt
                };
                
                db.UpcomingPayments.Add(newUpcoming);
                await db.SaveChangesAsync();

                _upcomingPayments.Add(new UpcomingPaymentRowModel
                {
                    Id = newUpcoming.Id,
                    Description = newUpcoming.Description,
                    RawAmount = newUpcoming.Amount,
                    AmountText = FormatMoney(newUpcoming.Amount)
                });
                
                TxtUpcomingDescription.Clear();
                TxtUpcomingAmount.Clear();
                EmptyUpcomingText.Visibility = Visibility.Collapsed;
            }
            catch (Exception)
            {
                MessageBox.Show("تعذر إضافة الدفعة. تأكد من إعداد جدول UpcomingPayment في قاعدة البيانات.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnPayUpcoming_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UpcomingPaymentRowModel item)
            {
                try
                {
                    using var db = new AppDbContext();
                    
                    var upcoming = await db.UpcomingPayments.FindAsync(item.Id);
                    if (upcoming != null)
                    {
                        db.UpcomingPayments.Remove(upcoming);
                    }

                    var newExpense = new ExpenseEntry
                    {
                        ExpenseDate = GetSyrianTime(),
                        Description = item.Description,
                        Amount = item.RawAmount
                    };
                    db.Expenses.Add(newExpense);
                    
                    await db.SaveChangesAsync();

                    _upcomingPayments.Remove(item);
                    EmptyUpcomingText.Visibility = _upcomingPayments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    
                    _allExpenses.Add(new ExpenseSnapshot
                    {
                        ExpenseDate = newExpense.ExpenseDate,
                        Description = newExpense.Description,
                        Amount = newExpense.Amount
                    });
                    
                    ApplyFilters(); // Update expenses lists and totals immediately
                }
                catch (Exception)
                {
                    MessageBox.Show("حدث خطأ أثناء معالجة الدفعة وتحويلها لمصاريف.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnCancelUpcoming_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UpcomingPaymentRowModel item)
            {
                if (MessageBox.Show("هل أنت متأكد من إلغاء وحذف هذه الدفعة نهائياً؟", "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var upcoming = await db.UpcomingPayments.FindAsync(item.Id);
                        if (upcoming != null)
                        {
                            db.UpcomingPayments.Remove(upcoming);
                            await db.SaveChangesAsync();
                        }
                        
                        _upcomingPayments.Remove(item);
                        EmptyUpcomingText.Visibility = _upcomingPayments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("حدث خطأ أثناء إلغاء الدفعة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ApplyFilters()
        {
            DateTime selectedDate = (DpFilterDate.SelectedDate ?? GetSyrianTime().Date).Date;
            
            List<VisitFinanceSnapshot> filteredVisits = _allVisits
                .Where(visit => MatchesRange(visit.VisitDate.Date, selectedDate))
                .OrderByDescending(visit => visit.VisitDate)
                .ToList();

            List<ExpenseSnapshot> filteredExpenses = _allExpenses
                .Where(expense => MatchesRange(expense.ExpenseDate.Date, selectedDate))
                .OrderByDescending(expense => expense.ExpenseDate)
                .ToList();

            List<IncomeRowModel> incomeRows = filteredVisits
                .Where(visit => visit.TodayPaid > 0)
                .Select(visit => new IncomeRowModel
                {
                    VisitId = visit.VisitId,
                    DateText = visit.VisitDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    TimeText = visit.VisitDate.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    PatientName = visit.PatientName,
                    PhoneNumber = visit.PhoneNumber,
                    RawAmount = visit.TodayPaid,
                    PaidAmountText = "+" + FormatMoneyCompact(visit.TodayPaid),
                    SelectedTreatments = ParseSelectedTreatments(visit.SelectedTreatmentsJson)
                })
                .ToList();

            List<ExpenseRowModel> expenseRows = filteredExpenses
                .Select(expense => new ExpenseRowModel
                {
                    DateText = expense.ExpenseDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    TimeText = expense.ExpenseDate.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    Description = expense.Description,
                    AmountText = "-" + FormatMoneyCompact(expense.Amount),
                    RawAmount = expense.Amount
                })
                .ToList();

            ReplaceCollection(_incomeRows, incomeRows);
            ReplaceCollection(_expenseRows, expenseRows);

            double totalIncome = incomeRows.Sum(item => item.RawAmount);
            double totalExpenses = expenseRows.Sum(item => item.RawAmount);
            double netAmount = totalIncome - totalExpenses;

            TxtTotalIncome.Text = FormatMoney(totalIncome);
            TxtTotalExpenses.Text = FormatMoney(totalExpenses);
            TxtNetAmount.Text = FormatMoney(netAmount);
            TxtNetAmount.Foreground = netAmount < 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C81E1E"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2447E4"));

            TxtVisitCount.Text = filteredVisits.Count.ToString(CultureInfo.CurrentCulture);
            TxtIncomeFooterTotal.Text = FormatMoney(totalIncome);
            TxtExpenseFooterTotal.Text = FormatMoney(totalExpenses);

            EmptyIncomeText.Visibility = _incomeRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyExpenseText.Visibility = _expenseRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            UpdateCurrentDateButton();
        }

        private void UpdateCurrentDateButton()
        {
            if (BtnCurrentDate == null || DpFilterDate == null) return;

            DateTime selectedDate = (DpFilterDate.SelectedDate ?? GetSyrianTime().Date).Date;
            DateTime today = GetSyrianTime().Date;

            bool isDifferent = false;
            string buttonText = "";

            switch (_rangeMode)
            {
                case FinancialRangeMode.Day:
                    isDifferent = selectedDate.Date != today.Date;
                    buttonText = "اليوم الحالي";
                    break;
                case FinancialRangeMode.Month:
                    isDifferent = selectedDate.Year != today.Year || selectedDate.Month != today.Month;
                    buttonText = "الشهر الحالي";
                    break;
                case FinancialRangeMode.Year:
                    isDifferent = selectedDate.Year != today.Year;
                    buttonText = "السنة الحالية";
                    break;
            }

            BtnCurrentDate.Content = buttonText;
            BtnCurrentDate.Visibility = isDifferent ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetRangeMode(FinancialRangeMode mode)
        {
            _rangeMode = mode;

            ApplyRangeButtonState(BtnDayRange, mode == FinancialRangeMode.Day);
            ApplyRangeButtonState(BtnMonthRange, mode == FinancialRangeMode.Month);
            ApplyRangeButtonState(BtnYearRange, mode == FinancialRangeMode.Year);

            ApplyFilters();
        }

        private static void ApplyRangeButtonState(Button button, bool isActive)
        {
            button.Background = isActive
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"))
                : Brushes.Transparent;
            button.Foreground = isActive
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C6C82"));
            button.BorderBrush = isActive
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9E5FF"))
                : Brushes.Transparent;
            button.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);
        }

        private bool MatchesRange(DateTime recordDate, DateTime selectedDate)
        {
            return _rangeMode switch
            {
                FinancialRangeMode.Day => recordDate.Date == selectedDate.Date,
                FinancialRangeMode.Month => recordDate.Year == selectedDate.Year && recordDate.Month == selectedDate.Month,
                FinancialRangeMode.Year => recordDate.Year == selectedDate.Year,
                _ => true
            };
        }

        private void SetLoadingState(bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Unpaid Accounts Overlay Logic ---

        private async void BtnOpenUnpaidOverlay_Click(object sender, RoutedEventArgs e)
        {
            UnpaidAccountsOverlay.Visibility = Visibility.Visible;
            await LoadUnpaidAccounts();
        }

        private void BtnCloseUnpaidOverlay_Click(object sender, RoutedEventArgs e)
        {
            UnpaidAccountsOverlay.Visibility = Visibility.Collapsed;
        }

        private async Task LoadUnpaidAccounts()
        {
            try
            {
                using var db = new AppDbContext();
                var allVisits = await db.Visits.Include(v => v.Patient).AsNoTracking().ToListAsync();

                var unpaidList = allVisits
                    .Where(v => v.Patient != null)
                    .GroupBy(v => v.PatientId)
                    .Select(g => {
                        var patient = g.First().Patient!;
                        double totalPositiveCost = g.Where(v => v.CurrentCost > 0).Sum(v => v.CurrentCost);
                        double totalForgiven = g.Where(v => v.CurrentCost < 0).Sum(v => Math.Abs(v.CurrentCost));
                        double totalPaid = g.Sum(v => v.TodayPaid);
                        
                        double remaining = Math.Round(totalPositiveCost - totalPaid - totalForgiven, 2);

                        double runningBalance = 0;
                        DateTime? activeDebtStartDate = null;

                        foreach (var v in g.OrderBy(visit => visit.VisitDate))
                        {
                            double visitImpact = v.CurrentCost - v.TodayPaid;
                            runningBalance = Math.Round(runningBalance + visitImpact, 2);

                            if (runningBalance > 0)
                            {
                                if (activeDebtStartDate == null)
                                {
                                    activeDebtStartDate = v.VisitDate;
                                }
                            }
                            else
                            {
                                activeDebtStartDate = null;
                            }
                        }

                        DateTime firstDebtDate = activeDebtStartDate ?? g.Min(v => v.VisitDate);

                        return new UnpaidAccountModel
                        {
                            PatientName = patient.FullName,
                            PhoneNumber = patient.PhoneNumber ?? "",
                            TotalCost = totalPositiveCost,
                            TotalPaid = totalPaid,
                            TotalForgiven = totalForgiven,
                            RemainingAmount = remaining,
                            FirstDebtDate = firstDebtDate
                        };
                    })
                    .Where(u => u.RemainingAmount > 0)
                    .ToList();

                if (CmbUnpaidSort != null)
                {
                    int sortIndex = CmbUnpaidSort.SelectedIndex;
                    if (sortIndex == 0) // Highest Amount
                        unpaidList = unpaidList.OrderByDescending(u => u.RemainingAmount).ToList();
                    else if (sortIndex == 1) // Alphabetical
                        unpaidList = unpaidList.OrderBy(u => u.PatientName).ToList();
                    else if (sortIndex == 2) // Oldest
                        unpaidList = unpaidList.OrderBy(u => u.FirstDebtDate).ToList();
                }

                ReplaceCollection(_unpaidAccounts, unpaidList);
                EmptyUnpaidText.Visibility = _unpaidAccounts.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception)
            {
                MessageBox.Show("حدث خطأ أثناء تحميل سجل الديون.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbUnpaidSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UnpaidAccountsOverlay != null && UnpaidAccountsOverlay.Visibility == Visibility.Visible)
            {
                await LoadUnpaidAccounts();
            }
        }


        // --- Patient Financial Search Overlay Logic ---
        
        private void BtnOpenSearchOverlay_Click(object sender, RoutedEventArgs e)
        {
            _selectedPatientSearchId = null;
            PatientSearchOverlay.Visibility = Visibility.Visible;
            TxtPatientSearchInput.Clear();
            TxtAddPaymentAmount.Clear();
            TxtAddForgivenessAmount.Clear();
            
            _patientSuggestions.Clear();
            _patientHistory.Clear();
            SuggestionsPopupBorder.Visibility = Visibility.Collapsed;
            EmptyPatientHistoryText.Visibility = Visibility.Visible;
            PatientHistoryFooter.Visibility = Visibility.Collapsed;
            TxtPatientSearchInput.Focus();
        }

        private void BtnCloseSearchOverlay_Click(object sender, RoutedEventArgs e)
        {
            PatientSearchOverlay.Visibility = Visibility.Collapsed;
        }

        private async void TxtPatientSearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtPatientSearchInput.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(query))
            {
                _patientSuggestions.Clear();
                SuggestionsPopupBorder.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var matchingPatients = await db.Patients
                    .AsNoTracking()
                    .Where(p => p.FullName.Contains(query) || (p.PhoneNumber != null && p.PhoneNumber.Contains(query)))
                    .Take(10)
                    .Select(p => new PatientSuggestionModel
                    {
                        Id = p.Id,
                        DisplayName = p.FullName + (!string.IsNullOrWhiteSpace(p.PhoneNumber) ? $" ({p.PhoneNumber})" : "")
                    })
                    .ToListAsync();

                ReplaceCollection(_patientSuggestions, matchingPatients);
                SuggestionsPopupBorder.Visibility = _patientSuggestions.Any() ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception)
            {
                // Handle silent fail for live search
            }
        }

        private async void LstPatientSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstPatientSuggestions.SelectedItem is not PatientSuggestionModel selectedPatient)
                return;

            _selectedPatientSearchId = selectedPatient.Id;
            SuggestionsPopupBorder.Visibility = Visibility.Collapsed;
            TxtPatientSearchInput.Text = selectedPatient.DisplayName;
            TxtPatientSearchInput.SelectionStart = TxtPatientSearchInput.Text.Length;

            await LoadPatientFinancialHistory(selectedPatient.Id);
        }

        private async Task LoadPatientFinancialHistory(int patientId)
        {
            try
            {
                using var db = new AppDbContext();
                
                var dbVisits = await db.Visits
                    .AsNoTracking()
                    .Where(v => v.PatientId == patientId)
                    .OrderByDescending(v => v.VisitDate)
                    .ToListAsync();

                double totalCost = dbVisits.Where(v => v.CurrentCost > 0).Sum(v => v.CurrentCost);
                double totalForgiven = dbVisits.Where(v => v.CurrentCost < 0).Sum(v => Math.Abs(v.CurrentCost));
                double totalPaid = dbVisits.Sum(v => v.TodayPaid);
                double totalRemaining = totalCost - totalPaid - totalForgiven;

                TxtPatientTotalCost.Text = $"إجمالي التكلفة: {FormatMoneyCompact(totalCost)}";
                TxtPatientTotalPaid.Text = $"إجمالي الدفعات: {FormatMoneyCompact(totalPaid)}";
                TxtPatientTotalForgiven.Text = $"إجمالي المسامحة: {FormatMoneyCompact(totalForgiven)}";
                TxtPatientTotalRemaining.Text = $"المتبقي: {FormatMoneyCompact(totalRemaining)}";

                var history = dbVisits.Select(v => new PatientHistoryModel
                {
                    DateText = v.VisitDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    TimeText = v.VisitDate.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    CostText = v.CurrentCost < 0 ? $"مسامحة: {FormatMoneyCompact(Math.Abs(v.CurrentCost))}" : FormatMoney(v.CurrentCost),
                    CostColor = v.CurrentCost < 0 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                    PaidText = FormatMoney(v.TodayPaid)
                }).ToList();

                ReplaceCollection(_patientHistory, history);
                
                bool hasHistory = _patientHistory.Any();
                EmptyPatientHistoryText.Visibility = hasHistory ? Visibility.Collapsed : Visibility.Visible;
                
                PatientHistoryFooter.Visibility = Visibility.Visible;
                
                if(!hasHistory)
                {
                    EmptyPatientHistoryText.Text = "لا توجد سجلات مالية سابقة لهذا المريض. يمكنك إضافة دفعة أدناه.";
                }
            }
            catch (Exception)
            {
                MessageBox.Show("تعذر تحميل السجل المالي للمريض.", "بحث مريض", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAddPatientPayment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPatientSearchId == null) return;

            bool isPaymentEntered = TryParseMoney(TxtAddPaymentAmount.Text, out decimal paymentAmount);
            bool isForgivenessEntered = TryParseMoney(TxtAddForgivenessAmount.Text, out decimal forgivenessAmount);

            if (!isPaymentEntered && !isForgivenessEntered)
            {
                MessageBox.Show("يرجى إدخال قيمة للدفعة أو للمسامحة.", "إضافة دفعة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if ((isPaymentEntered && paymentAmount < 0) || (isForgivenessEntered && forgivenessAmount < 0))
            {
                MessageBox.Show("لا يمكن إدخال قيم سالبة.", "إضافة دفعة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime paymentDateTime = GetSyrianTime();

            try
            {
                using var db = new AppDbContext();
                
                var newPaymentVisit = new Visit
                {
                    PatientId = _selectedPatientSearchId.Value,
                    VisitDate = paymentDateTime,
                    CurrentCost = isForgivenessEntered ? -(double)forgivenessAmount : 0, 
                    TodayPaid = isPaymentEntered ? (double)paymentAmount : 0,
                    RemainingAmount = 0 
                };

                db.Visits.Add(newPaymentVisit);
                await db.SaveChangesAsync();

                // Clear inputs
                TxtAddPaymentAmount.Clear();
                TxtAddForgivenessAmount.Clear();

                // Refresh the overlay
                await LoadPatientFinancialHistory(_selectedPatientSearchId.Value);

                RequestRefresh();
                await EnsureDataCurrentAsync();
            }
            catch (Exception)
            {
                MessageBox.Show("حدث خطأ أثناء إضافة الدفعة. يرجى المحاولة لاحقاً.", "إضافة دفعة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (T item in items)
            {
                target.Add(item);
            }
        }

        private static bool TryParseMoney(string? value, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string trimmedValue = value!.Trim(); 
            return decimal.TryParse(trimmedValue, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ||
                   decimal.TryParse(trimmedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        private void BtnIncomeDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: IncomeRowModel row })
                return;

            TxtIncomeTreatmentsPatient.Text = row.PatientName;
            TxtIncomeTreatmentsDate.Text = $"{row.DateText}  {row.TimeText}";

            if (row.HasSelectedTreatments)
            {
                IncomeTreatmentsItemsControl.ItemsSource = row.SelectedTreatments;
                EmptyIncomeTreatmentsText.Visibility = Visibility.Collapsed;
                IncomeTreatmentsItemsControl.Visibility = Visibility.Visible;
            }
            else
            {
                IncomeTreatmentsItemsControl.ItemsSource = null;
                IncomeTreatmentsItemsControl.Visibility = Visibility.Collapsed;
                EmptyIncomeTreatmentsText.Visibility = Visibility.Visible;
            }

            IncomeTreatmentsOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseIncomeTreatmentsOverlay_Click(object sender, RoutedEventArgs e)
        {
            IncomeTreatmentsOverlay.Visibility = Visibility.Collapsed;
        }

        private void IncomeTreatmentsDimmer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            IncomeTreatmentsOverlay.Visibility = Visibility.Collapsed;
        }

        private void Border_MouseLeftButtonDown_StopPropagation(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private static IReadOnlyList<SelectedTreatment> ParseSelectedTreatments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<SelectedTreatment>();
            try
            {
                var items = JsonSerializer.Deserialize<List<SelectedTreatment>>(json);
                return items?.Where(t => !string.IsNullOrWhiteSpace(t.TreatmentName)).ToList()
                       ?? (IReadOnlyList<SelectedTreatment>)Array.Empty<SelectedTreatment>();
            }
            catch (JsonException)
            {
                return Array.Empty<SelectedTreatment>();
            }
        }

        private static string FormatMoney(double amount)
        {
            return $"{amount.ToString("0.##", CultureInfo.CurrentCulture)} ل.س";
        }

        private static string FormatMoneyCompact(double amount)
        {
            return amount.ToString("0.##", CultureInfo.CurrentCulture);
        }

        private enum FinancialRangeMode { Day, Month, Year }

        private sealed class VisitFinanceSnapshot
        {
            public int VisitId { get; init; }
            public DateTime VisitDate { get; init; }
            public string PatientName { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public double CurrentCost { get; init; }
            public double TodayPaid { get; init; }
            public double RemainingAmount { get; init; }
            public string? SelectedTreatmentsJson { get; init; }
        }

        private sealed class ExpenseSnapshot
        {
            public DateTime ExpenseDate { get; init; }
            public string Description { get; init; } = string.Empty;
            public double Amount { get; init; }
        }

        public sealed class IncomeRowModel
        {
            public int VisitId { get; init; }
            public string DateText { get; init; } = string.Empty;
            public string TimeText { get; init; } = string.Empty;
            public string PatientName { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public string PaidAmountText { get; init; } = string.Empty;
            public double RawAmount { get; init; }
            public IReadOnlyList<SelectedTreatment> SelectedTreatments { get; init; } = Array.Empty<SelectedTreatment>();
            public bool HasSelectedTreatments => SelectedTreatments.Count > 0;
        }

        public sealed class ExpenseRowModel
        {
            public string DateText { get; init; } = string.Empty;
            public string TimeText { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string AmountText { get; init; } = string.Empty;
            public double RawAmount { get; init; }
        }

        public sealed class UpcomingPaymentRowModel
        {
            public int Id { get; init; }
            public string Description { get; init; } = string.Empty;
            public string AmountText { get; init; } = string.Empty;
            public double RawAmount { get; init; }
        }

        public sealed class PatientSuggestionModel
        {
            public int Id { get; init; }
            public string DisplayName { get; init; } = string.Empty;
        }

        public sealed class PatientHistoryModel
        {
            public string DateText { get; init; } = string.Empty;
            public string TimeText { get; init; } = string.Empty;
            public string CostText { get; init; } = string.Empty;
            public SolidColorBrush CostColor { get; init; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            public string PaidText { get; init; } = string.Empty;
        }

        public sealed class UnpaidAccountModel
        {
            public string PatientName { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public double TotalCost { get; init; }
            public double TotalPaid { get; init; }
            public double TotalForgiven { get; init; }
            public double RemainingAmount { get; set; }
            public DateTime FirstDebtDate { get; init; }

            public string RemainingAmountText => $"{RemainingAmount.ToString("0.##", CultureInfo.CurrentCulture)} ل.س";
            public string FirstDebtDateText => FirstDebtDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
    }
}