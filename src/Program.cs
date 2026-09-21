using System;
using System.Windows.Forms;

namespace DesktopCalendar;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "Local\\AS Desktop Calendar.SingleInstance",
            createdNew: out bool createdNew);

        if (!createdNew)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
