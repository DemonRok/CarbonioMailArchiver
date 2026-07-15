using CarbonioMailArchiver.Core.Abstractions;
using CarbonioMailArchiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace CarbonioMailArchiver.Infrastructure.Services;

public sealed class CarbonioMoveDiagnosticService(ILogger<CarbonioMoveDiagnosticService> logger) : IMoveDiagnosticService
{
  public async Task<MailMoveResult> MoveMessagesAsync(
    CarbonioConnectionSettings settings,
    string password,
    IReadOnlyList<string> messageIds,
    string destinationFolderId,
    CancellationToken cancellationToken)
  {
    if (messageIds.Count == 0)
    {
      return new MailMoveResult(0, 0, [], null);
    }

    using var client = CarbonioWebClient.Create(settings, out var validationError);
    if (validationError is not null)
    {
      logger.LogWarning("MsgActionRequest non avviata per {Account}: {Reason}", settings.Email, validationError);
      return new MailMoveResult(messageIds.Count, 0, messageIds, new SoapFaultInfo("validation", validationError, null));
    }

    try
    {
      var loginError = await client.LoginAsync(password, cancellationToken);
      if (loginError is not null)
      {
        logger.LogWarning("Login Carbonio Auth fallito per MsgActionRequest {Account}: {Reason}", settings.Email, loginError);
        return new MailMoveResult(messageIds.Count, 0, messageIds, new SoapFaultInfo("auth", loginError, null));
      }

      var response = await client.PostMoveMessagesAsync(messageIds, destinationFolderId, cancellationToken);
      var content = await response.Content.ReadAsStringAsync(cancellationToken);

      if (!response.IsSuccessStatusCode || content.Contains("\"Fault\"", StringComparison.OrdinalIgnoreCase))
      {
        logger.LogWarning(
          "MsgActionRequest move fallita con status {StatusCode} per {Account}. Risposta: {Response}",
          response.StatusCode,
          settings.Email,
          CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content));
        return new MailMoveResult(
          messageIds.Count,
          0,
          messageIds,
          new SoapFaultInfo($"HTTP {(int)response.StatusCode}", "MsgActionRequest move fallita.", CarbonioConnectionDiagnosticService.SanitizeDiagnosticResponse(content)));
      }

      logger.LogInformation(
        "MsgActionRequest move riuscita per {Account}. Messaggi: {Count}. Destinazione: {DestinationFolderId}.",
        settings.Email,
        messageIds.Count,
        destinationFolderId);
      return new MailMoveResult(messageIds.Count, messageIds.Count, [], null);
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning(ex, "Errore HTTP durante MsgActionRequest move per {Account}.", settings.Email);
      return new MailMoveResult(messageIds.Count, 0, messageIds, new SoapFaultInfo("http", ex.Message, null));
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
      logger.LogWarning(ex, "Timeout durante MsgActionRequest move per {Account}.", settings.Email);
      return new MailMoveResult(messageIds.Count, 0, messageIds, new SoapFaultInfo("timeout", "Timeout durante lo spostamento.", null));
    }
  }
}
