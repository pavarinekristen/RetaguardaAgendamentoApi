# Situacao Atual

Atualizado em: 07/07/2026.

## Produto

Nome do sistema: **SparkCore**

Objetivo:

- Sistema desktop WPF para a clinica operar localmente.
- Cadastro de cliente/funcionario.
- Cadastro de profissionais/salas.
- Agenda de consultas/laudos.
- Historico e relatorios.
- Geracao/download de laudos oficiais com arma e sem arma.
- Sincronizacao com API quando disponivel.
- Front web leve para consulta externa pelo celular.

## Repositorios e paths

API:

```text
C:\Users\krist\RetaguardaAgendamentoAPI
C:\Users\krist\RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.sln
```

WPF:

```text
C:\Users\krist\AgendamentoWpfApp
C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.sln
```

Front web leve:

```text
C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos
```

Arquivo de status principal:

```text
C:\Users\krist\RetaguardaAgendamentoAPI\STATUS_E_COMANDOS.txt
```

## Estado do desktop WPF

Implementado:

- Login.
- Cadastro de cliente/funcionario.
- Pesquisa separada do cadastro.
- Agenda.
- Profissionais/salas.
- Historico de laudos.
- Relatorio de agendamentos.
- Impressao de relatorio.
- Download de laudo oficial.
- Backup local SQLite.
- Sincronizacao automatica por snapshot.

Alteracoes da reuniao com cliente ja aplicadas no WPF:

- Cliente representa a pessoa/funcionario atendido.
- Cadastro possui empresa, escolaridade, cargo, estado civil, naturalidade, email, CPF, RG, sexo, tipo de endereco e endereco.
- Agendamento possui motivo, empresa, trabalha armado, status e observacao.
- Motivos iniciais: Admissao, Periodico, Retorno ao trabalho, Mudanca de funcao, Demissional.
- Dois tipos de laudo:
  - pagina 1 do PDF oficial: sem arma.
  - pagina 2 do PDF oficial: com arma.
- Aba Laudos lista historico, filtra e baixa PDF.

## Regra do ID do cliente

Regra aprovada:

- O ID operacional do cliente deve ser sequencial.
- Exemplo: cliente 1 ate cliente 30000.
- Esse ID deve ser usado como identificador visivel para pesquisa, atendimento e referencia.

Estado tecnico:

- `Cliente.Id` no WPF e o ID numerico sequencial do SQLite/EF.
- `Cliente.IdTexto` mostra esse ID no sistema.
- `Cliente.IdLocal` e GUID tecnico usado para sincronizacao/relacoes internas.

Decisao:

- Mostrar para o usuario apenas o ID sequencial.
- Usar GUID somente internamente.
- No portal web, a API devolve `IdCadastro` para exibicao e `IdLocal` para navegacao tecnica.

## Estado da API

Implementado:

- ASP.NET Core .NET 8.
- Login, criacao de conta, confirmacao de email e recuperacao/redefinicao de senha.
- Token proprio armazenado no banco.
- Hash de senha PBKDF2.
- Snapshot de sincronizacao do WPF para MySQL.
- Health check.
- Endpoint de atualizacao SparkCore.
- Endpoints do portal web:
  - `GET /portal/clientes`
  - `GET /portal/clientes/{idLocal}`

Pendente antes de deploy serio:

- Preparar reverse proxy/HTTPS.
- Ajustar secrets por variaveis de ambiente.
- Whitelist/limites do snapshot.
- Logout/limpeza de sessoes.
- Logs de producao.
- Deploy em VPS.

## Estado do front web leve

Implementado:

- HTML/CSS/JS estatico, sem build.
- Login.
- Pesquisa de clientes por:
  - Nome.
  - ID.
  - Empresa.
- Lista de resultados.
- Detalhe do cliente.
- Historico basico.
- Logout local.

Path:

```text
C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos
```

Publicacao futura:

```text
/home/u183827986/domains/clinicaideia.com.br/public_html/laudos
```

## Estado da infraestrutura atual

Hostinger:

- Plano: Premium Web Hosting.
- Dominio ativo: `clinicaideia.com.br`.
- Subdominio/dominio externo: `laudos.clinicaideia.com.br`.
- Diretorio do subdominio:

```text
/home/u183827986/domains/clinicaideia.com.br/public_html/laudos
```

Linode:

- Ha indicio forte de backend atual em Linode:

```text
http://li927-43.members.linode.com:9090
```

- Ha recibo Linode/Akamai de 01/07/2026 no valor de USD 7,00.

Conclusao:

- O sistema antigo provavelmente usa Hostinger para frontend e Linode para API/backend.
- O banco provavelmente esta na Linode ou acessivel pela API da Linode, mas ainda nao foi 100% confirmado.

