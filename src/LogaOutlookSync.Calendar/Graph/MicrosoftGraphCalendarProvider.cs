using System.Globalization;
using LogaOutlookSync.Calendar.Exceptions;
using LogaOutlookSync.Domain;
using LogaOutlookSync.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace LogaOutlookSync.Calendar.Graph;

/// <summary>
/// Bevorzugte <see cref="ICalendarProvider"/>-Implementierung über Microsoft Graph. Verwendet
/// MSAL.NET für die Authentifizierung (Public-Client, interaktiv über den System-Browser mit
/// persistentem, verschlüsseltem Token-Cache) und speichert die LOGA-Sync-Kennung sowie den
/// "ManagedBy"-Marker als erweiterte Eigenschaften am Termin.
/// </summary>
public sealed class MicrosoftGraphCalendarProvider : ICalendarProvider
{
    private static readonly string[] GraphScopes = { "Calendars.ReadWrite", "User.Read" };
    private const int MaxEventsPerPage = 250;

    private readonly AppSettings _settings;
    private readonly ILogger<MicrosoftGraphCalendarProvider> _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private GraphServiceClient? _client;

    public MicrosoftGraphCalendarProvider(AppSettings settings, ILogger<MicrosoftGraphCalendarProvider> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ManagedCalendarEntry>> GetManagedEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);

        EventCollectionResponse? page;
        try
        {
            page = await GetCalendarViewBuilder(client).GetAsync(cfg =>
            {
                cfg.QueryParameters.StartDateTime = from.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                cfg.QueryParameters.EndDateTime = to.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                cfg.QueryParameters.Top = MaxEventsPerPage;
                cfg.QueryParameters.Expand = new[]
                {
                    $"singleValueExtendedProperties($filter=id eq '{GraphExtendedPropertyIds.ManagedBy}' " +
                    $"or id eq '{GraphExtendedPropertyIds.SyncId}' or id eq '{GraphExtendedPropertyIds.Fingerprint}')",
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ODataError ex)
        {
            throw new GraphCalendarException(
                $"Kalendertermine konnten nicht von Microsoft Graph gelesen werden: {DescribeError(ex)}", ex);
        }

        var result = new List<ManagedCalendarEntry>();
        if (page?.Value is not null)
        {
            foreach (var graphEvent in page.Value)
            {
                result.Add(MapToManagedEntry(graphEvent));
            }
        }

        if (!string.IsNullOrEmpty(page?.OdataNextLink))
        {
            _logger.LogWarning(
                "Es wurden mehr als {Top} Kalendertermine im Synchronisationszeitraum gefunden. " +
                "Weitere Seiten werden aktuell nicht abgerufen; bitte den Synchronisationszeitraum eingrenzen, " +
                "falls dies regelmäßig auftritt.",
                MaxEventsPerPage);
        }

        return result;
    }

    public async Task<CalendarWriteResult> CreateAsync(CalendarSyncItem item, CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var graphEvent = BuildGraphEvent(item);

        Event created;
        try
        {
            created = await GetEventsBuilder(client).PostAsync(graphEvent, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
                ?? throw new GraphCalendarException("Microsoft Graph hat beim Anlegen des Termins keine Antwort geliefert.");
        }
        catch (ODataError ex)
        {
            throw new GraphCalendarException($"Termin konnte nicht in Microsoft Graph angelegt werden: {DescribeError(ex)}", ex);
        }

        _logger.LogInformation("Termin '{Subject}' in Microsoft Graph angelegt (SyncId {SyncId}).", item.Subject, item.SyncId);
        return await VerifyShowAsAsync(client, created.Id!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CalendarWriteResult> UpdateAsync(CalendarSyncItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.ExistingCalendarEntryId))
        {
            throw new InvalidOperationException(
                $"Für die Aktualisierung des Termins mit SyncId '{item.SyncId}' fehlt die vorhandene Termin-ID.");
        }

        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var graphEvent = BuildGraphEvent(item);

        try
        {
            await GetEventsBuilder(client)[item.ExistingCalendarEntryId]
                .PatchAsync(graphEvent, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ODataError ex)
        {
            throw new GraphCalendarException(
                $"Termin '{item.ExistingCalendarEntryId}' konnte nicht in Microsoft Graph aktualisiert werden: {DescribeError(ex)}", ex);
        }

        _logger.LogInformation("Termin '{Subject}' in Microsoft Graph aktualisiert (SyncId {SyncId}).", item.Subject, item.SyncId);
        return await VerifyShowAsAsync(client, item.ExistingCalendarEntryId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CalendarDescriptor>> ListCalendarsAsync(CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);

        CalendarCollectionResponse? response;
        try
        {
            response = await client.Me.Calendars.GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = new[] { "id", "name", "isDefaultCalendar" };
                cfg.QueryParameters.Top = 100;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ODataError ex)
        {
            throw new GraphCalendarException($"Die verfügbaren Kalender konnten nicht von Microsoft Graph gelesen werden: {DescribeError(ex)}", ex);
        }

        return response?.Value?
            .Where(c => c.Id is not null)
            .Select(c => new CalendarDescriptor(c.Id!, c.Name ?? c.Id!, c.IsDefaultCalendar ?? false))
            .ToList()
            ?? new List<CalendarDescriptor>();
    }

    public async Task DeleteAsync(string calendarEntryId, CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await GetEventsBuilder(client)[calendarEntryId].DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ODataError ex)
        {
            throw new GraphCalendarException(
                $"Termin '{calendarEntryId}' konnte in Microsoft Graph nicht gelöscht werden: {DescribeError(ex)}", ex);
        }

        _logger.LogInformation("Termin '{EntryId}' in Microsoft Graph gelöscht.", calendarEntryId);
    }

    private async Task<CalendarWriteResult> VerifyShowAsAsync(GraphServiceClient client, string eventId, CancellationToken cancellationToken)
    {
        try
        {
            var fetched = await GetEventsBuilder(client)[eventId]
                .GetAsync(cfg => cfg.QueryParameters.Select = new[] { "showAs" }, cancellationToken)
                .ConfigureAwait(false);

            if (fetched?.ShowAs == FreeBusyStatus.Oof)
            {
                return new CalendarWriteResult(eventId, true, null);
            }

            const string warning = "Der Termin wurde gespeichert, aber der verpflichtende Status \"Abwesend\" konnte nicht bestätigt werden.";
            _logger.LogWarning("{Warning} (Termin-ID {EventId})", warning, eventId);
            return new CalendarWriteResult(eventId, false, warning);
        }
        catch (ODataError ex)
        {
            var warning = $"Der \"Abwesend\"-Status des Termins konnte nach dem Speichern nicht überprüft werden: {DescribeError(ex)}";
            _logger.LogWarning(ex, "{Warning}", warning);
            return new CalendarWriteResult(eventId, false, warning);
        }
    }

    private Event BuildGraphEvent(CalendarSyncItem item)
    {
        return new Event
        {
            Subject = item.Subject,
            Body = new ItemBody { ContentType = BodyType.Text, Content = item.Body },
            Start = ToGraphDateTimeTimeZone(item.Start),
            End = ToGraphDateTimeTimeZone(item.End),
            IsAllDay = item.IsAllDay,
            ShowAs = GraphShowAsMapper.ToGraph(item.ShowAs),
            Sensitivity = GraphShowAsMapper.ToGraphSensitivity(item.Sensitivity),
            IsReminderOn = item.ReminderEnabled,
            Categories = new List<string> { item.Category },
            SingleValueExtendedProperties = new List<SingleValueLegacyExtendedProperty>
            {
                new() { Id = GraphExtendedPropertyIds.SyncId, Value = item.SyncId },
                new() { Id = GraphExtendedPropertyIds.ManagedBy, Value = ManagedEntryMetadata.ManagedByValue },
                new() { Id = GraphExtendedPropertyIds.Fingerprint, Value = item.SourceFingerprint },
            },
        };
    }

    private ManagedCalendarEntry MapToManagedEntry(Event graphEvent)
    {
        var extendedProperties = graphEvent.SingleValueExtendedProperties ?? new List<SingleValueLegacyExtendedProperty>();
        var syncId = extendedProperties.FirstOrDefault(p => p.Id == GraphExtendedPropertyIds.SyncId)?.Value;
        var managedByValue = extendedProperties.FirstOrDefault(p => p.Id == GraphExtendedPropertyIds.ManagedBy)?.Value;
        var fingerprint = extendedProperties.FirstOrDefault(p => p.Id == GraphExtendedPropertyIds.Fingerprint)?.Value;

        var isManaged = string.Equals(managedByValue, ManagedEntryMetadata.ManagedByValue, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(syncId);

        if (!isManaged && ManagedEntryMetadata.BodyContainsManagedByMarker(graphEvent.Body?.Content))
        {
            syncId ??= ManagedEntryMetadata.TryExtractSyncIdFromBody(graphEvent.Body?.Content);
            isManaged = !string.IsNullOrEmpty(syncId);
        }

        return new ManagedCalendarEntry(
            CalendarEntryId: graphEvent.Id ?? string.Empty,
            SyncId: syncId,
            IsManagedByApp: isManaged,
            Subject: graphEvent.Subject ?? string.Empty,
            Start: FromGraphDateTimeTimeZone(graphEvent.Start),
            End: FromGraphDateTimeTimeZone(graphEvent.End),
            IsAllDay: graphEvent.IsAllDay ?? false,
            Category: graphEvent.Categories?.FirstOrDefault(),
            ShowAs: GraphShowAsMapper.FromGraph(graphEvent.ShowAs),
            SourceFingerprint: fingerprint,
            LastModifiedUtc: graphEvent.LastModifiedDateTime);
    }

    private Microsoft.Graph.Me.Calendar.CalendarView.CalendarViewRequestBuilder GetCalendarViewBuilder(GraphServiceClient client)
    {
        return string.IsNullOrEmpty(_settings.TargetCalendarId)
            ? client.Me.Calendar.CalendarView
            : client.Me.Calendars[_settings.TargetCalendarId].CalendarView;
    }

    private Microsoft.Graph.Me.Calendar.Events.EventsRequestBuilder GetEventsBuilder(GraphServiceClient client)
    {
        return string.IsNullOrEmpty(_settings.TargetCalendarId)
            ? client.Me.Calendar.Events
            : client.Me.Calendars[_settings.TargetCalendarId].Events;
    }

    private async Task<GraphServiceClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _client ??= await CreateClientAsync().ConfigureAwait(false);
            return _client;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private async Task<GraphServiceClient> CreateClientAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.GraphClientId))
        {
            throw new GraphAuthenticationException(
                "Für Microsoft Graph ist keine Entra-ID-App-Registrierung (Client-ID) konfiguriert. " +
                "Bitte richten Sie eine App-Registrierung gemäß docs/GRAPH-SETUP.md ein, oder wechseln Sie " +
                "in den Einstellungen auf die Outlook-COM-Anbindung.");
        }

        IPublicClientApplication app;
        try
        {
            app = PublicClientApplicationBuilder
                .Create(_settings.GraphClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, _settings.GraphTenantId)
                .WithRedirectUri("http://localhost")
                .Build();
        }
        catch (MsalClientException ex)
        {
            throw new GraphAuthenticationException(
                $"Der Microsoft-Graph-Client konnte nicht initialisiert werden: {ex.Message}", ex);
        }

        await RegisterTokenCacheAsync(app).ConfigureAwait(false);

        var authProvider = new MsalAuthenticationProvider(app, GraphScopes);
        var requestAdapter = new HttpClientRequestAdapter(authProvider);
        return new GraphServiceClient(requestAdapter);
    }

    private static async Task RegisterTokenCacheAsync(IPublicClientApplication app)
    {
        AppPaths.EnsureDirectoriesExist();
        var storageProperties = new StorageCreationPropertiesBuilder("graph-token-cache.bin", AppPaths.ConfigDirectory)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
        cacheHelper.RegisterCache(app.UserTokenCache);
    }

    private static DateTimeTimeZone ToGraphDateTimeTimeZone(DateTimeOffset value)
    {
        return new DateTimeTimeZone
        {
            DateTime = value.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
            TimeZone = TimeZoneInfo.Local.Id,
        };
    }

    private static DateTimeOffset FromGraphDateTimeTimeZone(DateTimeTimeZone? value)
    {
        if (string.IsNullOrEmpty(value?.DateTime))
        {
            return default;
        }

        var localDateTime = DateTime.Parse(value.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZone ?? TimeZoneInfo.Local.Id);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Local;
        }

        var offset = zone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    private static string DescribeError(ODataError error) => error.Error?.Message ?? error.Message;
}
