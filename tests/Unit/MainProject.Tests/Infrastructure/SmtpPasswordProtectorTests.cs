using System.Security.Cryptography;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace MainProject.Tests.Infrastructure;

public sealed class SmtpPasswordProtectorTests
{
    [Fact]
    public void Protect_StoresVersionedCipherTextAndRestoresPassword()
    {
        var protector = new SmtpPasswordProtector(new EphemeralDataProtectionProvider());

        var protectedValue = protector.Protect("smtp-password");

        Assert.StartsWith(SmtpPasswordProtector.ProtectedValuePrefix, protectedValue);
        Assert.DoesNotContain("smtp-password", protectedValue, StringComparison.Ordinal);
        Assert.Equal("smtp-password", protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Unprotect_AcceptsPlainTextOnlyForLegacyMigration()
    {
        var protector = new SmtpPasswordProtector(new EphemeralDataProtectionProvider());

        Assert.False(protector.IsCurrentFormat("legacy-password"));
        Assert.Equal("legacy-password", protector.Unprotect("legacy-password"));
    }

    [Fact]
    public void Unprotect_RejectsCipherTextFromAnotherKeyRing()
    {
        var firstProtector = new SmtpPasswordProtector(new EphemeralDataProtectionProvider());
        var secondProtector = new SmtpPasswordProtector(new EphemeralDataProtectionProvider());
        var protectedValue = firstProtector.Protect("smtp-password");

        Assert.Throws<CryptographicException>(() => secondProtector.Unprotect(protectedValue));
    }
}
