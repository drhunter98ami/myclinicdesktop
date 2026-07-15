using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MyClinic
{
    public partial class StatisticsView : UserControl
    {
        // ── State ─────────────────────────────────────────────────────────────
        private enum MainTab { Income, Expenses }
        private enum SubTab  { Daily, Monthly, Yearly }

        private MainTab _mainTab   = MainTab.Income;
        private SubTab  _subTab    = SubTab.Daily;

        private DateTime _selectedDay   = DateTime.Today;
        private int      _selectedMonth = DateTime.Today.Month;
        private int      _selectedYear  = DateTime.Today.Year;

        // ── Pie colours ───────────────────────────────────────────────────────
        private static readonly string[] SliceColors =
        {
            "#3B82F6","#10B981","#F59E0B","#EF4444","#8B5CF6",
            "#06B6D4","#F97316","#EC4899","#84CC16","#6366F1",
            "#14B8A6","#FB923C","#A855F7","#22C55E","#E11D48"
        };

        // Expense keyword → display label (order matters: first match wins)
        private static readonly (string Keyword, string Label)[] ExpenseKeywords =
        {
            ("مخبر",   "مخبر"),
            ("نواقص",  "نواقص"),
            ("خزان",   "خزان"),
            ("صيانة",  "صيانة"),
            ("أجار",   "أجار"),
            ("أمبير",  "أمبير"),
            ("كهربا",  "كهربا"),
            ("ممرضة",  "ممرضة"),
        };
        private const string OtherLabel = "أخرى";

        // ── Constructor ───────────────────────────────────────────────────────
        public StatisticsView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await RefreshAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Tab / subtab click handlers
        // ═════════════════════════════════════════════════════════════════════

        private async void BtnTabIncome_Click(object sender, RoutedEventArgs e)
        {
            _mainTab = MainTab.Income;
            UpdateTabStyles();
            await RefreshAsync();
        }

        private async void BtnTabExpenses_Click(object sender, RoutedEventArgs e)
        {
            _mainTab = MainTab.Expenses;
            UpdateTabStyles();
            await RefreshAsync();
        }

        private async void BtnDaily_Click(object sender, RoutedEventArgs e)
        {
            _subTab = SubTab.Daily;
            UpdateSubTabStyles();
            await RefreshAsync();
        }

        private async void BtnMonthly_Click(object sender, RoutedEventArgs e)
        {
            _subTab = SubTab.Monthly;
            UpdateSubTabStyles();
            await RefreshAsync();
        }

        private async void BtnYearly_Click(object sender, RoutedEventArgs e)
        {
            _subTab = SubTab.Yearly;
            UpdateSubTabStyles();
            await RefreshAsync();
        }

        // ── Date navigation ───────────────────────────────────────────────────

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            StepDate(-1);
            await RefreshAsync();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            StepDate(+1);
            await RefreshAsync();
        }

        private async void BtnNow_Click(object sender, RoutedEventArgs e)
        {
            _selectedDay   = DateTime.Today;
            _selectedMonth = DateTime.Today.Month;
            _selectedYear  = DateTime.Today.Year;
            await RefreshAsync();
        }

        private void StepDate(int delta)
        {
            switch (_subTab)
            {
                case SubTab.Daily:
                    _selectedDay = _selectedDay.AddDays(delta);
                    break;
                case SubTab.Monthly:
                    var d = new DateTime(_selectedYear, _selectedMonth, 1).AddMonths(delta);
                    _selectedMonth = d.Month;
                    _selectedYear  = d.Year;
                    break;
                case SubTab.Yearly:
                    _selectedYear += delta;
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Main refresh
        // ═════════════════════════════════════════════════════════════════════

        private async Task RefreshAsync()
        {
            UpdateDateLabel();

            if (_mainTab == MainTab.Income)
                await RefreshIncomeAsync();
            else
                await RefreshExpensesAsync();
        }

        // ── Income ────────────────────────────────────────────────────────────

        private async Task RefreshIncomeAsync()
        {
            // 1. Load treatment names from settings (to get the full list)
            List<string> knownTreatments;
            List<Visit>  visits;

            using (var ctx = new AppDbContext())
            {
                knownTreatments = await ctx.TreatmentCosts
                    .AsNoTracking()
                    .Select(t => t.TreatmentName)
                    .ToListAsync();

                var query = ctx.Visits.AsNoTracking().AsQueryable();
                query = ApplyDateFilter(query);
                visits = await query.ToListAsync();
            }

            // 2. Aggregate paid amounts per treatment
            //    We look at TodayPaid per visit and attribute it via FIFO
            //    (same logic as FinancialRecordsView).  For statistics we only
            //    need the gross billed amount per treatment name, so we sum
            //    (Cost × Quantity) converted to SYP using the visit's snapshot.

            var totals = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var visit in visits)
            {
                if (visit.CurrentCost == 0 && visit.TodayPaid == 0) continue;

                double rate = visit.UsdToSypRateSnapshot > 0
                    ? visit.UsdToSypRateSnapshot
                    : 15000;

                var treatments = ParseTreatments(visit.SelectedTreatmentsJson);
                if (treatments.Count == 0)
                {
                    // No treatment detail — add to أخرى
                    double paid = visit.TodayPaid > 0 ? visit.TodayPaid : visit.CurrentCost;
                    AddTo(totals, OtherLabel, paid);
                }
                else
                {
                    foreach (var t in treatments)
                    {
                        double amountSyp = t.Currency == "USD"
                            ? (double)t.Cost * t.Quantity * rate
                            : (double)t.Cost * t.Quantity;

                        string name = string.IsNullOrWhiteSpace(t.TreatmentName)
                            ? OtherLabel
                            : t.TreatmentName;

                        AddTo(totals, name, amountSyp);
                    }
                }
            }

            // 3. Build slice list: known treatments first, then unknowns, أخرى last
            var slices = BuildIncomeSlices(knownTreatments, totals);
            DrawPie(slices);
        }

        // ── Expenses ──────────────────────────────────────────────────────────

        private async Task RefreshExpensesAsync()
        {
            List<ExpenseEntry> expenses;
            using (var ctx = new AppDbContext())
            {
                var query = ctx.Expenses.AsNoTracking().AsQueryable();
                query = ApplyDateFilterExpenses(query);
                expenses = await query.ToListAsync();
            }

            // Category totals
            var totals = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var expense in expenses)
            {
                string category = ClassifyExpense(expense.Description);
                AddTo(totals, category, expense.Amount);
            }

            // Build slices: fixed category order, then أخرى last
            var slices = new List<PieSlice>();
            var fixedOrder = ExpenseKeywords.Select(k => k.Label).ToList();
            fixedOrder.Add(OtherLabel);

            int colorIdx = 0;
            foreach (var label in fixedOrder)
            {
                if (totals.TryGetValue(label, out double amount) && amount > 0)
                {
                    slices.Add(new PieSlice
                    {
                        Label  = label,
                        Amount = amount,
                        Color  = SliceColors[colorIdx % SliceColors.Length]
                    });
                }
                colorIdx++;
            }

            DrawPie(slices);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Pie drawing
        // ═════════════════════════════════════════════════════════════════════

        private void DrawPie(List<PieSlice> slices)
        {
            PieCanvas.Children.Clear();
            LegendPanel.Children.Clear();

            double total = slices.Sum(s => s.Amount);

            // No data
            if (total <= 0 || slices.Count == 0)
            {
                TblNoData.Visibility = Visibility.Visible;
                TblTotal.Text        = "";
                return;
            }
            TblNoData.Visibility = Visibility.Collapsed;

            double cx = 140, cy = 140, r = 128, innerR = 56;
            double startAngle = -90.0; // start at 12 o'clock

            foreach (var slice in slices)
            {
                double sweep = (slice.Amount / total) * 360.0;
                // Clamp to avoid degenerate arcs at exactly 360
                if (sweep >= 360) sweep = 359.9999;

                var brush = HexBrush(slice.Color);

                if (slices.Count == 1)
                {
                    // Full ring
                    var outer = new Ellipse
                    {
                        Width = r * 2, Height = r * 2,
                        Fill = brush
                    };
                    Canvas.SetLeft(outer, cx - r);
                    Canvas.SetTop(outer,  cy - r);
                    PieCanvas.Children.Add(outer);
                }
                else
                {
                    var path = CreateDonutSlice(cx, cy, r, innerR, startAngle, sweep, brush);
                    PieCanvas.Children.Add(path);
                }

                startAngle += sweep;
            }

            // Centre hole (white/card bg)
            if (slices.Count > 1)
            {
                var hole = new Ellipse
                {
                    Width  = innerR * 2,
                    Height = innerR * 2,
                    Fill   = (Brush)Application.Current.Resources["AppWindowBg"]
                             ?? new SolidColorBrush(Color.FromRgb(11, 17, 32))
                };
                Canvas.SetLeft(hole, cx - innerR);
                Canvas.SetTop(hole,  cy - innerR);
                PieCanvas.Children.Add(hole);
            }

            // Total label
            TblTotal.Text = $"الإجمالي: {total:N0} ل.س";

            // Legend
            BuildLegend(slices, total);
        }

        private static Path CreateDonutSlice(
            double cx, double cy, double outerR, double innerR,
            double startDeg, double sweepDeg, Brush fill)
        {
            double startRad = DegToRad(startDeg);
            double endRad   = DegToRad(startDeg + sweepDeg);

            // Outer arc points
            var outerStart = new Point(cx + outerR * Math.Cos(startRad), cy + outerR * Math.Sin(startRad));
            var outerEnd   = new Point(cx + outerR * Math.Cos(endRad),   cy + outerR * Math.Sin(endRad));

            // Inner arc points (reversed)
            var innerEnd   = new Point(cx + innerR * Math.Cos(endRad),   cy + innerR * Math.Sin(endRad));
            var innerStart = new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad));

            bool largeArc = sweepDeg > 180;

            var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
            // Outer arc (clockwise)
            figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerR, outerR), 0,
                largeArc, SweepDirection.Clockwise, true));
            // Line to inner arc end
            figure.Segments.Add(new LineSegment(innerEnd, true));
            // Inner arc (counter-clockwise)
            figure.Segments.Add(new ArcSegment(innerStart, new Size(innerR, innerR), 0,
                largeArc, SweepDirection.Counterclockwise, true));

            return new Path
            {
                Data = new PathGeometry { Figures = { figure } },
                Fill = fill,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                StrokeThickness = 1
            };
        }

        private void BuildLegend(List<PieSlice> slices, double total)
        {
            // Title
            var title = new TextBlock
            {
                Text       = "التفاصيل",
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AppTextSecondary"],
                Margin     = new Thickness(0, 0, 0, 10)
            };
            LegendPanel.Children.Add(title);

            foreach (var slice in slices)
            {
                double pct = total > 0 ? (slice.Amount / total) * 100 : 0;

                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Colour swatch
                var swatch = new Border
                {
                    Width        = 12,
                    Height       = 12,
                    CornerRadius = new CornerRadius(3),
                    Background   = HexBrush(slice.Color),
                    Margin       = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(swatch, 0);

                // Label + percentage
                var labelBlock = new TextBlock
                {
                    Text                = $"{slice.Label}  ({pct:N1}%)",
                    FontSize            = 13,
                    Foreground          = (Brush)Application.Current.Resources["AppTextPrimary"],
                    VerticalAlignment   = VerticalAlignment.Center,
                    TextTrimming        = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(labelBlock, 1);

                // Amount
                var amtBlock = new TextBlock
                {
                    Text              = $"{slice.Amount:N0}",
                    FontSize          = 13,
                    FontWeight        = FontWeights.SemiBold,
                    Foreground        = HexBrush(slice.Color),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(12, 0, 0, 0)
                };
                Grid.SetColumn(amtBlock, 2);

                row.Children.Add(swatch);
                row.Children.Add(labelBlock);
                row.Children.Add(amtBlock);

                LegendPanel.Children.Add(row);

                // Thin separator
                LegendPanel.Children.Add(new Border
                {
                    Height     = 1,
                    Background = (Brush)Application.Current.Resources["AppBorder"],
                    Margin     = new Thickness(0, 2, 0, 2),
                    Opacity    = 0.5
                });
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        // Date filter for Visits
        private IQueryable<Visit> ApplyDateFilter(IQueryable<Visit> query)
        {
            switch (_subTab)
            {
                case SubTab.Daily:
                    var day = _selectedDay.Date;
                    return query.Where(v => v.VisitDate.Date == day);
                case SubTab.Monthly:
                    return query.Where(v => v.VisitDate.Year == _selectedYear
                                        && v.VisitDate.Month == _selectedMonth);
                case SubTab.Yearly:
                    return query.Where(v => v.VisitDate.Year == _selectedYear);
                default: return query;
            }
        }

        // Date filter for Expenses
        private IQueryable<ExpenseEntry> ApplyDateFilterExpenses(IQueryable<ExpenseEntry> query)
        {
            switch (_subTab)
            {
                case SubTab.Daily:
                    var day = _selectedDay.Date;
                    return query.Where(e => e.ExpenseDate.Date == day);
                case SubTab.Monthly:
                    return query.Where(e => e.ExpenseDate.Year == _selectedYear
                                         && e.ExpenseDate.Month == _selectedMonth);
                case SubTab.Yearly:
                    return query.Where(e => e.ExpenseDate.Year == _selectedYear);
                default: return query;
            }
        }

        // Classify an expense description into a category label
        private static string ClassifyExpense(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return OtherLabel;
            foreach (var (keyword, label) in ExpenseKeywords)
                if (description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return label;
            return OtherLabel;
        }

        // Build income slices in fixed order (known treatments → unknowns → أخرى)
        private static List<PieSlice> BuildIncomeSlices(
            List<string> knownTreatments,
            Dictionary<string, double> totals)
        {
            var slices    = new List<PieSlice>();
            var used      = new HashSet<string>(StringComparer.Ordinal);
            int colorIdx  = 0;

            // Known treatments first
            foreach (var name in knownTreatments)
            {
                if (totals.TryGetValue(name, out double amt) && amt > 0)
                {
                    slices.Add(new PieSlice
                    {
                        Label  = name,
                        Amount = amt,
                        Color  = SliceColors[colorIdx % SliceColors.Length]
                    });
                    colorIdx++;
                }
                used.Add(name);
            }

            // Unknown treatment names (not in settings — shouldn't normally happen)
            foreach (var kv in totals)
            {
                if (used.Contains(kv.Key) || kv.Key == OtherLabel) continue;
                if (kv.Value <= 0) continue;
                slices.Add(new PieSlice
                {
                    Label  = kv.Key,
                    Amount = kv.Value,
                    Color  = SliceColors[colorIdx % SliceColors.Length]
                });
                colorIdx++;
            }

            // أخرى last
            if (totals.TryGetValue(OtherLabel, out double other) && other > 0)
            {
                slices.Add(new PieSlice
                {
                    Label  = OtherLabel,
                    Amount = other,
                    Color  = SliceColors[colorIdx % SliceColors.Length]
                });
            }

            return slices;
        }

        private static void AddTo(Dictionary<string, double> dict, string key, double amount)
        {
            if (!dict.ContainsKey(key)) dict[key] = 0;
            dict[key] += amount;
        }

        private static List<SelectedTreatment> ParseTreatments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SelectedTreatment>();
            try
            {
                return JsonSerializer.Deserialize<List<SelectedTreatment>>(json)
                       ?? new List<SelectedTreatment>();
            }
            catch { return new List<SelectedTreatment>(); }
        }

        // Update the date label text
        private void UpdateDateLabel()
        {
            TblDateLabel.Text = _subTab switch
            {
                SubTab.Daily   => _selectedDay.ToString("dd / MM / yyyy"),
                SubTab.Monthly => $"{ArabicMonth(_selectedMonth)} {_selectedYear}",
                SubTab.Yearly  => _selectedYear.ToString(),
                _              => ""
            };
        }

        private static string ArabicMonth(int m) => m switch
        {
            1  => "يناير",  2  => "فبراير", 3  => "مارس",
            4  => "أبريل",  5  => "مايو",   6  => "يونيو",
            7  => "يوليو",  8  => "أغسطس",  9  => "سبتمبر",
            10 => "أكتوبر", 11 => "نوفمبر", 12 => "ديسمبر",
            _  => m.ToString()
        };

        // Visual tab/subtab active state
        private void UpdateTabStyles()
        {
            var active   = FindResource("MainTabBtnActive") as Style;
            var inactive = FindResource("MainTabBtn")       as Style;

            BtnTabIncome.Style   = _mainTab == MainTab.Income   ? active : inactive;
            BtnTabExpenses.Style = _mainTab == MainTab.Expenses ? active : inactive;
        }

        private void UpdateSubTabStyles()
        {
            var active   = FindResource("SubTabBtnActive") as Style;
            var inactive = FindResource("SubTabBtn")       as Style;

            BtnDaily.Style   = _subTab == SubTab.Daily   ? active : inactive;
            BtnMonthly.Style = _subTab == SubTab.Monthly ? active : inactive;
            BtnYearly.Style  = _subTab == SubTab.Yearly  ? active : inactive;
        }

        // Hex colour string → SolidColorBrush
        private static SolidColorBrush HexBrush(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        // ── Inner types ───────────────────────────────────────────────────────

        private class PieSlice
        {
            public string Label  { get; set; } = "";
            public double Amount { get; set; }
            public string Color  { get; set; } = "#3B82F6";
        }
    }
}
