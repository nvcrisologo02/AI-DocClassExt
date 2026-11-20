#nullable enable
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class UsoOpenAiMapperTests
{
    [Fact]
    public void LeerCacheWriteTokens_ConCampo_DevuelveElValor()
    {
        var cuerpo = BinaryData.FromString(
            "{\"usage\":{\"prompt_tokens_details\":{\"cached_tokens\":10,\"cache_write_tokens\":1408}}}");

        UsoOpenAiMapper.LeerCacheWriteTokens(cuerpo).Should().Be(1408);
    }

    [Theory]
    [InlineData("{\"usage\":{}}")]
    [InlineData("{\"usage\":{\"prompt_tokens_details\":{\"cached_tokens\":10}}}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"usage\":{\"prompt_tokens_details\":{\"cache_write_tokens\":\"mil\"}}}")]
    [InlineData("no es json")]
    [InlineData("")]
    public void LeerCacheWriteTokens_SinCampoOMalformado_DevuelveNuloSinLanzar(string json)
    {
        UsoOpenAiMapper.LeerCacheWriteTokens(BinaryData.FromString(json)).Should().BeNull();
    }

    [Fact]
    public void LeerCacheWriteTokens_CuerpoNulo_DevuelveNulo()
    {
        UsoOpenAiMapper.LeerCacheWriteTokens(null).Should().BeNull();
    }

    [Fact]
    public void Mapear_ConTokensEscritura_LosCopia()
    {
        var consumo = UsoOpenAiMapper.Mapear(null, "clasificar", "fase1", "modelo", tokensEscrituraCache: 1408);

        consumo.TokensEscrituraCache.Should().Be(1408);
    }
}
