using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace VALE.Mobile.UITests;

[SetUpFixture]
public sealed class AppiumSetup
{
    private static AndroidDriver? _driver;

    public const string PackageName = "com.berkanayk.vale";
    public const string ActivityName = "com.berkanayk.vale.MainActivity";

    public static AndroidDriver App => _driver
        ?? throw new InvalidOperationException("Appium Android oturumu başlatılmadı.");

    [OneTimeSetUp]
    public void StartSession()
    {
        var apkPath = Environment.GetEnvironmentVariable("VALE_APK_PATH");
        if (string.IsNullOrWhiteSpace(apkPath))
            throw new InvalidOperationException("VALE_APK_PATH, test edilecek Release APK dosyasını göstermelidir.");

        apkPath = Path.GetFullPath(apkPath);
        if (!File.Exists(apkPath))
            throw new FileNotFoundException("Test edilecek VALE APK dosyası bulunamadı.", apkPath);

        var serverUrl = Environment.GetEnvironmentVariable("VALE_APPIUM_SERVER_URL")
            ?? "http://127.0.0.1:4723";
        var deviceId = Environment.GetEnvironmentVariable("VALE_ANDROID_UDID");

        var options = new AppiumOptions
        {
            AutomationName = "UiAutomator2",
            DeviceName = string.IsNullOrWhiteSpace(deviceId) ? "VALE Android cihazı" : deviceId,
            App = apkPath
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
            options.AddAdditionalAppiumOption("udid", deviceId);

        options.AddAdditionalAppiumOption("appPackage", PackageName);
        options.AddAdditionalAppiumOption("appActivity", ActivityName);
        options.AddAdditionalAppiumOption("appWaitActivity", ActivityName);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("noReset", false);
        options.AddAdditionalAppiumOption("fullReset", false);
        options.AddAdditionalAppiumOption("forceAppLaunch", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);

        _driver = new AndroidDriver(new Uri(serverUrl), options, TimeSpan.FromMinutes(3));
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    }

    [OneTimeTearDown]
    public void StopSession()
    {
        try
        {
            _driver?.Quit();
        }
        finally
        {
            _driver?.Dispose();
            _driver = null;
        }
    }
}
