using System;
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
            var sha256 = section.GetValue<string>("Sha256") ?? string.Empty;
            var required = section.GetValue("Required", false);
            var releaseNotes = section.GetValue<string>("ReleaseNotes") ?? string.Empty;

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
    }
}
