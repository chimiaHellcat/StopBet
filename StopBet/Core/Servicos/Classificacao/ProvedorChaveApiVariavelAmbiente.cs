namespace StopBet.Core.Servicos.Classificacao;

// Funciona no Windows. Apps Android nao herdam variaveis de ambiente do shell,
// entao ObterChave sempre retorna null la. Quando a etapa Android for implementada,
// troque a implementacao registrada no DI (por exemplo SecureStorage) sem mexer no servico de classificacao.
public sealed class ProvedorChaveApiVariavelAmbiente : IProvedorChaveApi
{
    private const string NomeVariavelAmbiente = "GEMINI_API_KEY";

    public string? ObterChave() => Environment.GetEnvironmentVariable(NomeVariavelAmbiente);
}
