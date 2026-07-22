namespace LogaOutlookSync.Domain;

/// <summary>
/// Legt fest, welche <see cref="AbsenceType"/>-Werte tatsächlich mit Outlook synchronisiert
/// werden. Aktuell werden ausschließlich Urlaub und Gleitzeit verarbeitet; das Domänenmodell
/// ist bewusst um weitere Abwesenheitsarten erweiterbar (siehe <see cref="AbsenceType"/>), die
/// erst durch Aufnahme in diese Policy aktiv würden.
/// </summary>
public sealed class AbsenceSyncPolicy
{
    /// <summary>Enthält die aktuell im Lastenheft festgelegten, zu synchronisierenden Typen.</summary>
    public static readonly AbsenceSyncPolicy Default = new(new[] { AbsenceType.Vacation, AbsenceType.FlexTime });

    private readonly HashSet<AbsenceType> _syncedTypes;

    public AbsenceSyncPolicy(IEnumerable<AbsenceType> syncedTypes)
    {
        _syncedTypes = new HashSet<AbsenceType>(syncedTypes);
    }

    /// <summary>Prüft, ob der angegebene Abwesenheitstyp aktuell synchronisiert werden soll.</summary>
    public bool IsSynced(AbsenceType type) => _syncedTypes.Contains(type);

    /// <summary>Die aktuell aktiven, zu synchronisierenden Abwesenheitsarten.</summary>
    public IReadOnlyCollection<AbsenceType> SyncedTypes => _syncedTypes;
}
