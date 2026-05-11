using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Abstractions;

public interface IPromptDataProvider
{
    Task<PromptResultado> EjecutarPromptAsync(PromptActivityInput input, CancellationToken cancellationToken = default);
}
