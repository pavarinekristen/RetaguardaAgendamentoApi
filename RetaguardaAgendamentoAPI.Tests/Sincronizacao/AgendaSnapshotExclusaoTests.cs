using RetaguardaAgendamentoAPI.Services.Sincronizacao;
using Xunit;

namespace RetaguardaAgendamentoAPI.Tests.Sincronizacao;

// Cobre a regressao em que o upsert gravava EXCLUIDO = 'N' fixo e
// "ressuscitava" no portal registros excluidos no desktop: o campo
// "Excluido" viaja no DadosJson e precisa alimentar a coluna de controle.
public class AgendaSnapshotExclusaoTests
{
    // SQLite envia o booleano como numero (0/1); clientes futuros podem
    // enviar bool JSON (ja convertido para "S"/"N") ou texto.
    [Theory]
    [InlineData("1")]
    [InlineData("S")]
    [InlineData("s")]
    [InlineData("true")]
    [InlineData("True")]
    public void RegistroComExcluidoVerdadeiro_MarcaExcluido(object valor)
    {
        var dados = new Dictionary<string, object> { ["Excluido"] = valor };
        Assert.True(AgendaSnapshotService.RegistroMarcadoComoExcluido(dados));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("N")]
    [InlineData("false")]
    [InlineData("")]
    public void RegistroComExcluidoFalso_NaoMarcaExcluido(object valor)
    {
        var dados = new Dictionary<string, object> { ["Excluido"] = valor };
        Assert.False(AgendaSnapshotService.RegistroMarcadoComoExcluido(dados));
    }

    [Fact]
    public void ChaveEmOutraCaixa_TambemEhReconhecida()
    {
        // O dicionario de dados do snapshot e case-insensitive.
        var dados = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["excluido"] = "1"
        };
        Assert.True(AgendaSnapshotService.RegistroMarcadoComoExcluido(dados));
    }

    [Fact]
    public void SemCampoExcluido_NaoMarcaExcluido()
    {
        Assert.False(AgendaSnapshotService.RegistroMarcadoComoExcluido(new Dictionary<string, object>()));
        Assert.False(AgendaSnapshotService.RegistroMarcadoComoExcluido(null));
    }

    [Fact]
    public void ValorNuloOuDBNull_NaoMarcaExcluido()
    {
        Assert.False(AgendaSnapshotService.RegistroMarcadoComoExcluido(
            new Dictionary<string, object> { ["Excluido"] = null }));
        Assert.False(AgendaSnapshotService.RegistroMarcadoComoExcluido(
            new Dictionary<string, object> { ["Excluido"] = DBNull.Value }));
    }
}
