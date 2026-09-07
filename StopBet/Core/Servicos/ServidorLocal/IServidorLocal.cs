namespace StopBet.Core.Servicos.ServidorLocal;

// Contrato platform-agnostico para o servidor HTTP local que serve de ponte com a
// extensao de navegador. Implementado apenas no Windows (ServidorLocalWindows) - no
// Android essa ponte nao se aplica, pois o bloqueio la sera feito via VpnService,
// sem depender de uma extensao de navegador.
public interface IServidorLocal
{
    bool EstaAtivo { get; }

    void Iniciar();

    void Parar();
}
