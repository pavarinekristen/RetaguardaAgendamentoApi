using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using RetaguardaAgendamentoAPI.Models;
using RetaguardaAgendamentoAPI.Models.Sincronizacao;
using RetaguardaAgendamentoAPI.Services.Auth;
using RetaguardaAgendamentoAPI.Services.Sincronizacao;

namespace RetaguardaAgendamentoAPI.Controllers.Sincronizacao
{
    [Route("sincroniza/agenda")]
    [Produces("application/json")]
    public class AgendaSnapshotController : Controller
    {
        private readonly AuthService _authService;
        private readonly AgendaSnapshotService _snapshotService;
        private readonly ILogger<AgendaSnapshotController> _logger;

        public AgendaSnapshotController(
            AuthService authService,
            AgendaSnapshotService snapshotService,
            ILogger<AgendaSnapshotController> logger)
        {
            _authService = authService;
            _snapshotService = snapshotService;
            _logger = logger;
        }

        [HttpPost("snapshot")]
        public async Task<IActionResult> Snapshot([FromBody] AgendaSnapshotRequest request)
        {
            try
            {
                var sessao = await _authService.ValidarTokenAsync(ExtrairToken());
                _logger.LogInformation(
                    "Snapshot recebido. EmpresaId={EmpresaId}; Cnpj={Cnpj}; DispositivoId={DispositivoId}; Tabelas={TotalTabelas}",
                    sessao.Empresa.Id,
                    sessao.Empresa.Cnpj,
                    request?.DispositivoId,
                    request?.Tabelas?.Count ?? 0);

                var resposta = await _snapshotService.SalvarSnapshotAsync(sessao.Empresa.Id, sessao.Empresa.Cnpj, request);
                _logger.LogInformation(
                    "Snapshot salvo. EmpresaId={EmpresaId}; Banco={Banco}; Registros={TotalRegistros}",
                    sessao.Empresa.Id,
                    resposta.BancoOperacional,
                    resposta.TotalRegistros);

                return Ok(resposta);
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
                _logger.LogError(ex, "Erro ao processar snapshot da agenda.");
                return StatusCode(500, new RetornoJsonErro(500, "Erro no servidor [Snapshot Agenda]", ex));
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
