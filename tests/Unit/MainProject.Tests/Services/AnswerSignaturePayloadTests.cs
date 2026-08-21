using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MainProject.Application.UseCases.Answers;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;

namespace MainProject.Tests.Services;

public sealed class AnswerSignaturePayloadTests
{
    [Fact]
    public void TryDecodeBase64_AcceptsSignatureWithoutTrailingPadding()
    {
        byte[] expected = [1, 2, 3, 4, 5];
        var unpaddedSignature = Convert.ToBase64String(expected).TrimEnd('=');
        Assert.Equal(3, unpaddedSignature.Length % 4);

        var method = typeof(AnswerService).GetMethod(
            "TryDecodeBase64",
            BindingFlags.NonPublic | BindingFlags.Static);

        var decoded = Assert.IsType<byte[]>(method?.Invoke(null, [unpaddedSignature]));

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void TryDecodeBase64_RejectsImpossiblePayloadLength()
    {
        var method = typeof(AnswerService).GetMethod(
            "TryDecodeBase64",
            BindingFlags.NonPublic | BindingFlags.Static);

        var decoded = Assert.IsType<byte[]>(method?.Invoke(null, ["A"]));

        Assert.Empty(decoded);
    }

    [Fact]
    public void BuildSignatureInfo_UsesRecordedParticipantWhenCertificateCannotBeRead()
    {
        var answer = new AnswerRecord
        {
            Csp = Convert.ToBase64String("not a CMS signature"u8.ToArray()),
            SignerName = "Импортированный пользователь"
        };

        var signatureInfo = InvokeBuildSignatureInfo(answer);

        Assert.True(signatureInfo.IsSigned);
        Assert.Equal("Импортированный пользователь", signatureInfo.SignedBy);
        Assert.Equal("Проверка недоступна", signatureInfo.Status);
    }

    [Fact]
    public void BuildSignatureInfo_ReadsLegacyDoubleBase64CmsSignature()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Тестовый подписант",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var cms = new SignedCms(new ContentInfo("legacy payload"u8.ToArray()));
        cms.ComputeSignature(new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        });
        var legacyBase64 = Convert.ToBase64String(cms.Encode());
        var importedBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(legacyBase64));

        var signatureInfo = InvokeBuildSignatureInfo(new AnswerRecord { Csp = importedBase64 });

        Assert.True(signatureInfo.IsSigned);
        Assert.Equal("Тестовый подписант", signatureInfo.SignedBy);
    }

    [Fact]
    public void GetSignerDisplayName_PrefersPersonalCertificateAttributesOverOrganizationCommonName()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Тестовая организация, SN=Оболонская, G=Екатерина Анатольевна",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var method = typeof(AnswerService).GetMethod(
            "GetSignerDisplayName",
            BindingFlags.NonPublic | BindingFlags.Static);

        var signerName = Assert.IsType<string>(method?.Invoke(null, [certificate, null]));

        Assert.Equal("Оболонская Екатерина Анатольевна", signerName);
    }

    private static AnswerSignatureInfoViewModel InvokeBuildSignatureInfo(AnswerRecord answer)
    {
        var method = typeof(AnswerService).GetMethod(
            "BuildSignatureInfo",
            BindingFlags.NonPublic | BindingFlags.Static);

        return Assert.IsType<AnswerSignatureInfoViewModel>(method?.Invoke(null, [answer]));
    }
}
