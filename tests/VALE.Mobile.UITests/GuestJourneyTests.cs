using NUnit.Framework;

namespace VALE.Mobile.UITests;

[TestFixture]
[Category("RealDevice")]
[NonParallelizable]
public sealed class GuestJourneyTests : DeviceTestBase
{
    [Test]
    public void MinimalLoginRegistrationAndAndroidBackFlowWorks()
    {
        var email = WaitForElement("login-email");
        var password = WaitForElement("login-password");
        var rememberDevice = WaitForElement("remember-device");
        var login = WaitForElement("login-submit");

        Assert.Multiple(() =>
        {
            Assert.That(email.Enabled, Is.True, "E-posta alanı kullanılabilir olmalı.");
            Assert.That(password.Enabled, Is.True, "Parola alanı kullanılabilir olmalı.");
            Assert.That(login.Enabled, Is.True, "Giriş düğmesi kullanılabilir olmalı.");
        });

        rememberDevice.Click();
        Assert.That(rememberDevice.GetDomAttribute("checked"), Is.EqualTo("true").IgnoreCase,
            "Bu cihazı hatırla seçeneği dokunarak etkinleşmelidir.");

        WaitForElement("login-options-toggle").Click();
        Assert.That(WaitForElement("email-code-login-open").Displayed, Is.True,
            "İsteğe bağlı e-posta kodu girişi açılabilmelidir.");

        WaitForElement("register-open").Click();
        Assert.Multiple(() =>
        {
            Assert.That(WaitForElement("register-account-type").Displayed, Is.True);
            Assert.That(WaitForElement("register-name").Displayed, Is.True);
            Assert.That(WaitForElement("register-email").Displayed, Is.True);
            Assert.That(WaitForElement("register-company-name").Displayed, Is.True);
            Assert.That(WaitForElement("register-submit").Displayed, Is.True);
            Assert.That(IsVisible("register-password"), Is.False,
                "Varsayılan e-posta kodu kaydında parola alanı kapalı kalmalıdır.");
            Assert.That(IsVisible("register-company-code"), Is.False,
                "İleri firma ayrıntıları ilk açılışta gizli kalmalıdır.");
        });

        App.Navigate().Back();
        Assert.That(WaitForElement("login-email").Displayed, Is.True,
            "Android sistem geri hareketi kayıt ekranından giriş ekranına dönmelidir.");

        SaveScreenshot(nameof(MinimalLoginRegistrationAndAndroidBackFlowWorks));
    }
}
