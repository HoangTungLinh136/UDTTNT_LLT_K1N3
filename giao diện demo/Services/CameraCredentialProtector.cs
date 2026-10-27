using Microsoft.AspNetCore.DataProtection;

namespace giao_dien_demo.Services
{
    public interface ICameraCredentialProtector
    {
        string Protect(string plaintext);
        string Unprotect(string? protectedValue);
        bool IsProtected(string? value);
    }

    public class CameraCredentialProtector : ICameraCredentialProtector
    {
        private readonly IDataProtector _protector;
        private const string Prefix = "DP:v1:";

        public CameraCredentialProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("HRMS.CameraCredentials.v1");
        }

        public string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
            // Always protect, even if it happens to start with DP:v1:
            return Prefix + _protector.Protect(plaintext);
        }

        public bool IsProtected(string? value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(Prefix);
        }

        public string Unprotect(string? protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue)) return string.Empty;
            if (!protectedValue.StartsWith(Prefix)) return protectedValue; // legacy plaintext

            try
            {
                return _protector.Unprotect(protectedValue.Substring(Prefix.Length));
            }
            catch
            {
                // Unprotect failed, return empty to fail safely
                return string.Empty;
            }
        }
    }
}
