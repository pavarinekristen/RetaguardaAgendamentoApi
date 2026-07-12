# SparkCore - Documentacao do Sistema

Atualizado em: 07/07/2026.

Esta pasta consolida o estado atual do SparkCore, a arquitetura aprovada, a estrategia de infraestrutura e os procedimentos de deploy/operacao.

## Como esta documentacao foi organizada

A estrutura segue uma separacao parecida com o modelo Diataxis:

- **Visao e explicacao**: contexto, decisoes e arquitetura.
- **Guias de como fazer**: deploy, publicacao, testes e operacao.
- **Referencia tecnica**: endpoints, bancos, paths, configuracoes.
- **Roadmap**: pendencias e proximos passos.

Referencias usadas como base de organizacao:

- Diataxis: https://diataxis.fr/
- Twelve-Factor App: https://12factor.net/config
- Build, release, run: https://12factor.net/build-release-run
- Azure Architecture Fundamentals: https://learn.microsoft.com/en-us/azure/architecture/guide/

## Indice

1. [[01-situacao-atual|Situacao atual]]
2. [[02-arquitetura-e-infra|Arquitetura e infraestrutura]]
3. [[03-api-backend|API e backend]]
4. [[04-front-web-laudos|Front web leve]]
5. [[05-desktop-wpf-instalador|Desktop WPF e instalador]]
6. [[06-banco-dados-sincronizacao|Banco de dados e sincronizacao]]
7. [[07-deploy-vps-api|Deploy da API em VPS]]
8. [[08-hostinger-subdominios|Hostinger, dominios e subdominios]]
9. [[09-operacao-checklists|Operacao e checklists]]
10. [[10-pendencias-roadmap|Pendencias e roadmap]]

## Decisao principal aprovada

O sistema seguira com tres partes:

```text
Desktop local/WPF        -> operacao principal da clinica
laudos.clinicaideia.com.br -> front web leve para celular
api.clinicaideia.com.br    -> API .NET em VPS rodando 24h
```

Diretriz:

- Manter o desktop/local como fluxo principal.
- Publicar na web apenas login, pesquisa de clientes e historico basico.
- Usar Hostinger para site, DNS e frontend estatico.
- Usar VPS para API .NET e banco do modulo web.
- Nao tentar hospedar a API .NET na hospedagem compartilhada atual da Hostinger.

