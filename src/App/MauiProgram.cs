using Oahu.Maui;

namespace Bit.App
{
    public class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = Core.MauiProgram.ConfigureMauiAppBuilder(
                effects =>
                {
#if IOS
                    iOS.Core.Utilities.iOSCoreHelpers.ConfigureMAUIEffects(effects);
#endif
                },
                handlers =>
                {
#if ANDROID
                    Bit.App.Handlers.EntryHandlerMappings.Setup();
                    Bit.App.Handlers.EditorHandlerMappings.Setup();
                    Bit.App.Handlers.LabelHandlerMappings.Setup();
                    Bit.App.Handlers.PickerHandlerMappings.Setup();
                    Bit.App.Handlers.SearchBarHandlerMappings.Setup();
                    Bit.App.Handlers.SwitchHandlerMappings.Setup();
                    Bit.App.Handlers.DatePickerHandlerMappings.Setup();
                    Bit.App.Handlers.SliderHandlerMappings.Setup();
                    Bit.App.Handlers.StepperHandlerMappings.Setup();
                    Bit.App.Handlers.TimePickerHandlerMappings.Setup();
                    Bit.App.Handlers.ButtonHandlerMappings.Setup();
                    Bit.App.Handlers.ToolbarHandlerMappings.Setup();

                    handlers.AddHandler(typeof(Bit.App.Controls.HybridWebView), typeof(Bit.App.Handlers.HybridWebViewHandler));
                    handlers.AddHandler(typeof(Bit.App.Pages.TabsPage), typeof(Bit.App.Handlers.CustomTabbedPageHandler));
                    handlers.AddHandler(typeof(Bit.App.Controls.ExtendedDatePicker), typeof(Bit.App.Handlers.ExtendedDatePickerHandler));
#else
                    iOS.Core.Utilities.iOSCoreHelpers.ConfigureMAUIHandlers(handlers);
#endif
                },
               initUseMauiApp: true
            ).UseOahu();
            // OAHU: a contract method that throws, or a defect OAHU detects, is reported only here.
#if IOS
            Oahu.iOS.IosPortHost.Shared.MethodFailed += ex => Console.WriteLine($"OAHU METHOD FAILED {ex}");
            Oahu.iOS.IosPortHost.Shared.Faulted += ex => Console.WriteLine($"OAHU FAULT {ex}");
#elif ANDROID
            Oahu.Android.AndroidPortHost.Main.MethodFailed += ex => Android.Util.Log.Error("OAHU", $"METHOD FAILED {ex}");
            Oahu.Android.AndroidPortHost.Main.Faulted += ex => Android.Util.Log.Error("OAHU", $"FAULT {ex}");
#endif
            return builder.Build();
        }
    }
}
