using System.Reflection;
using MainProject.Application.UseCases.Answers;

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
}
