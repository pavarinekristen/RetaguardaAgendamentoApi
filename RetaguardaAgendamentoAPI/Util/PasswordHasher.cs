using System;
using System.Security.Cryptography;
using System.Text;

namespace RetaguardaAgendamentoAPI.Util
{
    /// <summary>
    /// Hash de senha com PBKDF2-SHA256 em formato versionado e autodescritivo:
    ///   PBKDF2$&lt;iteracoes&gt;$&lt;salt-base64&gt;$&lt;hash-base64&gt;
    /// Hashes legados (hex de 64 caracteres gerados por SHA256("salt:senha")) continuam
    /// verificaveis e devem ser regravados no proximo login bem-sucedido (re-hash transparente).
    /// </summary>
    public static class PasswordHasher
    {
        public const string Prefixo = "PBKDF2";
        public const int IteracoesPadrao = 210_000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        public static string Hash(string senha)
        {
            if (senha == null)
                throw new ArgumentNullException(nameof(senha));

            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, IteracoesPadrao, HashAlgorithmName.SHA256, HashBytes);
            return $"{Prefixo}${IteracoesPadrao}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verificar(string senha, string senhaHashArmazenado, string saltLegado)
        {
            if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(senhaHashArmazenado))
                return false;

            return EhFormatoPbkdf2(senhaHashArmazenado)
                ? VerificarPbkdf2(senha, senhaHashArmazenado)
                : VerificarLegadoSha256(senha, senhaHashArmazenado, saltLegado);
        }

        /// <summary>Indica se o hash armazenado ainda esta no formato legado e precisa ser regravado.</summary>
        public static bool PrecisaRehash(string senhaHashArmazenado)
        {
            if (string.IsNullOrEmpty(senhaHashArmazenado))
                return true;

            if (!EhFormatoPbkdf2(senhaHashArmazenado))
                return true;

            var partes = senhaHashArmazenado.Split('$');
            return partes.Length != 4
                || !int.TryParse(partes[1], out var iteracoes)
                || iteracoes < IteracoesPadrao;
        }

        public static bool EhFormatoPbkdf2(string senhaHashArmazenado)
        {
            return senhaHashArmazenado != null
                && senhaHashArmazenado.StartsWith(Prefixo + "$", StringComparison.Ordinal);
        }

        private static bool VerificarPbkdf2(string senha, string armazenado)
        {
            var partes = armazenado.Split('$');
            if (partes.Length != 4)
                return false;

            if (!int.TryParse(partes[1], out var iteracoes) || iteracoes <= 0)
                return false;

            byte[] salt;
            byte[] esperado;
            try
            {
                salt = Convert.FromBase64String(partes[2]);
                esperado = Convert.FromBase64String(partes[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }

        private static bool VerificarLegadoSha256(string senha, string armazenado, string saltLegado)
        {
            var calculado = HashLegadoSha256(senha, saltLegado ?? string.Empty);
            var calculadoBytes = Encoding.UTF8.GetBytes(calculado);
            var armazenadoBytes = Encoding.UTF8.GetBytes(armazenado.ToLowerInvariant());
            return calculadoBytes.Length == armazenadoBytes.Length
                && CryptographicOperations.FixedTimeEquals(calculadoBytes, armazenadoBytes);
        }

        public static string HashLegadoSha256(string senha, string salt)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{senha}"));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
