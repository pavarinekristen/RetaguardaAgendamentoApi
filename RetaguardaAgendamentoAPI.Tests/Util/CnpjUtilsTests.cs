using RetaguardaAgendamentoAPI.Util;
using Xunit;

namespace RetaguardaAgendamentoAPI.Tests.Util;

public class CnpjUtilsTests
{
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // NORMALIZAR
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("11222333000181",     "11222333000181")]   // jÃ¡ sem mÃ¡scara
    [InlineData("ab.cd1.234/efgh-46", "ABCD1234EFGH46")]  // minÃºsculas + mÃ¡scara
    [InlineData("ABCD1234EFGH46",     "ABCD1234EFGH46")]  // maiÃºsculas sem mÃ¡scara
    [InlineData("PM.0O3.6A7/0001-71", "PM0O36A7000171")]  // CNPJ do ticket PDV-40
    [InlineData("  11222333000181  ", "11222333000181")]   // espaÃ§os externos
    public void Normalizar_RemoveFormatacaoEUppercase(string entrada, string esperado)
    {
        Assert.Equal(esperado, CnpjUtils.Normalizar(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_EntradaVazia_RetornaStringVazia(string? entrada)
    {
        Assert.Equal(string.Empty, CnpjUtils.Normalizar(entrada!));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // ISVALIDO â€” vÃ¡lidos
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("11222333000181")]               // numÃ©rico sem mÃ¡scara
    [InlineData("11.222.333/0001-81")]           // numÃ©rico com mÃ¡scara
    [InlineData("99888777000100")]               // outro numÃ©rico vÃ¡lido
    [InlineData("ABCD1234EFGH46")]              // alfanumÃ©rico sem mÃ¡scara
    [InlineData("abcd1234efgh46")]              // alfanumÃ©rico minÃºsculas
    [InlineData("AB.CD1.234/EFGH-46")]          // alfanumÃ©rico com mÃ¡scara
    [InlineData("PM.0O3.6A7/0001-71")]          // CNPJ do ticket PDV-40
    [InlineData("PM0O36A7000171")]              // mesmo sem mÃ¡scara
    public void IsValido_CnpjValido_RetornaTrue(string cnpj)
    {
        Assert.True(CnpjUtils.IsValido(cnpj));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // ISVALIDO â€” invÃ¡lidos
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("123")]                  // muito curto
    [InlineData("1234567890123")]        // 13 dÃ­gitos (sem DV)
    [InlineData("")]                     // vazio
    [InlineData("   ")]                  // sÃ³ espaÃ§os
    [InlineData(null)]                   // nulo
    [InlineData("ABCD1234EFGH00")]      // DV incorreto
    [InlineData("ABCD1234EFGHAA")]      // DV nÃ£o-numÃ©rico
    [InlineData("00000000000000")]      // tudo zeros
    [InlineData("11111111111111")]      // todos iguais (DV incorreto)
    [InlineData("11222333000182")]      // DV trocado (correto seria 81)
    [InlineData("PM.0O3.6A7/0001-72")] // DV errado no ticket
    public void IsValido_CnpjInvalido_RetornaFalse(string? cnpj)
    {
        Assert.False(CnpjUtils.IsValido(cnpj!));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CALCULAR DV
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("112223330001", "81")]   // numÃ©rico â€” DV da empresa de teste
    [InlineData("998887770001", "00")]   // numÃ©rico â€” DV = 00
    [InlineData("ABCD1234EFGH", "46")]  // alfanumÃ©rico â€” caso dos testes Java de referÃªncia
    [InlineData("PM0O36A70001", "71")]  // alfanumÃ©rico â€” CNPJ do ticket PDV-40
    [InlineData("abcd1234efgh", "46")]  // minÃºsculas devem ser normalizadas
    public void CalcularDV_BaseValida_RetornaDVCorreto(string base12, string dvEsperado)
    {
        Assert.Equal(dvEsperado, CnpjUtils.CalcularDV(base12));
    }

    [Theory]
    [InlineData("123")]          // muito curto
    [InlineData("1234567890123456")]  // muito longo
    [InlineData("ABCD1234EFG!")]     // caractere invÃ¡lido
    public void CalcularDV_BaseInvalida_LancaArgumentException(string base12)
    {
        Assert.Throws<ArgumentException>(() => CnpjUtils.CalcularDV(base12));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // FORMATAR
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("11222333000181",  "11.222.333/0001-81")]
    [InlineData("ABCD1234EFGH46", "AB.CD1.234/EFGH-46")]
    [InlineData("PM0O36A7000171", "PM.0O3.6A7/0001-71")]
    [InlineData("99888777000100", "99.888.777/0001-00")]
    public void Formatar_Cnpj14Chars_AplicaMascara(string entrada, string esperado)
    {
        Assert.Equal(esperado, CnpjUtils.Formatar(entrada));
    }

    [Fact]
    public void Formatar_CnpjJaMascarado_RetornaFormatadoCorreto()
    {
        // Normaliza primeiro e aplica mÃ¡scara
        Assert.Equal("11.222.333/0001-81", CnpjUtils.Formatar("11.222.333/0001-81"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    public void Formatar_TamanhoInvalido_RetornaSemMascara(string entrada)
    {
        // Se nÃ£o tem 14 chars depois de normalizar, devolve como veio
        Assert.Equal(CnpjUtils.Normalizar(entrada), CnpjUtils.Formatar(entrada));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CONSISTÃŠNCIA: IsValido + Formatar + Normalizar devem ser coerentes
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("11222333000181")]
    [InlineData("ABCD1234EFGH46")]
    [InlineData("PM0O36A7000171")]
    public void Formatado_QuandoRevalidado_AindaEhValido(string cnpj)
    {
        var formatado = CnpjUtils.Formatar(cnpj);
        Assert.True(CnpjUtils.IsValido(formatado),
            $"CNPJ {cnpj} formatado como '{formatado}' deve continuar vÃ¡lido");
    }

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("AB.CD1.234/EFGH-46")]
    [InlineData("PM.0O3.6A7/0001-71")]
    public void Normalizado_QuandoRevalidado_AindaEhValido(string cnpjFormatado)
    {
        var normalizado = CnpjUtils.Normalizar(cnpjFormatado);
        Assert.True(CnpjUtils.IsValido(normalizado),
            $"CNPJ '{cnpjFormatado}' normalizado como '{normalizado}' deve ser vÃ¡lido");
    }
}

