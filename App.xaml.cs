using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Windows;

namespace MyClinic;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan LoginGracePeriod = TimeSpan.FromHours(24);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        using var context = new AppDbContext();

        // Creates the database if it does not already exist.
        context.Database.EnsureCreated();

        // Backfill the newer clinic tables when an older local database already exists.
        context.Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS Patients (
                Id INTEGER NOT NULL CONSTRAINT PK_Patients PRIMARY KEY AUTOINCREMENT,
                PhoneNumber TEXT NOT NULL,
                FullName TEXT NULL,
                Age INTEGER NULL,
                Gender TEXT NULL,
                BloodType TEXT NULL,
                IsSmoker INTEGER NOT NULL DEFAULT 0,
                SmokingType TEXT NULL,
                SmokingFrequency TEXT NULL,
                Allergies TEXT NULL,
                ChronicDiseases TEXT NULL
            );");

        context.Database.ExecuteSqlRaw(
            @"CREATE UNIQUE INDEX IF NOT EXISTS IX_Patients_PhoneNumber
              ON Patients (PhoneNumber);");

        context.Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS Appointments (
                Id INTEGER NOT NULL CONSTRAINT PK_Appointments PRIMARY KEY AUTOINCREMENT,
                PatientName TEXT NOT NULL,
                PhoneNumber TEXT NOT NULL,
                Reason TEXT NOT NULL,
                AppointmentDateTime TEXT NOT NULL
            );");

        context.Database.ExecuteSqlRaw(
            @"CREATE INDEX IF NOT EXISTS IX_Appointments_AppointmentDateTime
              ON Appointments (AppointmentDateTime);");

        context.Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS Visits (
                Id INTEGER NOT NULL CONSTRAINT PK_Visits PRIMARY KEY AUTOINCREMENT,
                VisitDate TEXT NOT NULL,
                PatientId INTEGER NOT NULL,
                IsPregnant INTEGER NOT NULL DEFAULT 0,
                IsNursing INTEGER NOT NULL DEFAULT 0,
                PregnancyMonth INTEGER NULL,
                BloodPressure TEXT NULL,
                HeartRate TEXT NULL,
                Temperature TEXT NULL,
                RespiratoryRate TEXT NULL,
                Weight TEXT NULL,
                Height TEXT NULL,
                Symptoms TEXT NULL,
                Diagnosis TEXT NULL,
                CurrentCost REAL NOT NULL DEFAULT 0,
                TodayPaid REAL NOT NULL DEFAULT 0,
                RemainingAmount REAL NOT NULL DEFAULT 0,
                ChartMode TEXT NOT NULL DEFAULT 'Adult',
                CONSTRAINT FK_Visits_Patients_PatientId FOREIGN KEY (PatientId) REFERENCES Patients (Id) ON DELETE CASCADE
            );");

        context.Database.ExecuteSqlRaw(
            @"CREATE INDEX IF NOT EXISTS IX_Visits_PatientId
              ON Visits (PatientId);");

        context.Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS ToothRecords (
                Id INTEGER NOT NULL CONSTRAINT PK_ToothRecords PRIMARY KEY AUTOINCREMENT,
                VisitId INTEGER NOT NULL,
                ToothId TEXT NOT NULL,
                Condition TEXT NULL,
                Notes TEXT NULL,
                CONSTRAINT FK_ToothRecords_Visits_VisitId FOREIGN KEY (VisitId) REFERENCES Visits (Id) ON DELETE CASCADE
            );");

        context.Database.ExecuteSqlRaw(
            @"CREATE INDEX IF NOT EXISTS IX_ToothRecords_VisitId
              ON ToothRecords (VisitId);");

        context.Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS Expenses (
                Id INTEGER NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY AUTOINCREMENT,
                ExpenseDate TEXT NOT NULL,
                Description TEXT NOT NULL,
                Amount REAL NOT NULL DEFAULT 0
            );");

        context.Database.ExecuteSqlRaw(
            @"CREATE INDEX IF NOT EXISTS IX_Expenses_ExpenseDate
              ON Expenses (ExpenseDate);");

        EnsureColumnExists(context, "Visits", "PrescriptionJson", "TEXT NULL");
        EnsureColumnExists(context, "Visits", "TreatmentPlanNotes", "TEXT NULL");
        EnsureColumnExists(context, "Visits", "FinalTreatment", "TEXT NULL");
        EnsureColumnExists(context, "Visits", "AttachedImagePathsJson", "TEXT NULL");
        EnsureColumnExists(context, "Visits", "CurrentCost", "REAL NOT NULL DEFAULT 0");
        EnsureColumnExists(context, "Visits", "TodayPaid", "REAL NOT NULL DEFAULT 0");
        EnsureColumnExists(context, "Visits", "RemainingAmount", "REAL NOT NULL DEFAULT 0");

        context.Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS Shortages (
                Id INTEGER NOT NULL CONSTRAINT PK_Shortages PRIMARY KEY AUTOINCREMENT,
                Item TEXT NOT NULL,
                IsUrgent INTEGER NOT NULL DEFAULT 0,
                Price REAL NOT NULL DEFAULT 0,
                Currency TEXT NOT NULL DEFAULT 'SYP',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );");

        EnsureColumnExists(context, "Shortages", "Price", "REAL NOT NULL DEFAULT 0");
        EnsureColumnExists(context, "Shortages", "Currency", "TEXT NOT NULL DEFAULT 'SYP'");

        bool hasUsers = context.Users.Any();
        Window startupWindow = hasUsers && LoginSessionStore.HasValidLoginSession(LoginGracePeriod)
            ? new MainWindow()
            : new LoginWindow();

        MainWindow = startupWindow;
        startupWindow.Show();
    }

    private static void EnsureColumnExists(AppDbContext context, string tableName, string columnName, string columnDefinition)
    {
        using var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}
