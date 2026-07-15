using System.Security.Cryptography;
using System.Text;
using CarbonioMailArchiver.Core.Abstractions;

namespace CarbonioMailArchiver.Infrastructure.Security;

public sealed class DpapiCredentialStore : ICredentialStore
{
  private readonly string _directory;

  public DpapiCredentialStore()
  {
    _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CarbonioMailArchiver", "Credentials");
    Directory.CreateDirectory(_directory);
  }

  public async Task SavePasswordAsync(string account, string password, CancellationToken cancellationToken)
  {
    var bytes = Encoding.UTF8.GetBytes(password);
    var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    await File.WriteAllBytesAsync(GetPath(account), protectedBytes, cancellationToken);
  }

  public async Task<string?> ReadPasswordAsync(string account, CancellationToken cancellationToken)
  {
    var path = GetPath(account);
    if (!File.Exists(path))
    {
      return null;
    }

    var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
    var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(bytes);
  }

  public Task DeletePasswordAsync(string account, CancellationToken cancellationToken)
  {
    var path = GetPath(account);
    if (File.Exists(path))
    {
      File.Delete(path);
    }

    return Task.CompletedTask;
  }

  private string GetPath(string account)
  {
    var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.ToLowerInvariant())));
    return Path.Combine(_directory, $"{fileName}.bin");
  }
}
