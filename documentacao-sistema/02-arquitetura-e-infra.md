# Arquitetura e Infraestrutura

Atualizado em: 07/07/2026.

## Arquitetura alvo aprovada

```text
Usuario interno da clinica
        |
        v
Desktop SparkCore WPF
SQLite local offline-first
        |
        | snapshot/sync quando houver internet
        v
API .NET em VPS 24h
MySQL operacional
        ^
        |
Front web leve em laudos.clinicaideia.com.br
Login + pesquisa + historico basico
        ^
        |
Celular/navegador
```

## Responsabilidade de cada parte

### Desktop WPF

Responsavel por:

- Operacao principal da clinica.
- Cadastro completo.
- Agenda.
- Laudos.
- Historico.
- Relatorios.
- Impressao.
- Download de PDF.
- Persistencia local SQLite.

### API .NET

Responsavel por:

- Autenticacao.
- Usuarios/empresas.
- Validacao de token.
- Recebimento dos snapshots do WPF.
- Disponibilizacao de dados para o portal web.
- Integracao futura com outros modulos online.

### Front web leve

Responsavel por:

- Login pelo navegador/celular.
- Pesquisa simples de clientes por nome, ID e empresa.
- Visualizacao de detalhe.
- Historico basico.

Nao e o objetivo inicial do front web:

- Substituir o desktop.
- Editar todo o cadastro.
- Operar agenda completa.
- Gerar laudos completos.

### Hostinger

Responsavel por:

- Dominio.
- DNS.
- Site institucional.
- Frontend estatico do subdominio `laudos`.
- Possivel email.

Nao deve ser usada para:

- API .NET rodando 24h no plano compartilhado atual.
- Processo persistente ASP.NET Core.

### VPS

Responsavel por:

- Rodar API .NET 24h.
- Rodar ou acessar MySQL.
- Nginx/reverse proxy.
- HTTPS/SSL.
- Logs.
- Backup.
- Atualizacoes controladas.

## Decisao sobre Hostinger x VPS

Plano atual da Hostinger:

- Premium Web Hosting.
- Hospedagem compartilhada.
- Boa para site, HTML, PHP, arquivos estaticos e MySQL simples.
- Nao e o destino correto para API .NET Core como processo persistente.

Decisao:

- Hostinger fica com site/front/DNS.
- API fica em VPS.
- Se cancelar Linode, substituir por outra VPS equivalente.

## Principios de arquitetura adotados

Baseados nas referencias:

- Documentacao deve separar explicacao, guias e referencia tecnica.
- Configuracao sensivel deve ficar fora do codigo versionado.
- Build, release e run devem ser tratados como etapas diferentes.
- Aplicacoes web devem considerar seguranca, custo, operacao, performance e confiabilidade.

Aplicacao no SparkCore:

- `web-laudos/config.js` define URL da API por ambiente.
- `appsettings.json` nao deve carregar secrets reais em producao.
- API em producao deve receber connection strings e SMTP por variaveis de ambiente.
- Deploy da API deve gerar release publicado e imutavel.
- Runtime da API deve ser monitorado por service manager, como systemd.

