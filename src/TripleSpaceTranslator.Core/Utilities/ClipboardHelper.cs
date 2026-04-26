using System.Runtime.InteropServices;
using System.Windows;

namespace TripleSpaceTranslator.Core.Utilities;

public static class ClipboardHelper
{
    public static ClipboardTextSnapshot CaptureTextSnapshot()
    {
        return ExecuteWithRetry(() =>
        {
            if (!Clipboard.ContainsText())
            {
                return new ClipboardTextSnapshot(false, string.Empty);
            }

            return new ClipboardTextSnapshot(true, Clipboard.GetText());
        });
    }

    public static void RestoreTextSnapshot(ClipboardTextSnapshot snapshot)
    {
        ExecuteWithRetry(() =>
        {
            if (snapshot.HadText)
            {
                Clipboard.SetText(snapshot.Text ?? string.Empty);
            }
            else
            {
                Clipboard.Clear();
            }

            return 0;
        });
    }

    public static void SetText(string text)
    {
        ExecuteWithRetry(() =>
        {
            Clipboard.SetText(text ?? string.Empty);
            return 0;
        });
    }

    private static T ExecuteWithRetry<T>(Func<T> operation)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return ExecuteOnSta(operation);
            }
            catch (Exception ex) when (ex is COMException or ExternalException)
            {
                lastError = ex;
                Thread.Sleep(40);
            }
        }

        throw lastError ?? new InvalidOperationException("Clipboard operation failed.");
    }

    private static T ExecuteOnSta<T>(Func<T> operation)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = operation();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }
}
