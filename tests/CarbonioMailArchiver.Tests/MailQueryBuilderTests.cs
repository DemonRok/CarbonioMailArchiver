using CarbonioMailArchiver.Core.Models;
using CarbonioMailArchiver.Core.Services;

namespace CarbonioMailArchiver.Tests;

public sealed class MailQueryBuilderTests
{
  [Fact]
  public void BuildInboxBeforeQuery_UsesExclusiveDateFormat()
  {
    var builder = new MailQueryBuilder();
    var request = new MailSearchRequest(new DateOnly(2024, 1, 31), 200);

    var query = builder.BuildInboxBeforeQuery(request);

    Assert.Equal("in:inbox before:31/01/2024", query);
  }
}
