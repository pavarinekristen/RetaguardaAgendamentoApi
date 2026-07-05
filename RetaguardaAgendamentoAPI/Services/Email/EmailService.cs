using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace RetaguardaAgendamentoAPI.Services.Email
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public bool Enabled => _configuration.GetValue<bool>("Email:Enabled");

        public bool ReturnConfirmationCodeInResponse =>
            _configuration.GetValue<bool>("Email:ReturnConfirmationCodeInResponse");

        public async Task EnviarCodigoConfirmacaoAsync(string destino, string nome, string codigo)
        {
            if (!Enabled)
                throw new InvalidOperationException("Envio de e-mail nao esta habilitado.");

            var host = ValorObrigatorio("Email:Host");
            var username = ValorObrigatorio("Email:Username");
            var password = ValorObrigatorio("Email:Password");
            var from = ValorObrigatorio("Email:From");
            var displayName = _configuration.GetValue<string>("Email:DisplayName") ?? "Retaguarda Agendamento";
            var port = _configuration.GetValue<int?>("Email:Port") ?? 587;
            var enableSsl = _configuration.GetValue<bool?>("Email:EnableSsl") ?? true;

            using var message = new MailMessage
            {
                From = new MailAddress(from, displayName),
                Subject = "Codigo de confirmacao da sua conta",
                Body = MontarCorpo(nome, codigo),
                IsBodyHtml = false
            };
            message.To.Add(destino);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };

            try
            {
                _logger.LogInformation("Enviando e-mail de confirmacao para {Destino} via {Host}:{Port}.", destino, host, port);
                await client.SendMailAsync(message);
                _logger.LogInformation("E-mail de confirmacao enviado para {Destino}.", destino);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha SMTP ao enviar codigo de confirmacao para {Destino}. Host={Host}; Port={Port}", destino, host, port);
                throw;
            }
        }

        private string ValorObrigatorio(string chave)
        {
            var valor = _configuration.GetValue<string>(chave);
            if (string.IsNullOrWhiteSpace(valor))
                throw new InvalidOperationException($"{chave} nao configurado.");

            return valor.Trim();
        }

        private static string MontarCorpo(string nome, string codigo)
        {
            var saudacao = string.IsNullOrWhiteSpace(nome) ? "Ola" : $"Ola, {nome}";
            return $@"{saudacao}.

Seu codigo de confirmacao da Retaguarda Agendamento e:

{codigo}

Este codigo expira em 30 minutos.

Se voce nao solicitou esta conta, ignore este e-mail.";
        }
    }
}
