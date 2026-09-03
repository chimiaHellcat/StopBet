using SQLite;

namespace StopBet.Core.Modelos;

[Table("DominiosBloqueados")]
public class DominioBloqueado
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed(Unique = true)]
    public string NomeDominio { get; set; } = string.Empty;

    [NotNull]
    public string Categoria { get; set; } = string.Empty;

    [NotNull]
    public OrigemDominio Origem { get; set; }

    [NotNull]
    public DateTime DataInclusao { get; set; }
}
