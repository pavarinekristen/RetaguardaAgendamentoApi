using System.Collections.Generic;

namespace RetaguardaAgendamentoAPI.Models.Portal
{
    public class PortalClienteResumo
    {
        public string IdLocal { get; set; }
        public string IdCadastro { get; set; }
        public string Nome { get; set; }
        public string Empresa { get; set; }
        public string Cargo { get; set; }
        public string Cpf { get; set; }
        public string Rg { get; set; }
        public string Email { get; set; }
        public string Sexo { get; set; }
        public string Telefone { get; set; }
        public string SincronizadoEm { get; set; }
    }

    public class PortalClienteDetalhe : PortalClienteResumo
    {
        public string Escolaridade { get; set; }
        public string EstadoCivil { get; set; }
        public string Naturalidade { get; set; }
        public string TipoEndereco { get; set; }
        public string Endereco { get; set; }
        public string Observacoes { get; set; }
        public List<PortalHistoricoItem> Historico { get; set; } = new List<PortalHistoricoItem>();
    }

    public class PortalHistoricoItem
    {
        public string IdLocal { get; set; }
        public string Data { get; set; }
        public string Horario { get; set; }
        public string Funcionario { get; set; }
        public string Empresa { get; set; }
        public string Cargo { get; set; }
        public string Motivo { get; set; }
        public string Status { get; set; }
        public string TipoLaudo { get; set; }
        public string Observacao { get; set; }
    }
}
