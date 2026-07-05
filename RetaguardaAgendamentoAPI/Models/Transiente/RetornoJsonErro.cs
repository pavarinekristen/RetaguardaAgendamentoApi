using System;

namespace RetaguardaAgendamentoAPI.Models
{
    public class RetornoJsonErro
    {
        // Detalhes da exceção (mensagem interna e stack trace) nunca vão na resposta HTTP;
        // devem ser registrados no log do servidor pelo controller que capturou o erro.
        public RetornoJsonErro(int codigo, String mensagem, Exception ex)
        {
            Status = codigo;
            Message = mensagem;

            switch (Status)
            {
                case 400:
                    Error = "Bad Request";
                    break;
                case 404:
                    Error = "Not Found";
                    break;
                case 401:
                    Error = "Unauthorized";
                    break;
                case 500:
                    Error = "Internal Server Error";
                    break;
                default:
                    Error = "Erro não mapeado";
                    break;
            }
        }


        public int Status { get; set; }
        public string Error { get; set; }
        public string Message { get; set; }

    }
}
