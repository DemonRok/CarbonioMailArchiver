using System.Globalization;
using CarbonioMailArchiver.Core.Models;

namespace CarbonioMailArchiver.Core.Services;

public sealed class MailQueryBuilder
{
  public string BuildInboxBeforeQuery(MailSearchRequest request)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(request.Limit, 1);

    var date = request.BeforeDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
    return $"{request.SourceFolderQuery} before:{date}";
  }
}
