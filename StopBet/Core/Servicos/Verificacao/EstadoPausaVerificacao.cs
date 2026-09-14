namespace StopBet.Core.Servicos.Verificacao;

// Estado compartilhado (singleton) que permite pausar temporariamente a verificacao
// de dominios novos - por exemplo, para o usuario navegar livremente sem gerar
// chamadas a API do Gemini durante uma atividade especifica (assistir uma transmissao
// esportiva, por exemplo). Dominios ja conhecidos continuam bloqueados normalmente
// enquanto pausado; so a classificacao de dominios nunca vistos e suspensa.
//
// Sempre comeca "nao pausado" a cada reinicio do app - por seguranca, a protecao
// nunca fica pausada sem o usuario perceber depois de fechar e abrir o StopBet de novo.
public sealed class EstadoPausaVerificacao
{
    public bool Pausado { get; set; }
}
