using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using RetaguardaAgendamentoAPI.Models;
using RetaguardaAgendamentoAPI.Models.Auth;
using RetaguardaAgendamentoAPI.Services.Auth;

namespace RetaguardaAgendamentoAPI.Controllers.Auth
{
    [Route("auth")]
    [Produces("application/json")]
    [EnableRateLimiting("AuthPolicy")]
    public class AuthController : Controller
    {
        private readonly AuthService _service;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService service, ILogger<AuthController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("criar-conta")]
        public async Task<IActionResult> CriarConta([FromBody] CriarContaRequest request)
        {
            try
            {
                return Ok(await _service.CriarContaAsync(request));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Criar Conta].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Criar Conta]", ex));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                return Ok(await _service.LoginAsync(request));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new RetornoJsonErro(401, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Login].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Login]", ex));
            }
        }

        [HttpPost("confirmar-email")]
        public async Task<IActionResult> ConfirmarEmail([FromBody] ConfirmarEmailRequest request)
        {
            try
            {
                return Ok(await _service.ConfirmarEmailAsync(request));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new RetornoJsonErro(401, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Confirmar Email].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Confirmar Email]", ex));
            }
        }

        [HttpPost("reenviar-confirmacao")]
        public async Task<IActionResult> ReenviarConfirmacao([FromBody] ReenviarConfirmacaoRequest request)
        {
            try
            {
                return Ok(await _service.ReenviarConfirmacaoAsync(request));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Reenviar Confirmacao].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Reenviar Confirmacao]", ex));
            }
        }

        [HttpPost("recuperar-senha")]
        public async Task<IActionResult> RecuperarSenha([FromBody] RecuperarSenhaRequest request)
        {
            try
            {
                return Ok(await _service.RecuperarSenhaAsync(request));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Recuperar Senha].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Recuperar Senha]", ex));
            }
        }

        [HttpPost("redefinir-senha")]
        public async Task<IActionResult> RedefinirSenha([FromBody] RedefinirSenhaRequest request)
        {
            try
            {
                return Ok(await _service.RedefinirSenhaAsync(request));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new RetornoJsonErro(401, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Redefinir Senha].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Redefinir Senha]", ex));
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            try
            {
                var authorization = Request.Headers["Authorization"].ToString();
                var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authorization.Substring("Bearer ".Length).Trim()
                    : authorization;

                return Ok(await _service.ValidarTokenAsync(token));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new RetornoJsonErro(401, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor [Validar Token].");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Validar Token]", ex));
            }
        }
    }
}
