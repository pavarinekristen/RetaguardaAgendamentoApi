using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RetaguardaAgendamentoAPI.Models;
using RetaguardaAgendamentoAPI.Services.Auth;
using RetaguardaAgendamentoAPI.Services.Portal;
using System;
using System.Threading.Tasks;

namespace RetaguardaAgendamentoAPI.Controllers.Portal
{
    [Route("portal/clientes")]
    [Produces("application/json")]
    public class PortalClientesController : Controller
    {
        private readonly AuthService _authService;
        private readonly PortalClienteService _clienteService;
        private readonly ILogger<PortalClientesController> _logger;

        public PortalClientesController(
            AuthService authService,
            PortalClienteService clienteService,
            ILogger<PortalClientesController> logger)
        {
            _authService = authService;
            _clienteService = clienteService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Pesquisar([FromQuery] string nome, [FromQuery] string id, [FromQuery] string empresa, [FromQuery] int limite = 30)
        {
            try
            {
                var sessao = await _authService.ValidarTokenAsync(ExtrairToken());
                var clientes = await _clienteService.PesquisarAsync(sessao.Empresa.Id, nome, id, empresa, limite);
                return Ok(clientes);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new RetornoJsonErro(401, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao pesquisar clientes no portal.");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Portal Clientes]", ex));
            }
        }

        [HttpGet("{idLocal}")]
        public async Task<IActionResult> Detalhe(string idLocal)
        {
            try
            {
                var sessao = await _authService.ValidarTokenAsync(ExtrairToken());
                var cliente = await _clienteService.ObterDetalheAsync(sessao.Empresa.Id, idLocal);

                if (cliente == null)
                    return NotFound(new RetornoJsonErro(404, "Cliente nao localizado.", null));

                return Ok(cliente);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new RetornoJsonErro(401, ex.Message, null));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, new RetornoJsonErro(400, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar cliente no portal.");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Portal Cliente Detalhe]", ex));
            }
        }

        private string ExtrairToken()
        {
            var authorization = Request.Headers["Authorization"].ToString();
            return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorization.Substring("Bearer ".Length).Trim()
                : authorization;
        }
    }
}
