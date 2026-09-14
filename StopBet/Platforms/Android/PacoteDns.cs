using System.Text;

namespace StopBet.Platforms.Android;

// Funcoes puras de leitura/montagem de pacotes IPv4 + UDP + DNS a partir dos bytes brutos
// que o VpnService le do tun. Sem dependencia de Android aqui de proposito - facilita testar
// esta parte isoladamente (ex.: em um teste unitario com bytes de exemplo) sem precisar de
// emulador, o que ajuda bastante a validar o parser antes de integrar ao loop de verdade.
public static class PacoteDns
{
    public readonly record struct CabecalhoIPv4(
        byte[] EnderecoOrigem, byte[] EnderecoDestino, byte Protocolo, int OffsetDados, int TamanhoCabecalho);

    public readonly record struct CabecalhoUdp(int PortaOrigem, int PortaDestino, int OffsetDados);

    public readonly record struct Consulta(string Dominio, int TamanhoConsulta);

    public static CabecalhoIPv4? TentarLerCabecalhoIPv4(byte[] pacote, int tamanho)
    {
        if (tamanho < 20) return null;

        var versao = (pacote[0] & 0xF0) >> 4;
        if (versao != 4) return null; // so IPv4 neste prototipo - IPv6 fica para uma iteracao futura

        var ihl = (pacote[0] & 0x0F) * 4; // tamanho do cabecalho em bytes (IHL vem em palavras de 4 bytes)
        if (ihl < 20 || tamanho < ihl) return null;

        var protocolo = pacote[9];
        var origem = pacote[12..16];
        var destino = pacote[16..20];

        return new CabecalhoIPv4(origem, destino, protocolo, ihl, ihl);
    }

    public static CabecalhoUdp? TentarLerCabecalhoUdp(byte[] pacote, int offset)
    {
        if (pacote.Length < offset + 8) return null;

        var portaOrigem = (pacote[offset] << 8) | pacote[offset + 1];
        var portaDestino = (pacote[offset + 2] << 8) | pacote[offset + 3];

        return new CabecalhoUdp(portaOrigem, portaDestino, offset + 8);
    }

    // Le a secao "Question" de uma mensagem DNS (RFC 1035, 4.1.2) a partir do cabecalho de 12 bytes
    // e decodifica o QNAME (sequencia de labels prefixados por tamanho, terminando em 0x00).
    // Nao segue ponteiros de compressao aqui de proposito: numa CONSULTA (nao resposta), o QNAME
    // e sempre escrito por extenso, nunca comprimido, entao nao precisamos desse caso.
    public static Consulta? TentarLerConsulta(byte[] pacote, int offsetDnsInicio, int tamanhoPacote)
    {
        const int tamanhoCabecalhoDns = 12;
        if (tamanhoPacote < offsetDnsInicio + tamanhoCabecalhoDns) return null;

        var qdcount = (pacote[offsetDnsInicio + 4] << 8) | pacote[offsetDnsInicio + 5];
        if (qdcount < 1) return null; // sem pergunta - nao e uma consulta que nos interessa

        var cursor = offsetDnsInicio + tamanhoCabecalhoDns;
        var labels = new List<string>();

        while (true)
        {
            if (cursor >= tamanhoPacote) return null;
            var tamanhoLabel = pacote[cursor];

            if (tamanhoLabel == 0)
            {
                cursor++;
                break; // fim do QNAME
            }

            if ((tamanhoLabel & 0xC0) != 0) return null; // ponteiro de compressao - nao esperado numa consulta
            if (cursor + 1 + tamanhoLabel > tamanhoPacote) return null;

            labels.Add(Encoding.ASCII.GetString(pacote, cursor + 1, tamanhoLabel));
            cursor += 1 + tamanhoLabel;
        }

        // QTYPE (2 bytes) + QCLASS (2 bytes) vem logo depois do QNAME
        if (cursor + 4 > tamanhoPacote) return null;
        cursor += 4;

        var dominio = string.Join('.', labels);
        var tamanhoConsulta = cursor - offsetDnsInicio;

        return string.IsNullOrEmpty(dominio) ? null : new Consulta(dominio, tamanhoConsulta);
    }

    // Monta o corpo de uma resposta DNS que responde com 127.0.0.1 para o dominio bloqueado -
    // mesma logica do bloqueio via 'hosts' no Windows, so que sintetizada aqui na hora em vez de
    // lida de um arquivo. Reaproveita o cabecalho + pergunta originais (obrigatorio: o
    // solicitante so aceita a resposta se ela ecoar a mesma pergunta que ele mandou).
    public static byte[] MontarRespostaBloqueio(byte[] pacoteOriginal, int offsetDnsInicio, int tamanhoConsulta)
    {
        var resposta = new byte[tamanhoConsulta + 16]; // + registro de resposta (A record) de 16 bytes
        Array.Copy(pacoteOriginal, offsetDnsInicio, resposta, 0, tamanhoConsulta);

        // Flags: QR=1 (resposta), Opcode=0, AA=0, TC=0, RD=copiado da consulta, RA=1, RCODE=0
        var rdOriginal = (byte)(pacoteOriginal[offsetDnsInicio + 2] & 0x01);
        resposta[2] = (byte)(0x80 | rdOriginal);
        resposta[3] = 0x80;

        // ANCOUNT = 1 (tinhamos 0 na consulta original)
        resposta[6] = 0x00;
        resposta[7] = 0x01;

        // NSCOUNT/ARCOUNT: zera explicitamente em vez de deixar o que veio copiado da consulta
        // original. Resolvers modernos (incluindo o do proprio Android) costumam mandar um
        // registro adicional OPT (EDNS0) na consulta, o que deixaria ARCOUNT=1 copiado aqui sem
        // que a resposta de fato inclua esse registro - uma resposta malformada que um cliente
        // mais rigoroso poderia descartar silenciosamente.
        resposta[8] = 0x00;
        resposta[9] = 0x00;
        resposta[10] = 0x00;
        resposta[11] = 0x00;

        var cursor = tamanhoConsulta;

        // NAME: ponteiro de compressao apontando de volta para o QNAME no offset 12 (0xC0 0x0C)
        resposta[cursor++] = 0xC0;
        resposta[cursor++] = 0x0C;

        // TYPE = A (1)
        resposta[cursor++] = 0x00;
        resposta[cursor++] = 0x01;

        // CLASS = IN (1)
        resposta[cursor++] = 0x00;
        resposta[cursor++] = 0x01;

        // TTL = 60s - curto de proposito: se o dominio for desbloqueado depois, o cache do
        // solicitante nao segura o bloqueio por muito tempo
        resposta[cursor++] = 0x00;
        resposta[cursor++] = 0x00;
        resposta[cursor++] = 0x00;
        resposta[cursor++] = 0x3C;

        // RDLENGTH = 4 (um endereco IPv4)
        resposta[cursor++] = 0x00;
        resposta[cursor++] = 0x04;

        // RDATA = 127.0.0.1
        resposta[cursor++] = 127;
        resposta[cursor++] = 0;
        resposta[cursor++] = 0;
        resposta[cursor] = 1;

        return resposta;
    }

    // Envolve um corpo de resposta DNS (bloqueio ou repasse do upstream) num pacote IPv4 + UDP
    // completo, com origem/destino invertidos em relacao a consulta original - e assim que o
    // solicitante reconhece a resposta como sendo para ele.
    public static byte[] MontarPacoteResposta(CabecalhoIPv4 ipOriginal, CabecalhoUdp udpOriginal, byte[] corpoDns)
    {
        const int tamanhoIp = 20;
        const int tamanhoUdp = 8;
        var pacote = new byte[tamanhoIp + tamanhoUdp + corpoDns.Length];

        // --- cabecalho IPv4 ---
        pacote[0] = 0x45; // versao 4, IHL 5 (20 bytes, sem opcoes)
        pacote[1] = 0x00; // ToS
        var tamanhoTotal = tamanhoIp + tamanhoUdp + corpoDns.Length;
        pacote[2] = (byte)(tamanhoTotal >> 8);
        pacote[3] = (byte)tamanhoTotal;
        pacote[4] = 0x00; pacote[5] = 0x00; // identificacao
        pacote[6] = 0x40; pacote[7] = 0x00; // flags: don't fragment
        pacote[8] = 64; // TTL
        pacote[9] = 17; // protocolo UDP

        // origem/destino invertidos: quem perguntou vira o destino da resposta
        Array.Copy(ipOriginal.EnderecoDestino, 0, pacote, 12, 4);
        Array.Copy(ipOriginal.EnderecoOrigem, 0, pacote, 16, 4);

        var checksumIp = CalcularChecksum(pacote, 0, tamanhoIp);
        pacote[10] = (byte)(checksumIp >> 8);
        pacote[11] = (byte)checksumIp;

        // --- cabecalho UDP ---
        pacote[20] = (byte)(udpOriginal.PortaDestino >> 8);
        pacote[21] = (byte)udpOriginal.PortaDestino; // porta de origem da resposta = porta DNS original de destino (53)
        pacote[22] = (byte)(udpOriginal.PortaOrigem >> 8);
        pacote[23] = (byte)udpOriginal.PortaOrigem; // porta de destino da resposta = porta de origem de quem perguntou
        var tamanhoUdpTotal = tamanhoUdp + corpoDns.Length;
        pacote[24] = (byte)(tamanhoUdpTotal >> 8);
        pacote[25] = (byte)tamanhoUdpTotal;
        pacote[26] = 0x00; pacote[27] = 0x00; // checksum UDP = 0 ("nao verificado", valido para IPv4 - RFC 768).
                                               // Simplificacao deliberada: calcular o checksum UDP exigiria montar
                                               // o pseudo-cabecalho IP+UDP; a maioria das pilhas aceita 0 sem problema,
                                               // mas fica como possivel refinamento se algum dispositivo reclamar.

        Array.Copy(corpoDns, 0, pacote, 28, corpoDns.Length);

        return pacote;
    }

    private static int CalcularChecksum(byte[] dados, int offset, int tamanho)
    {
        long soma = 0;
        for (var i = offset; i < offset + tamanho; i += 2)
        {
            var palavra = (dados[i] << 8) | (i + 1 < offset + tamanho ? dados[i + 1] : 0);
            soma += palavra;
        }

        while ((soma >> 16) != 0)
            soma = (soma & 0xFFFF) + (soma >> 16);

        return (int)(~soma & 0xFFFF);
    }
}
