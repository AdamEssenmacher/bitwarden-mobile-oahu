using Foundation;
using UIKit;

namespace Bit.iOS
{
    // Apps built with the iOS 27 SDK must use the UIKit scene lifecycle (MT7199). Under it, UIKit sends the
    // activation, background, foreground, URL and user-activity callbacks to the scene delegate instead of the
    // app delegate. This forwards each one to the existing AppDelegate override so its behavior is unchanged.
    [Register("SceneDelegate")]
    public class SceneDelegate : MauiUISceneDelegate
    {
        private static AppDelegate AppDelegate => UIApplication.SharedApplication.Delegate as AppDelegate;

        public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            base.WillConnect(scene, session, connectionOptions);

            // A cold launch from a URL or a user activity arrives here, not in OpenUrlContexts/ContinueUserActivity.
            if (connectionOptions?.UrlContexts != null)
            {
                OpenUrlContexts(scene, connectionOptions.UrlContexts);
            }
            if (connectionOptions?.UserActivities != null)
            {
                foreach (NSUserActivity userActivity in connectionOptions.UserActivities)
                {
                    ContinueUserActivity(scene, userActivity);
                }
            }
        }

        // Override MAUI's own handlers (and call them): exporting the selectors on new methods hid them, so MAUI never
        // saw the window activate, and on MAUI 11 a modal page pushed before activation is never presented.
        public override void OnResignActivation(UIScene scene)
        {
            AppDelegate?.OnResignActivation(UIApplication.SharedApplication);
            base.OnResignActivation(scene);
        }

        public override void DidEnterBackground(UIScene scene)
        {
            AppDelegate?.DidEnterBackground(UIApplication.SharedApplication);
            base.DidEnterBackground(scene);
        }

        public override void WillEnterForeground(UIScene scene)
        {
            AppDelegate?.WillEnterForeground(UIApplication.SharedApplication);
            base.WillEnterForeground(scene);
        }

        public override void OnActivated(UIScene scene)
        {
            base.OnActivated(scene);
            AppDelegate?.OnActivated(UIApplication.SharedApplication);
        }

        public override bool OpenUrl(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
        {
            var handled = base.OpenUrl(scene, urlContexts);
            OpenUrlContexts(scene, urlContexts);
            return handled;
        }

        private static void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
        {
            foreach (UIOpenUrlContext urlContext in urlContexts)
            {
                AppDelegate?.OpenUrl(UIApplication.SharedApplication, urlContext.Url, new NSDictionary());
            }
        }

        public override bool ContinueUserActivity(UIScene scene, NSUserActivity userActivity)
        {
            var handled = base.ContinueUserActivity(scene, userActivity);
            return (AppDelegate?.ContinueUserActivity(UIApplication.SharedApplication, userActivity, _ => { }) ?? false)
                || handled;
        }
    }
}
