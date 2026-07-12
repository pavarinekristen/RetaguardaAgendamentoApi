using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace RetaguardaAgendamentoAPI.Controllers.Updates
{
    [ApiController]
    [Route("updates/sparkcore")]
    public class SparkCoreUpdateController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SparkCoreUpdateController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("latest")]
        public IActionResult Latest([FromQuery] string currentVersion = "")
        {
            var section = _configuration.GetSection("Updates:SparkCore");
            var enabled = section.GetValue("Enabled", false);
            var version = section.GetValue("Version", "1.0.0") ?? "1.0.0";
            var packageUrl = section.GetValue<string>("PackageUrl") ?? string.Empty;
            var packageFilePath = section.GetValue<string>("PackageFilePath") ?? string.Empty;
            var sha256 = section.GetValue<string>("Sha256") ?? string.Empty;
            var required = section.GetValue("Required", false);
            var releaseNotes = section.GetValue<string>("ReleaseNotes") ?? string.Empty;

            // Sem PackageUrl explicita, a propria API serve o pacote via /updates/sparkcore/package.
            if (string.IsNullOrWhiteSpace(packageUrl) &&
                !string.IsNullOrWhiteSpace(packageFilePath) &&
                System.IO.File.Exists(packageFilePath))
            {
                packageUrl = "/updates/sparkcore/package";
            }

            if (!string.IsNullOrWhiteSpace(packageUrl) &&
                Uri.TryCreate(packageUrl, UriKind.Relative, out _))
            {
                packageUrl = $"{Request.Scheme}://{Request.Host}{packageUrl}";
            }

            return Ok(new
            {
                enabled,
                version,
                packageUrl,
                sha256,
                required,
                releaseNotes,
                currentVersion
            });
        }

        [HttpGet("package")]
        public IActionResult Package()
        {
            var section = _configuration.GetSection("Updates:SparkCore");
            var enabled = section.GetValue("Enabled", false);
            var version = section.GetValue("Version", "1.0.0") ?? "1.0.0";
            var packageFilePath = section.GetValue<string>("PackageFilePath") ?? string.Empty;

            if (!enabled || string.IsNullOrWhiteSpace(packageFilePath))
                return NotFound(new { erro = "Nenhum pacote de atualizacao disponivel." });

            var fullPath = Path.GetFullPath(packageFilePath);
            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { erro = "Arquivo do pacote de atualizacao nao encontrado no servidor." });

            return PhysicalFile(fullPath, "application/zip", $"sparkcore-{version}.zip", enableRangeProcessing: true);
        }
    }
}
