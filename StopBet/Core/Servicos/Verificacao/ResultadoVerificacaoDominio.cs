using StopBet.Core.Modelos;

namespace StopBet.Core.Servicos.Verificacao;

public sealed record ResultadoVerificacaoDominio(bool DeveBloquear, string Categoria, OrigemDominio Origem);
