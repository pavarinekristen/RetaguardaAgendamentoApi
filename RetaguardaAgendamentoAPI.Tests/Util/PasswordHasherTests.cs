using RetaguardaAgendamentoAPI.Util;
using Xunit;

namespace RetaguardaAgendamentoAPI.Tests.Util;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_GeraFormatoVersionadoPbkdf2()
    {
        var hash = PasswordHasher.Hash("Senha@123");

        Assert.StartsWith("PBKDF2$", hash);
        Assert.Equal(4, hash.Split('$').Length);
        Assert.True(hash.Length <= 128, "hash precisa caber em SENHA_HASH VARCHAR(128)");
    }

    [Fact]
    public void Hash_MesmaSenha_GeraHashesDiferentes()
    {
        // Salt aleatorio por hash: duas chamadas nunca podem coincidir.
        Assert.NotEqual(PasswordHasher.Hash("Senha@123"), PasswordHasher.Hash("Senha@123"));
    }

    [Fact]
    public void Verificar_SenhaCorreta_RetornaTrue()
    {
        var hash = PasswordHasher.Hash("Senha@123");
        Assert.True(PasswordHasher.Verificar("Senha@123", hash, saltLegado: null));
    }

    [Theory]
    [InlineData("senha@123")]
    [InlineData("Senha@124")]
    [InlineData("")]
    public void Verificar_SenhaErrada_RetornaFalse(string tentativa)
    {
        var hash = PasswordHasher.Hash("Senha@123");
        Assert.False(PasswordHasher.Verificar(tentativa, hash, saltLegado: null));
    }

    [Fact]
    public void Verificar_HashLegadoSha256_SenhaCorreta_RetornaTrue()
    {
        var legado = PasswordHasher.HashLegadoSha256("Senha@123", "meusalt");
        Assert.True(PasswordHasher.Verificar("Senha@123", legado, "meusalt"));
    }

    [Fact]
    public void Verificar_HashLegadoSha256_SenhaErrada_RetornaFalse()
    {
        var legado = PasswordHasher.HashLegadoSha256("Senha@123", "meusalt");
        Assert.False(PasswordHasher.Verificar("Senha@124", legado, "meusalt"));
    }

    [Fact]
    public void Verificar_HashCorrempido_RetornaFalseSemLancar()
    {
        Assert.False(PasswordHasher.Verificar("Senha@123", "PBKDF2$abc$naoehbase64$x", null));
        Assert.False(PasswordHasher.Verificar("Senha@123", "PBKDF2$210000$so-tres-partes", null));
    }

    [Fact]
    public void PrecisaRehash_HashLegado_RetornaTrue()
    {
        var legado = PasswordHasher.HashLegadoSha256("Senha@123", "meusalt");
        Assert.True(PasswordHasher.PrecisaRehash(legado));
    }

    [Fact]
    public void PrecisaRehash_HashPbkdf2Atual_RetornaFalse()
    {
        Assert.False(PasswordHasher.PrecisaRehash(PasswordHasher.Hash("Senha@123")));
    }

    [Fact]
    public void PrecisaRehash_IteracoesAbaixoDoPadrao_RetornaTrue()
    {
        var hash = PasswordHasher.Hash("Senha@123")
            .Replace($"${PasswordHasher.IteracoesPadrao}$", "$1000$");
        Assert.True(PasswordHasher.PrecisaRehash(hash));
    }
}
