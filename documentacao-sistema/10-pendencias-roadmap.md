# Pendencias e Roadmap

Atualizado em: 07/07/2026.

## Prioridade imediata

1. Validar front web com banco sincronizado real.
2. Garantir que ID sequencial aparece corretamente em todos os resultados.
3. Confirmar lista oficial de motivos com a cliente.
4. Confirmar PDF oficial definitivo de laudos.
5. Definir VPS.
6. Publicar API em ambiente de teste.
7. Publicar front web no subdominio.

## Pendencias API antes de producao

Obrigatorias:

- Secrets fora do `appsettings.json`.
- HTTPS.
- Nginx/reverse proxy.
- `UseForwardedHeaders`.
- CORS de producao.
- Backup MySQL.
- Logs.
- Whitelist do snapshot.
- Limite de registros/payload.

Recomendadas:

- Logout.
- Limpeza de sessoes expiradas.
- Middleware global de erro.
- Swagger em dev.
- Testes HTTP com WebApplicationFactory.
- Rate limit em `/health`.

## Pendencias WPF

- Confirmar UX final das abas Cadastro/Pesquisa.
- Confirmar campos obrigatorios com cliente.
- Confirmar lista final de motivos.
- Validar fluxo completo com cliente real.
- Confirmar instalador em maquina limpa.
- Definir configuracao da URL da API em producao.
- Melhorar log local para suporte.

## Pendencias front web

- Testar com dados reais sincronizados.
- Ajustar mensagens de erro se necessario.
- Confirmar responsividade em celular real.
- Confirmar cache/atualizacao apos publicar na Hostinger.
- Confirmar se sera necessario recuperar senha pelo front.

## Pendencias infraestrutura

- Escolher VPS.
- Definir se banco fica na VPS.
- Definir backup e retencao.
- Definir acesso SSH.
- Definir usuario Linux da API.
- Definir DNS:
  - `clinicaideia.com.br`
  - `laudos.clinicaideia.com.br`
  - `api.clinicaideia.com.br`
- Definir estrategia de migracao/cancelamento Linode.

## Pendencias sobre sistema antigo

- Confirmar se o sistema antigo ainda sera usado.
- Confirmar onde esta o banco antigo.
- Confirmar se dados antigos precisam ser migrados.
- Confirmar se a Linode pode ser cancelada.
- Fazer backup antes de qualquer cancelamento.

## Ordem recomendada

1. Fechar desenvolvimento local.
2. Rodar testes e smoke.
3. Subir API em VPS de teste/producao.
4. Configurar DNS e SSL.
5. Publicar front web na Hostinger.
6. Validar login/pesquisa no celular.
7. Sincronizar desktop com API publicada.
8. Fazer periodo de uso assistido.
9. Avaliar cancelamento Linode somente depois de backup e validacao.

