# Hostinger, Dominios e Subdominios

Atualizado em: 07/07/2026.

## Estado encontrado

Plano:

```text
Premium Web Hosting
```

Dominio:

```text
clinicaideia.com.br
```

Subdominio/sistema:

```text
laudos.clinicaideia.com.br
```

Diretorio do subdominio:

```text
/home/u183827986/domains/clinicaideia.com.br/public_html/laudos
```

Recursos vistos:

- phpMyAdmin.
- MySQL remoto.
- Servico de email.
- `u183827986.hostingerapp.com`.

## O que a Hostinger atual deve hospedar

Pode hospedar:

- Site institucional.
- Arquivos HTML/CSS/JS.
- Front web leve.
- Eventuais paginas PHP.
- DNS.
- Email.

Nao deve hospedar no plano atual:

- API ASP.NET Core .NET 8 rodando 24h.
- Processo persistente da API.

## Publicacao do front web leve

Origem local:

```text
C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos
```

Destino Hostinger:

```text
/home/u183827986/domains/clinicaideia.com.br/public_html/laudos
```

Arquivos:

```text
index.html
styles.css
app.js
config.js
```

Antes de subir:

- Configurar `config.js`:

```js
window.LAUDOS_CONFIG = {
  API_BASE_URL: "https://api.clinicaideia.com.br"
};
```

## Site institucional

Dominio:

```text
clinicaideia.com.br
```

Pode continuar no plano atual da Hostinger.

## API

Dominio recomendado:

```text
api.clinicaideia.com.br
```

Deve apontar para:

```text
IP da VPS
```

Tipo de DNS:

```text
A record
```

## Sobre cancelar Linode

Pode cancelar somente se:

- O sistema antigo nao for mais necessario, ou
- O backend antigo for migrado, ou
- A nova API ja estiver publicada em outra VPS e validada.

Nao pode cancelar se:

- O sistema antigo ainda estiver em uso.
- O banco real estiver somente na Linode e nao houver backup/migracao.
- A nova VPS ainda nao estiver pronta.

## Como confirmar onde esta o banco antigo

No phpMyAdmin da Hostinger:

- Procurar tabelas de negocio reais.
- Conferir se ha dados atuais.

Na Linode:

- Verificar arquivos de configuracao da API antiga.
- Verificar MySQL local.
- Verificar containers/processos.
- Verificar string de conexao.

Conclusao atual:

- Hostinger tem MySQL disponivel.
- Isso nao prova que o sistema antigo usa esse MySQL.
- A evidencia mais forte aponta para backend na Linode.

