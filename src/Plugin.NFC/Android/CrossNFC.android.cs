using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Plugin.NFC
{
    public static partial class CrossNFC
    {
        internal static ActivityLifecycleContextListener lifecycleListener;
        public static void Init(Application application)
        {
            lifecycleListener = new ActivityLifecycleContextListener();
            application.RegisterActivityLifecycleCallbacks(lifecycleListener);
        }

        public static void Init(Activity activity)
        {
            Init(activity.Application);
            lifecycleListener.Activity = activity;
        }

        public static void OnNewIntent(Intent intent)
        {
            ((NFCImplementation)Current).HandleNewIntent(intent);
        }

        internal static Context AppContext => Application.Context;
        internal static Activity GetCurrentActivity(bool throwError)
        {
            var activity = lifecycleListener?.Activity;
            if (throwError && activity == null)
                throw new NullReferenceException("The current Activity can not be detected. Ensure that you have called Init in your Activity or Application class.");
            return activity;
        }
    }

    class ActivityLifecycleContextListener : Java.Lang.Object, Application.IActivityLifecycleCallbacks
    {
        WeakReference<Activity> currentActivity = new WeakReference<Activity>(null);

        internal Context Context => Activity ?? Application.Context;

        internal Activity Activity
        {
            get => currentActivity.TryGetTarget(out var a) ? a : null;
            set => currentActivity.SetTarget(value);
        }

        void Application.IActivityLifecycleCallbacks.OnActivityCreated(Activity activity, Bundle savedInstanceState) =>
            Activity = activity;

        void Application.IActivityLifecycleCallbacks.OnActivityDestroyed(Activity activity)
        {
        }

        void Application.IActivityLifecycleCallbacks.OnActivityPaused(Activity activity) => Activity = activity;

        void Application.IActivityLifecycleCallbacks.OnActivityResumed(Activity activity) =>  Activity = activity;

        void Application.IActivityLifecycleCallbacks.OnActivitySaveInstanceState(Activity activity, Bundle outState)
        {
        }

        void Application.IActivityLifecycleCallbacks.OnActivityStarted(Activity activity)
        {
        }

        void Application.IActivityLifecycleCallbacks.OnActivityStopped(Activity activity)
        {
        }
    }
}
