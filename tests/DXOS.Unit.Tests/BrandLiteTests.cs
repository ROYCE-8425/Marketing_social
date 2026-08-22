using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class BrandLiteTests
{
    [Theory]
    [InlineData("Khóa học lập trình thực chiến")]
    [InlineData("Ra mắt giải pháp tự động hóa")]
    [InlineData("Ưu đãi học phí 30% cho sinh viên")]
    public void BrandLite_CleanCopy_PassesValidation(string copy)
    {
        BrandLite.Validate(copy);
    }

    [Theory]
    [InlineData("Sản phẩm cam kết 100% việc làm")]
    [InlineData("Dịch vụ số 1 việt nam hiện nay")]
    [InlineData("Cam đoan hoàn tiền vô điều kiện")]
    [InlineData("Chữa bách bệnh")]
    [InlineData("Chiêu trò lừa đảo khách hàng")]
    [InlineData("Đối thủ giả cạnh tranh không lành mạnh")]
    public void BrandLite_ProhibitedCopy_ThrowsBrandBlocked(string copy)
    {
        var ex = Assert.Throws<DomainRuleException>(() => BrandLite.Validate(copy));
        Assert.Equal("BrandBlocked", ex.Code);
    }
}
