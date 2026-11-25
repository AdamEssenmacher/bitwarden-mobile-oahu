#if ANDROID
#nullable enable
namespace Bit.App.Utilities
{
    /// <summary>
    /// The app's declared view IDs for OAHU screens' saved state (src/App/Platforms/Android/Resources/values/ids.xml).
    /// The pages are in this library, which has no Android resources and so no Resource.Id, so the IDs are found by name
    /// at run time (docs/oahu/gaps.md, G20).
    /// </summary>
    public static class OahuAndroidIds
    {
        public static int Get(string name)
        {
            var context = Android.App.Application.Context;
#pragma warning disable CA1422 // GetIdentifier is discouraged, not removed; there is no other way from a library
            var id = context.Resources?.GetIdentifier(name, "id", context.PackageName) ?? 0;
#pragma warning restore CA1422
            if (id == 0)
            {
                throw new InvalidOperationException($"View ID '{name}' is not declared in the app's ids.xml.");
            }
            return id;
        }
    }
}
#endif
