using DocumentIA.Functions.Services;
using FluentAssertions;
using OpenAI.Chat;
using Xunit;

#nullable enable

namespace DocumentIA.Tests.Unit.Services
{
    /// <summary>
    /// Tests de OpenAiModelCapabilities: los modelos de razonamiento (familia gpt-5,
    /// o-series) rechazan el parámetro temperature con HTTP 400, por lo que solo debe
    /// enviarse a los modelos clásicos (gpt-4x).
    /// </summary>
    public class OpenAiModelCapabilitiesTests
    {
        [Theory]
        // Modelos clásicos: admiten temperature
        [InlineData("gpt-4o-mini", true)]
        [InlineData("gpt-4.1-mini", true)]
        [InlineData("gpt-4o", true)]
        [InlineData("gpt-35-turbo", true)]
        // Nombres que empiezan por "o" pero no son o-series
        [InlineData("openai-custom", true)]
        // Sin deployment conocido: comportamiento actual (enviar temperature)
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        // Familia gpt-5: rechaza temperature
        [InlineData("gpt-5", false)]
        [InlineData("gpt-5-mini", false)]
        [InlineData("gpt-5-nano", false)]
        [InlineData("gpt-5.1-mini", false)]
        [InlineData("GPT-5-MINI", false)]
        // o-series: rechaza temperature
        [InlineData("o1", false)]
        [InlineData("o1-mini", false)]
        [InlineData("o3-mini", false)]
        [InlineData("o4-mini", false)]
        public void SupportsTemperature_DetectaFamiliaDelModelo(string? deploymentName, bool esperado)
        {
            OpenAiModelCapabilities.SupportsTemperature(deploymentName).Should().Be(esperado);
        }

        [Fact]
        public void ApplyTemperature_ModeloClasico_AsignaLaTemperaturaConfigurada()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ApplyTemperature(options, "gpt-4o-mini", 0.0);

            options.Temperature.Should().Be(0f);
        }

        [Fact]
        public void ApplyTemperature_ModeloRazonamiento_NoEnviaTemperature()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ApplyTemperature(options, "gpt-5-mini", 0.0);

            options.Temperature.Should().BeNull();
        }

        [Fact]
        public void ApplyTemperature_ModeloClasico_RespetaValoresNoCero()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ApplyTemperature(options, "gpt-4.1-mini", 0.7);

            options.Temperature.Should().BeApproximately(0.7f, 0.0001f);
        }

        [Fact]
        public void ConfigureChatOptions_ModeloClasico_AsignaTemperatureYMaxTokens()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-4o-mini", 0.0, 2000);

            options.Temperature.Should().Be(0f);
            options.MaxOutputTokenCount.Should().Be(2000);
        }

        [Fact]
        public void ConfigureChatOptions_ModeloRazonamiento_NoEnviaTemperatureNiMaxTokens()
        {
            // El SDK estable (OpenAI 2.1.0) serializa MaxOutputTokenCount como
            // 'max_tokens', que la familia gpt-5 rechaza con HTTP 400
            // (exige 'max_completion_tokens'): no se debe enviar ninguno de los dos.
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5-mini", 0.0, 2000);

            options.Temperature.Should().BeNull();
            options.MaxOutputTokenCount.Should().BeNull();
        }

        [Fact]
        public void ConfigureChatOptions_MaxTokensNulo_NoAsignaLimite()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-4o-mini", 0.0, null);

            options.Temperature.Should().Be(0f);
            options.MaxOutputTokenCount.Should().BeNull();
        }
    }
}
