using System.Security.Cryptography;
using System.Text;

namespace WorklogManager.Helpers;

/// <summary>
/// Encrypts and decrypts sensitive strings (API tokens) using Windows DPAPI
/// (Data Protection API via System.Security.Cryptography.ProtectedData).
///
/// Encrypted data is tied to the current Windows user account —
/// other users or machines cannot decrypt it.
/// </summary>
public static class CredentialHelper
{
    /// <summary>
    /// Encrypts a plain-text string to a Base64-encoded DPAPI blob.
    /// Returns an empty string if input is null or empty.
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a Base64-encoded DPAPI blob back to plain text.
    /// Returns an empty string if the input is null, empty, or decryption fails.
    /// </summary>
    public static string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(encryptedBase64);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            // Decryption failure (wrong user, corrupted data) — return empty rather than throw
            return string.Empty;
        }
    }
}
