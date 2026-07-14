using Microsoft.EntityFrameworkCore;
using MyClinic.Models;
using System;
using System.IO;

namespace MyClinic
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<AppointmentEntry> Appointments { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<ExpenseEntry> Expenses { get; set; }
        public DbSet<ToothRecord> ToothRecords { get; set; }
        
        // تمت إضافة الجدول الجديد هنا
        public DbSet<UpcomingPayment> UpcomingPayments { get; set; }
        public DbSet<LabWork> LabWorks { get; set; }
        public DbSet<Shortage> Shortages { get; set; }
        public DbSet<AppSettings> AppSettings { get; set; }
        public DbSet<LabName> LabNames { get; set; }
        public DbSet<TreatmentCost> TreatmentCosts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath;

            // Check if running in development mode (dotnet run)
            #if DEBUG
            // Use local project database for development
            string projectFolder = Directory.GetCurrentDirectory();
            string devDbFolder = Path.Combine(projectFolder, "DevData");
            if (!Directory.Exists(devDbFolder))
            {
                Directory.CreateDirectory(devDbFolder);
            }
            dbPath = Path.Combine(devDbFolder, "ClinicData_Dev.db");
            #else
            // Production: Use LocalAppData for installed app
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string clinicFolder = Path.Combine(appDataFolder, "MyClinicApp");
            if (!Directory.Exists(clinicFolder))
            {
                Directory.CreateDirectory(clinicFolder);
            }
            dbPath = Path.Combine(clinicFolder, "ClinicData.db");
            #endif

            optionsBuilder.UseSqlite($"Data Source={dbPath};Foreign Keys=True");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // التعديل هنا: تم تغيير IsUnique إلى false 
            // هذا يسمح لأكثر من مريض (مثل أفراد العائلة) بمشاركة نفس رقم الهاتف
            // مع إبقاء الفهرس (Index) لتسريع عملية البحث التلقائي (Auto-fill)
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.PhoneNumber)
                .IsUnique(false);

            // Patient → Visits  (cascade delete: removing a patient removes their visits)
            modelBuilder.Entity<Patient>()
                .HasMany(p => p.Visits)
                .WithOne(v => v.Patient)
                .HasForeignKey(v => v.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Visit → ToothRecords  (cascade delete)
            modelBuilder.Entity<Visit>()
                .HasMany(v => v.ToothRecords)
                .WithOne(t => t.Visit)
                .HasForeignKey(t => t.VisitId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}