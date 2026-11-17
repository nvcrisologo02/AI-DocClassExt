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
            // La sobrecarga de cuatro parámetros se conserva sin cambios para la
            // extracción: no envía temperature ni límite a los modelos de razonamiento.
            // El SDK actual ya sabe emitir 'max_completion_tokens'; esa ruta es la
            // sobrecarga de cinco parámetros (con esfuerzo de razonamiento).
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

        [Theory]
        [InlineData("none", "none")]
        [InlineData("minimal", "minimal")]
        [InlineData("low", "low")]
        [InlineData("medium", "medium")]
        [InlineData("high", "high")]
        [InlineData(" None ", "none")]
        [InlineData("HIGH", "high")]
        public void TryParseReasoningEffort_ValoresValidos_Devuelve(string valor, string normalizado)
        {
            var ok = OpenAiModelCapabilities.TryParseReasoningEffort(valor, out var level);

            ok.Should().BeTrue();
            level.ToString().Should().Be(normalizado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ultra")]
        [InlineData("xhigh")]
        public void TryParseReasoningEffort_ValoresInvalidos_DevuelveFalse(string? valor)
        {
            var ok = OpenAiModelCapabilities.TryParseReasoningEffort(valor, out var level);

            ok.Should().BeFalse();
            level.Should().Be(default(ChatReasoningEffortLevel));
        }

        [Fact]
        public void ConfigureChatOptions_Razonamiento_ConEsfuerzo_EnviaEsfuerzoYLimite()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5.6-luna", 0.0, 2000, "none");

            options.Temperature.Should().BeNull();
            options.MaxOutputTokenCount.Should().Be(2000);
            options.ReasoningEffortLevel.Should().Be(new ChatReasoningEffortLevel("none"));
        }

        [Fact]
        public void ConfigureChatOptions_Razonamiento_ConSufijo_EnviaEsfuerzo()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5.6-luna-dz", 0.0, 2000, "low");

            options.ReasoningEffortLevel.Should().Be(new ChatReasoningEffortLevel("low"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void ConfigureChatOptions_Razonamiento_SinLimite_EnviaSoloEsfuerzo(int? limite)
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5.6-luna", 0.0, limite, "none");

            options.MaxOutputTokenCount.Should().BeNull();
            options.ReasoningEffortLevel.Should().Be(new ChatReasoningEffortLevel("none"));
        }

        [Fact]
        public void ConfigureChatOptions_Razonamiento_EsfuerzoInvalido_NoLoEnvia()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5.6-luna", 0.0, 2000, "ultra");

            options.ReasoningEffortLevel.Should().BeNull();
            options.MaxOutputTokenCount.Should().Be(2000);
        }

        [Fact]
        public void ConfigureChatOptions_Razonamiento_EsfuerzoNulo_NoEnviaEsfuerzo()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5-mini", 0.0, 2000, null);

            options.ReasoningEffortLevel.Should().BeNull();
            options.MaxOutputTokenCount.Should().Be(2000);
        }

        [Fact]
        public void ConfigureChatOptions_Clasico_IgnoraEsfuerzo()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-4o-mini", 0.0, 150, "none");

            options.Temperature.Should().Be(0f);
            options.MaxOutputTokenCount.Should().Be(150);
            options.ReasoningEffortLevel.Should().BeNull();
        }

        [Fact]
        public void ConfigureChatOptions_SobrecargaAntigua_MantieneComportamiento()
        {
            var options = new ChatCompletionOptions();

            OpenAiModelCapabilities.ConfigureChatOptions(options, "gpt-5-mini", 0.0, 2000);

            options.Temperature.Should().BeNull();
            options.MaxOutputTokenCount.Should().BeNull();
            options.ReasoningEffortLevel.Should().BeNull();
        }
    }
}
