# ![NFC logo][logo] Plugin.NFC
A Cross-Platform NFC (_Near Field Communication_) plugin to easily read and write NFC tags in your application.

This plugin uses **NDEF** (_NFC Data Exchange Format_) for maximum compatibilty between NFC devices, tag types, and operating systems.

## Status
|Package|Build|NuGet|MyGet
|:---|:---|:---|:---
|Plugin.NFC | [![Build status](https://dev.azure.com/franckbour/franckbour/_apis/build/status/Plugin.NFC-CI)](https://dev.azure.com/franckbour/franckbour/_build/latest?definitionId=1) | ![Nuget](https://img.shields.io/nuget/v/Plugin.NFC.svg?label=Nuget) | ![MyGet](https://img.shields.io/myget/plugin-nfc/v/Plugin.NFC.svg?label=MyGet)

CI Feed : https://www.myget.org/F/plugin-nfc/api/v3/index.json

## Supported Platforms
Platform|Version|Tested on
:---|:---|:---
Android|4.4+|Google Nexus 5, Huawei Mate 10 Pro
iOS|11+|iPhone 7

> Windows is currently not supported. Pull Requests are welcomed! 

## Setup
### Android Specific
* Add NFC Permission `android.permission.NFC` and NFC feature `android.hardware.nfc` in your `AndroidManifest.xml`
```xml
<uses-permission android:name="android.permission.NFC" />
<uses-feature android:name="android.hardware.nfc" android:required="false" />
```

* Add the line `CrossNFC.Init(this)` in your `OnCreate()`
```csharp
protected override void OnCreate(Bundle savedInstanceState)
{
    TabLayoutResource = Resource.Layout.Tabbar;
    ToolbarResource = Resource.Layout.Toolbar;
    base.OnCreate(savedInstanceState);
    
    // Plugin NFC: Initialization
    CrossNFC.Init(this);

    global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
    LoadApplication(new App());
}
```
* Add the line `CrossNFC.OnResume()` in your `OnResume()`
```csharp
protected override void OnResume()
{
    base.OnResume();

    // Plugin NFC: Restart NFC listening on resume (needed for Android 10+) 
    CrossNFC.OnResume();
}
```

* Add the line `CrossNFC.OnNewIntent(intent)` in your `OnNewIntent()`
```csharp
protected override void OnNewIntent(Intent intent)
{
    base.OnNewIntent(intent);

    // Plugin NFC: Tag Discovery Interception
    CrossNFC.OnNewIntent(intent);
}
```

### iOS Specific

> iOS 13+ is required for writing tags.

An iPhone 7+ and iOS 11+ are required in order to use NFC with iOS devices.

* Add `Near Field Communication Tag Reading` capabilty in your `Entitlements.plist`
```xml
<key>com.apple.developer.nfc.readersession.formats</key>
<array>
    <string>NDEF</string>
    <string>TAG</string>
</array>
```

* Add a NFC feature description in your Info.plist
```xml
<key>NFCReaderUsageDescription</key>
<string>NFC tag to read NDEF messages into the application</string>
```

* Add these lines in your Info.plist if you want to interact with ISO 7816 compatible tags
```xml
<key>com.apple.developer.nfc.readersession.iso7816.select-identifiers</key>
<string>com.apple.developer.nfc.readersession.iso7816.select-identifiers</string>
```

## API Usage

Before to use the plugin, please check if NFC feature is supported by the platform using `CrossNFC.IsSupported`.

To get the current platform implementation of the plugin, please call `CrossNFC.Current`:
* Check `CrossNFC.Current.IsAvailable` to verify if NFC is available.
* Check `CrossNFC.Current.IsEnabled` to verify if NFC is enabled. 
* Register events:
```csharp
// Event raised when a ndef message is received.
CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
// Event raised when a ndef message has been published.
CrossNFC.Current.OnMessagePublished += Current_OnMessagePublished;
// Event raised when a tag is discovered. Used for publishing.
CrossNFC.Current.OnTagDiscovered += Current_OnTagDiscovered;
// Event raised when NFC listener status changed
CrossNFC.Current.OnTagListeningStatusChanged += Current_OnTagListeningStatusChanged;

// Android Only:
// Event raised when NFC state has changed.
CrossNFC.Current.OnNfcStatusChanged += Current_OnNfcStatusChanged;

// iOS Only: 
// Event raised when a user cancelled NFC session.
CrossNFC.Current.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
```

### Launch app when a compatible tag is detected on Android

In Android, you can use `IntentFilter` attribute on your `MainActivity` to initialize tag listening.
```csharp
[IntentFilter(new[] { NfcAdapter.ActionNdefDiscovered }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "application/com.companyname.yourapp")]
public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity 
{
    ...
}
```
In this case, we will receive tags supporting NDEF mapped to a specified MIME Type "`application/com.companyname.yourapp`".

### Read a tag
* Start listening with `CrossNFC.Current.StartListening()`.
* When a NDEF message is received, the event `OnMessageReceived` is raised.

### Write a tag
* To write a tag, call `CrossNFC.Current.StartPublishing()`
* Then `CrossNFC.Current.PublishMessage(ITagInfo)` when `OnTagDiscovered` event is raised. 
* Do not forget to call `CrossNFC.Current.StopPublishing()` once the tag has been written.

### Clear a tag
* To clear a tag, call `CrossNFC.Current.StartPublishing(clearMessage: true)`
* Then `CrossNFC.Current.PublishMessage(ITagInfo)` when `OnTagDiscovered` event is raised.
* Do not forget to call `CrossNFC.Current.StopPublishing()` once the tag has been cleared.


For more examples, see sample application in the repository.

### Customizing UI messages
* Set a new `NfcConfiguration` object to `CrossNFC.Current` with `SetConfiguration(NfcConfiguration cfg)` method like below

```Csharp
// Custom NFC configuration (ex. UI messages in French)
CrossNFC.Current.SetConfiguration(new NfcConfiguration
{
    Messages = new UserDefinedMessages
    {
        NFCWritingNotSupported = "L'écriture des TAGs NFC n'est pas supporté sur cet appareil",
        NFCDialogAlertMessage = "Approchez votre appareil du tag NFC",
        NFCErrorRead = "Erreur de lecture. Veuillez rééssayer",
        NFCErrorEmptyTag = "Ce tag est vide",
        NFCErrorReadOnlyTag = "Ce tag n'est pas accessible en écriture",
        NFCErrorCapacityTag = "La capacité de ce TAG est trop basse",
        NFCErrorMissingTag = "Aucun tag trouvé",
        NFCErrorMissingTagInfo = "Aucune information à écrire sur le tag",
        NFCErrorNotSupportedTag = "Ce tag n'est pas supporté",
        NFCErrorNotCompliantTag = "Ce tag n'est pas compatible NDEF",
        NFCErrorWrite = "Aucune information à écrire sur le tag",
        NFCSuccessRead = "Lecture réussie",
        NFCSuccessWrite = "Ecriture réussie",
        NFCSuccessClear = "Effaçage réussi"
    }
});
```

## Contributing
Feel free to contribute. PRs are accepted and welcomed.

## Credits
Inspired by the great work of many developers. Many thanks to:
- James Montemagno ([@jamesmontemagno](https://github.com/jamesmontemagno)).
- Matthew Leibowitz ([@mattleibow](https://github.com/mattleibow)) for [Xamarin.Essentials PR #131](https://github.com/xamarin/Essentials/pull/131).
- Alessandro Pozone ([@poz1](https://github.com/poz1)) for [NFCForms](https://github.com/poz1/NFCForms).
- Ultz ([@ultz](https://github.com/Ultz)) for [XNFC](https://github.com/Ultz/XNFC).
- Sven-Michael Stübe ([@smstuebe](https://github.com/smstuebe)) for [xamarin-nfc](https://github.com/smstuebe/xamarin-nfc).

[logo]: https://github.com/franckbour/Plugin.NFC/raw/master/art/nfc48.png "NFC Logo"
