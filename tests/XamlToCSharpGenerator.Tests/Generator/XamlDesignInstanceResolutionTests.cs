using XamlToCSharpGenerator.LanguageService.Completion;

namespace XamlToCSharpGenerator.Tests.Generator;

public class XamlDesignInstanceResolutionTests
{
    [Theory]
    [InlineData("{d:DesignInstance vm:MainViewModel}", "vm:MainViewModel")]
    [InlineData("{d:DesignInstance Type=vm:MainViewModel}", "vm:MainViewModel")]
    [InlineData("{d:DesignInstance Type=vm:MainViewModel, IsDesignTimeCreatable=True}", "vm:MainViewModel")]
    [InlineData("{d:DesignInstance local:MyViewModel}", "local:MyViewModel")]
    [InlineData("{d:DesignInstance Type=local:MyViewModel, IsDesignTimeCreatable=False}", "local:MyViewModel")]
    public void TryExtractDesignInstanceType_ValidMarkup_ReturnsTypeToken(string attributeValue, string expectedType)
    {
        var result = XamlSemanticSourceTypeResolver.TryExtractDesignInstanceType(attributeValue, out var typeToken);

        Assert.True(result);
        Assert.Equal(expectedType, typeToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{x:Null}")]
    [InlineData("{StaticResource MyResource}")]
    [InlineData("{Binding Path=Name}")]
    [InlineData("{d:DesignData Source=../SampleData.xaml}")]
    [InlineData("{d:DesignInstance}")]
    public void TryExtractDesignInstanceType_InvalidOrNonDesignInstance_ReturnsFalse(string attributeValue)
    {
        var result = XamlSemanticSourceTypeResolver.TryExtractDesignInstanceType(attributeValue, out _);

        Assert.False(result);
    }
}
