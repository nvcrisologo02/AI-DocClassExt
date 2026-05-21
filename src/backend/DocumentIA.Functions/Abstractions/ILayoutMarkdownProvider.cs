using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Abstractions;

public interface ILayoutMarkdownProvider
{
    Task<ExtraerMarkdownLayoutResultado> ExtraerMarkdownAsync(
        ExtraerMarkdownLayoutInput input,
        CancellationToken cancellationToken = default);
}