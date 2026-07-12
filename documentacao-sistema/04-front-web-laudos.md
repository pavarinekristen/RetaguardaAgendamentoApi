# Front Web Leve

Atualizado em: 07/07/2026.

## Objetivo

Criar um acesso simples pelo celular/navegador para:

- Login.
- Pesquisa de clientes.
- Detalhe do cliente.
- Historico basico.

O front web nao substitui o sistema desktop. Ele existe para consulta externa rapida.

## Path local

```text
C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos
```

Arquivos:

```text
index.html
styles.css
app.js
config.js
README.md
```

## Stack

- HTML.
- CSS.
- JavaScript puro.
- Sem React/Vite/build.
- Publicavel direto na Hostinger.

Motivo:

- O subdominio Hostinger consegue servir arquivos estaticos.
- Nao precisa Node no servidor.
- Menos pontos de falha.

## Telas

### Login

Usa:

```text
POST /auth/login
```

Guarda token em:

```text
localStorage
```

### Pesquisa

Campos:

- Nome.
- ID.
- Empresa.

Endpoint:

```text
GET /portal/clientes?nome=&id=&empresa=&limite=50
```

### Detalhe

Endpoint:

```text
GET /portal/clientes/{idLocal}
```

Mostra:

- ID sequencial.
- Nome.
- Empresa.
- Cargo.
- CPF.
- RG.
- Email.
- Telefone.
- Sexo.
- Escolaridade.
- Estado civil.
- Naturalidade.
- Endereco.
- Observacoes.
- Historico basico.

## Configuracao

Arquivo:

```text
web-laudos\config.js
```

Local:

```js
window.LAUDOS_CONFIG = {
  API_BASE_URL: "http://localhost:5000"
};
```

Producao:

```js
window.LAUDOS_CONFIG = {
  API_BASE_URL: "https://api.clinicaideia.com.br"
};
```

## Como rodar local

Em CMD:

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos
python -m http.server 8080 --bind 127.0.0.1
```

Abrir:

```text
http://127.0.0.1:8080/
```

## Como publicar na Hostinger

Diretorio informado no hPanel:

```text
/home/u183827986/domains/clinicaideia.com.br/public_html/laudos
```

Enviar para esse diretorio:

```text
index.html
styles.css
app.js
config.js
```

Antes de enviar:

- Alterar `config.js` para apontar para `https://api.clinicaideia.com.br`.
- Confirmar que API ja esta publicada e com HTTPS.
- Confirmar CORS liberado na API.

## Limitacoes atuais

- Nao edita cadastro.
- Nao baixa laudo.
- Nao agenda.
- Nao substitui o desktop.
- Depende da API publicada e do banco sincronizado.

