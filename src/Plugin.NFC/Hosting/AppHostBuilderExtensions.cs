#if ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif
using Plugin.NFC.Configuration;

namespace Plugin.NFC.Hosting;

public static class AppHostBuilderExtensions
{
    public static MauiAppBuilder UseNfc(this MauiAppBuilder builder, Action<NfcOptions>? options = null)
    {
        if (CrossNFC.IsSupported)
        {
#if ANDROID
            // Configure Android lifecycle
            builder.ConfigureLifecycleEvents(lifecycles =>
            {
                lifecycles.AddAndroid(androidLifecycleBuilder =>
                {
                    androidLifecycleBuilder.OnNewIntent((_, intent) => CrossNFC.OnNewIntent(intent));
                    androidLifecycleBuilder.OnResume(_ => CrossNFC.OnResume());
                });
            });
#endif

            if (CrossNFC.Current is not null)
            {
                var nfcOptions = new NfcOptions();
                options?.Invoke(nfcOptions);
                CrossNFC.Current.Options = nfcOptions;

                builder.Services.AddSingleton(CrossNFC.Current);
            }
        }

        return builder;
    }
}
