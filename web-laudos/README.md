# Front web leve - laudos.clinicaideia.com.br

Frontend estatico para consulta externa pelo celular.

## Telas

- Login usando `POST /auth/login`.
- Pesquisa de clientes usando `GET /portal/clientes`.
- Detalhe do cliente usando `GET /portal/clientes/{idLocal}`.
- Historico basico retornado junto do detalhe.

## Configuracao

Antes de publicar em producao, alterar `config.js`:

```js
window.LAUDOS_CONFIG = {
  API_BASE_URL: "https://api.clinicaideia.com.br"
};
```

## Publicacao na Hostinger

Enviar os arquivos desta pasta para o diretorio do subdominio:

```text
/home/u183827986/domains/clinicaideia.com.br/public_html/laudos
```

Arquivos necessarios:

- `index.html`
- `styles.css`
- `app.js`
- `config.js`

## Observacao

O frontend depende da API publicada em VPS. A hospedagem compartilhada da Hostinger entrega estes arquivos, mas nao substitui a API .NET rodando 24h.
