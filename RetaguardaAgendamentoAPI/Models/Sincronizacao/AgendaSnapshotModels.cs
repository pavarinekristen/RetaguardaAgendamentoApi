using System;
using System.Collections.Generic;

namespace RetaguardaAgendamentoAPI.Models.Sincronizacao
{
    public class AgendaSnapshotRequest
    {
        public string DispositivoId { get; set; }

        // true somente quando o cliente enviou TODAS as linhas de todas as tabelas.
        // Snapshots incrementais (padrao) nao permitem inferir exclusao por ausencia.
        public bool SnapshotCompleto { get; set; }

        public List<AgendaSnapshotTable> Tabelas { get; set; } = new List<AgendaSnapshotTable>();
    }

    public class AgendaSnapshotTable
    {
        public string Nome { get; set; }
        public List<AgendaSnapshotRecord> Registros { get; set; } = new List<AgendaSnapshotRecord>();
    }

    public class AgendaSnapshotRecord
    {
        public string IdLocal { get; set; }
        public string DadosJson { get; set; }
        public string Hash { get; set; }
    }

    public class AgendaSnapshotResponse
    {
        public string Cnpj { get; set; }
        public string BancoOperacional { get; set; }
        public string DispositivoId { get; set; }
        public int TotalTabelas { get; set; }
        public int TotalRegistros { get; set; }
        public DateTime SincronizadoEm { get; set; }
    }
}

