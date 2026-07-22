using LogaOutlookSync.Domain;

namespace LogaOutlookSync.App.ViewModels;

/// <summary>Anzeigefreundliche Projektion eines <see cref="SyncPlanItem"/> für die Vorschau-Tabelle.</summary>
public sealed class SyncPlanItemViewModel
{
    public SyncPlanItemViewModel(SyncPlanItem item)
    {
        Operation = item.Operation switch
        {
            SyncOperationKind.Create => "Neu anlegen",
            SyncOperationKind.Update => "Aktualisieren",
            SyncOperationKind.Delete => "Löschen",
            SyncOperationKind.Unchanged => "Unverändert",
            SyncOperationKind.Conflict => "Konflikt",
            _ => item.Operation.ToString(),
        };

        var subject = item.ProposedItem?.Subject ?? item.ExistingEntry?.Subject ?? "(unbekannt)";
        Subject = subject;

        var start = item.ProposedItem?.Start ?? item.ExistingEntry?.Start;
        var end = item.ProposedItem?.End ?? item.ExistingEntry?.End;
        DateRangeText = start is null || end is null
            ? "-"
            : $"{start:dd.MM.yyyy HH:mm} - {end:dd.MM.yyyy HH:mm}";

        Type = item.SourceAbsence?.Type.ToString() ?? string.Empty;
        Reason = item.Reason;
    }

    public string Operation { get; }

    public string Subject { get; }

    public string DateRangeText { get; }

    public string Type { get; }

    public string Reason { get; }
}
