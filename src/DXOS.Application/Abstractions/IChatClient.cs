namespace DXOS.Application.Abstractions;

public interface IChatClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
