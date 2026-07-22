namespace LogaOutlookSync.Calendar.OutlookCom;

/// <summary>
/// Führt eine synchrone Aktion auf einem dediziertem Thread im STA-Apartment aus. Outlook-COM-
/// Interop-Objekte erfordern zwingend ein STA-Apartment; der ThreadPool (von <c>Task.Run</c>)
/// läuft im MTA und würde COM-Aufrufe unzuverlässig oder gar nicht funktionieren lassen.
/// </summary>
internal static class StaThread
{
    public static Task<T> RunAsync<T>(Func<T> action)
    {
        var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                completionSource.SetResult(action());
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completionSource.Task;
    }
}
