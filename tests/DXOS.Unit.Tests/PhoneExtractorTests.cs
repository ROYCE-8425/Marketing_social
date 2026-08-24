using DXOS.Application;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class PhoneExtractorTests
{
    [Theory]
    [InlineData("0912345678", "0912345678")]
    [InlineData("0387654321", "0387654321")]
    [InlineData("0561234567", "0561234567")]
    [InlineData("0709876543", "0709876543")]
    [InlineData("0899887766", "0899887766")]
    [InlineData("+84912345678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData("(+84) 912 345 678", "0912345678")]
    [InlineData("0912 345 678", "0912345678")]
    [InlineData("0912-345-678", "0912345678")]
    [InlineData("Alo shop ơi tư vấn giúp mình qua số 0987654321 với ạ", "0987654321")]
    [InlineData("Giao hàng về địa chỉ quận 1, SĐT: +84903123456 nhé", "0903123456")]
    public void ExtractFirstPhoneNumber_FindsValidVietnamesePhone(string input, string expected)
    {
        var result = PhoneExtractor.ExtractFirstPhoneNumber(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Không có số điện thoại")]
    [InlineData("12345")]
    [InlineData("0123456789")] // 01 is not a modern valid VN mobile prefix
    public void ExtractFirstPhoneNumber_ReturnsNull_ForInvalidInput(string input)
    {
        var result = PhoneExtractor.ExtractFirstPhoneNumber(input);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractAllPhoneNumbers_ReturnsMultiplePhonesWithoutDuplicates()
    {
        var text = "Liên hệ 0912345678 hoặc 0987654321, cũng có thể gọi lại 0912345678 nhé";
        var results = PhoneExtractor.ExtractAllPhoneNumbers(text);

        Assert.Equal(2, results.Count);
        Assert.Contains("0912345678", results);
        Assert.Contains("0987654321", results);
    }
}
