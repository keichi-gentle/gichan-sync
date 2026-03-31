using GichanDiary.Helpers;
using GichanDiary.Models;

namespace GichanDiary.Tests.Helpers;

public class ExcelColumnMapperTests
{
    [Theory]
    [InlineData("수유", EventCategory.수유)]
    [InlineData("배변", EventCategory.배변)]
    [InlineData("위생", EventCategory.위생관리)]
    [InlineData("신체", EventCategory.신체측정)]
    [InlineData("건강", EventCategory.건강관리)]
    [InlineData("기타", EventCategory.기타)]
    public void ParseCategory_NewFormat_MapsCorrectly(string excel, EventCategory expected)
        => Assert.Equal(expected, ExcelColumnMapper.ParseCategory(excel));

    [Theory]
    [InlineData("위생관리", EventCategory.위생관리)]
    [InlineData("통증", EventCategory.건강관리)]
    public void ParseCategory_LegacyFormat_StillWorks(string excel, EventCategory expected)
        => Assert.Equal(expected, ExcelColumnMapper.ParseCategory(excel));

    [Theory]
    [InlineData(EventCategory.수유, "수유")]
    [InlineData(EventCategory.위생관리, "위생")]
    [InlineData(EventCategory.신체측정, "신체")]
    [InlineData(EventCategory.건강관리, "건강")]
    public void ToCategoryString_WritesNewFormat(EventCategory cat, string expected)
        => Assert.Equal(expected, ExcelColumnMapper.ToCategoryString(cat));

    [Fact]
    public void ParseAmount_Number_ReturnsNumber() => Assert.Equal(80, ExcelColumnMapper.ParseAmount("80"));

    [Fact]
    public void ParseAmount_Dash_ReturnsNull() => Assert.Null(ExcelColumnMapper.ParseAmount("-"));

    [Fact]
    public void ParseAmount_Empty_ReturnsNull() => Assert.Null(ExcelColumnMapper.ParseAmount(""));

    [Fact]
    public void FormatAmount_WithValue_ReturnsString() => Assert.Equal("80", ExcelColumnMapper.FormatAmount(80));

    [Fact]
    public void FormatAmount_Null_ReturnsDash() => Assert.Equal("-", ExcelColumnMapper.FormatAmount(null));
}
