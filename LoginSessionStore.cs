using System;
using System.IO;

namespace MyClinic
{
    internal static class LoginSessionStore
    {
        private static readonly string SessionDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyClinicApp");

        private static readonly string SessionFilePath = Path.Combine(SessionDirectory, "session.txt");

        public static bool HasValidLoginSession(TimeSpan maxAge)
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                {
                    return false;
                }

                string savedText = File.ReadAllText(SessionFilePath).Trim();
                if (!DateTime.TryParse(savedText, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastLoginUtc))
                {
                    return false;
                }

                return DateTime.UtcNow - lastLoginUtc < maxAge;
            }
            catch
            {
                return false;
            }
        }

        public static void MarkSuccessfulLogin()
        {
            Directory.CreateDirectory(SessionDirectory);
            File.WriteAllText(SessionFilePath, DateTime.UtcNow.ToString("O"));
        }
    }
}
