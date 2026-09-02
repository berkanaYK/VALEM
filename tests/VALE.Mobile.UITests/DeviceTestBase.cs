using System.Diagnostics;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace VALE.Mobile.UITests;

public abstract class DeviceTestBase
{
    protected AndroidDriver App => AppiumSetup.App;

    protected AppiumElement WaitForElement(string automationId, int timeoutSeconds = 30)
    {
        var timer = Stopwatch.StartNew();
        WebDriverException? lastError = null;
        while (timer.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var element = FindVisibleElement(automationId);
                if (element is not null)
                    return element;
            }
            catch (WebDriverException ex)
            {
                lastError = ex;
            }

            Thread.Sleep(250);
        }

        throw new AssertionException(
            $"'{automationId}' öğesi {timeoutSeconds} saniye içinde görünmedi. Son Appium hatası: {lastError?.Message ?? "yok"}");
    }

    protected bool IsVisible(string automationId) => FindVisibleElement(automationId) is not null;

    protected void SaveScreenshot(string name)
    {
        var directory = Environment.GetEnvironmentVariable("VALE_UI_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "device-ui-artifacts");

        Directory.CreateDirectory(directory);
        var safeName = string.Concat(name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var path = Path.Combine(directory, $"{safeName}.png");
        App.GetScreenshot().SaveAsFile(path);
        TestContext.AddTestAttachment(path, "Android gerçek cihaz ekran görüntüsü");
    }

    [TearDown]
    public void CaptureFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Failed)
            return;

        try
        {
            SaveScreenshot($"FAILED-{TestContext.CurrentContext.Test.Name}");
        }
        catch (Exception ex)
        {
            TestContext.Error.WriteLine($"Hata ekran görüntüsü alınamadı: {ex.Message}");
        }
    }

    private AppiumElement? FindVisibleElement(string automationId)
    {
        var selectors = new By[]
        {
            MobileBy.AccessibilityId(automationId),
            MobileBy.Id(automationId),
            MobileBy.Id($"{AppiumSetup.PackageName}:id/{automationId}")
        };

        foreach (var selector in selectors)
        {
            foreach (var element in App.FindElements(selector))
            {
                try
                {
                    if (element.Displayed)
                        return element;
                }
                catch (StaleElementReferenceException)
                {
                    // The MAUI visual tree was refreshed; retry with the next poll.
                }
            }
        }

        return null;
    }
}
