using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Kofrenot.Phone;

[Application]
public sealed class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
